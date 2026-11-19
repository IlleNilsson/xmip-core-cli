//! The MCP server itself: each message a line on standard input answered by
//! one on standard output, protocol messages and nothing else there, what
//! the server says of itself on stderr. It answers `initialize`, `ping`,
//! `tools/list`, `tools/call`, `resources/list`, `resources/templates/list`
//! and `resources/read`, takes every notification unanswered, and answers
//! any other method with JSON-RPC's method not found.

use std::io::{BufRead, Write};
use std::sync::atomic::AtomicBool;

use net::rpc::{self, Incoming, code};
use operate::surface::scope_tree::ROOT;
use serde_json::Value;
use xaudit::program_audit::ProgramAudit;

use crate::command::Command;
use crate::command_audit;
use crate::invocation::Invocation;
use crate::json::Json;
use crate::mcp::{error, resources, result, tools};
use crate::run::{self, Writers};
use crate::say;
use crate::surface_open::{Opener, Surface};

/// The revision of the protocol this server speaks, offered when a client
/// asks for one it does not understand.
pub const PROTOCOL_VERSION: &str = "2025-11-25";

/// The revisions whose messages this server's are, newest first.
const UNDERSTOOD: [&str; 3] = [PROTOCOL_VERSION, "2025-06-18", "2025-03-26"];

const INSTRUCTIONS: &str = "Xmip, read through its operator boundary, read only. Resources \
    are Xmip URIs (xmip:///<cluster>/node/<node>/...): read one for its row - mood, worst leaf \
    and why, figures - and the scopes beneath it, and read those to drill to a cause. Tools are \
    xmip's read commands; each answers one JSON document, and every row in it names its scope.";

/// A session: the `mcp` line, whose surface every tool and resource reads,
/// the program's audit every answer is recorded in, and the opener of the
/// surface a line reads.
pub struct McpServer<'a> {
    session: &'a Invocation,
    audit: &'a ProgramAudit,
    open: Opener<'a>,
}

/// Serve standard input and output until the input ends, `diagnostics`
/// told what the server says of itself.
pub fn serve(
    session: &Invocation,
    audit: &ProgramAudit,
    open: Opener<'_>,
    diagnostics: &mut dyn Write,
) -> i32 {
    let server = McpServer::new(session, audit, open);
    let mut input = std::io::stdin().lock();
    let mut output = std::io::stdout().lock();

    server.run(&mut input, &mut output, diagnostics)
}

impl<'a> McpServer<'a> {
    /// A session over the surface `session` states.
    #[must_use]
    pub fn new(session: &'a Invocation, audit: &'a ProgramAudit, open: Opener<'a>) -> Self {
        Self {
            session,
            audit,
            open,
        }
    }

    /// Answer every message on `input`, one line each on `output`, until
    /// the input ends: exit 0, or 1 where a stream broke.
    pub fn run(
        &self,
        input: &mut impl BufRead,
        output: &mut dyn Write,
        diagnostics: &mut dyn Write,
    ) -> i32 {
        say(
            diagnostics,
            &format!("xmip mcp: speaking MCP {PROTOCOL_VERSION} on stdin and stdout."),
        );

        loop {
            let line = match rpc::line::read(input) {
                Ok(Some(line)) => line,
                Ok(None) => return 0,
                Err(broken) => {
                    say(diagnostics, &format!("xmip mcp: {broken}"));
                    return 1;
                }
            };

            if let Some(answer) = self.answer(&line, diagnostics)
                && let Err(broken) = rpc::line::write_text(output, &answer)
            {
                say(diagnostics, &format!("xmip mcp: {broken}"));
                return 1;
            }
        }
    }

    /// The answer to one message, or `None` for a notification, which is
    /// never answered.
    pub fn answer(&self, line: &str, diagnostics: &mut dyn Write) -> Option<String> {
        let message = match rpc::parse(line) {
            Ok(message) => message,
            Err(unread) => return Some(said(&unread)),
        };

        let (id, method, parameters) = match rpc::incoming(&message) {
            Err(refused) => return Some(said(&refused)),
            Ok(Incoming::Notification { method, .. }) => {
                // notifications/initialized and the rest are taken and
                // never answered.
                say(diagnostics, &format!("xmip mcp: {method}."));
                return None;
            }
            Ok(Incoming::Request { id, method, params }) => (id, method, params),
        };

        Some(match method {
            "initialize" => result(id, |json| initialize(json, parameters)),
            "ping" => result(id, |_| {}),
            "tools/list" => result(id, tools::write_list),
            "tools/call" => self.call(id, parameters),
            "resources/list" => resources::list(self, id),
            "resources/templates/list" => result(id, resources::write_templates),
            "resources/read" => resources::read(self, id, parameters),
            _ => error(
                Some(id),
                code::METHOD_NOT_FOUND,
                &format!("Method not found: {method}."),
            ),
        })
    }

    /// Answer a command line as `xmip` would, audited as that command: its
    /// exit, what it wrote on its output and on its error writer. A line
    /// that cannot be obeyed, or that acts, is answered exit 2 with why.
    #[must_use]
    pub fn answered(&self, line: &[String]) -> (i32, String, String) {
        let refused = match Invocation::parse(line) {
            Err(problem) => Err(problem),
            Ok(invocation) if acts(&invocation) => Err(format!(
                "REFUSED: '{}' acts, and mcp reads only.",
                line.join(" ")
            )),
            Ok(invocation) => Ok(invocation),
        };
        let invocation = match refused {
            Ok(invocation) => invocation,
            Err(refused) => {
                command_audit::refused(self.audit, &refused);
                return (2, String::new(), refused);
            }
        };

        let (mut output, mut error, mut input) = (Vec::new(), Vec::new(), std::io::empty());
        let stop = AtomicBool::new(false);
        command_audit::begun(self.audit, &invocation);
        let exit = run::answer(
            &invocation,
            self.audit,
            self.open,
            &mut Writers {
                output: &mut output,
                error: &mut error,
                input: &mut input,
                stop: &stop,
            },
        );
        let (output, error) = (text(&output), text(&error));
        command_audit::ended(self.audit, &invocation, exit, &error);

        (exit, output, error)
    }

    /// `words` followed by what every line a tool or a resource answers by
    /// carries: the surface the `mcp` line stated — `--remote`, `--local`,
    /// `--snapshot` — and `--json`, since a client reads documents.
    #[must_use]
    pub fn line(&self, words: &[&str]) -> Vec<String> {
        let mut line: Vec<String> = words.iter().map(ToString::to_string).collect();
        line.extend(self.session_line());
        line
    }

    /// The session's surface and `--json`.
    #[must_use]
    pub fn session_line(&self) -> Vec<String> {
        let mut line = Vec::new();
        for (option, value) in [
            ("--remote", &self.session.remote),
            ("--local", &self.session.local),
            ("--snapshot", &self.session.snapshot),
        ] {
            if let Some(value) = value {
                line.push(option.to_string());
                line.push(value.clone());
            }
        }
        line.push("--json".to_string());
        line
    }

    /// The surface the session's line states.
    ///
    /// # Errors
    ///
    /// Why it cannot be read.
    pub fn surface(&self) -> Result<Surface, String> {
        (self.open)(self.session)
    }

    fn call(&self, id: &Value, parameters: Option<&Value>) -> String {
        let Some(name) = parameters
            .filter(|given| given.is_object())
            .and_then(|given| given.get("name"))
            .and_then(Value::as_str)
        else {
            return error(
                Some(id),
                code::INVALID_PARAMS,
                "tools/call needs the tool's name.",
            );
        };
        let arguments = parameters.and_then(|given| given.get("arguments"));
        let line = match tools::line(name, arguments, &self.session_line()) {
            Ok(line) => line,
            Err(problem) => return error(Some(id), code::INVALID_PARAMS, &problem),
        };

        let (exit, output, error) = self.answered(&line);
        let scope = scope(&line);

        result(id, |json| {
            json.array("content", |json| {
                json.object_value(|json| {
                    json.string("type", "text");
                    json.string("text", &said_by(exit, &output, &error));
                });
            });
            json.boolean("isError", exit != 0);
            json.object("_meta", |json| {
                json.string("xmip/scope", &scope);
                json.number("xmip/exit", exit);
            });
        })
    }
}

fn initialize(json: &mut Json, parameters: Option<&Value>) {
    let asked = parameters
        .and_then(|given| given.get("protocolVersion"))
        .and_then(Value::as_str)
        .unwrap_or(PROTOCOL_VERSION);

    json.string(
        "protocolVersion",
        if UNDERSTOOD.contains(&asked) {
            asked
        } else {
            PROTOCOL_VERSION
        },
    );
    json.object("capabilities", |json| {
        json.object("tools", |json| json.boolean("listChanged", false));
        json.object("resources", |json| {
            json.boolean("listChanged", false);
            json.boolean("subscribe", false);
        });
    });
    json.object("serverInfo", |json| {
        json.string("name", command_audit::PROGRAM);
        json.string("version", env!("CARGO_PKG_VERSION"));
    });
    json.string("instructions", INSTRUCTIONS);
}

// Whether a parsed line acts rather than reads: pause, resume, probe, a
// noun's act, setting a secret, or a who.
fn acts(invocation: &Invocation) -> bool {
    matches!(
        invocation.command,
        Command::Pause | Command::Resume | Command::Probe | Command::Mcp | Command::Help
    ) || invocation.who.is_some()
        || invocation.follow
        || invocation.act.is_some()
        || invocation.secret.as_ref().is_some_and(|ask| ask.set)
}

// The scope a line asks at: its argument where that is an Xmip URI, else
// its --location, else the root.
fn scope(line: &[String]) -> String {
    if let Some(argument) = line.get(1).filter(|argument| argument.starts_with("xmip:")) {
        return argument.clone();
    }

    line.windows(2)
        .skip(1)
        .find(|pair| pair[0] == "--location")
        .map_or_else(|| ROOT.to_string(), |pair| pair[1].clone())
}

// What a command said: its document; where it failed, what it said on its
// error writer too, as the operator would read both.
fn said_by(exit: i32, output: &str, error: &str) -> String {
    let mut said = output.trim_end().to_string();

    if exit != 0 && !error.trim().is_empty() {
        if !said.is_empty() {
            said.push('\n');
        }
        said.push_str(error.trim_end());
    }

    said
}

// A refusal `net::rpc` wrote, written again in this server's order: the
// id it carries, its code and its words.
fn said(refused: &Value) -> String {
    let id = refused.get("id").filter(|id| !id.is_null());
    let code = refused["error"]["code"]
        .as_i64()
        .unwrap_or(code::INTERNAL_ERROR);
    let message = refused["error"]["message"].as_str().unwrap_or_default();

    error(id, code, message)
}

fn text(bytes: &[u8]) -> String {
    String::from_utf8_lossy(bytes).into_owned()
}
