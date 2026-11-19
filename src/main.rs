//! `xmip`, the Xmip command line. The console and the audit are wired here
//! and nowhere else; what each command answers is `run`'s, the one `mcp`'s
//! tools answer by too.
//!
//! Every invocation is audited (ADR-0062): the command as it begins, its
//! end, a non-zero exit as a failure with what it said on stderr, a line
//! that could not be obeyed, and anything unhandled — which then ends the
//! process as it would have.

use std::io::Write;
use std::process::ExitCode;

use xmip_core_cli::command::Command;
use xmip_core_cli::invocation::Invocation;
use xmip_core_cli::run::{self, Writers};
use xmip_core_cli::system::interrupt;
use xmip_core_cli::{command_audit, mcp, surface_open};

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let audit = command_audit::open();
    audit.watch_panics();

    let invocation = match Invocation::parse(&args) {
        Ok(invocation) => invocation,
        Err(problem) => {
            xmip_core_cli::say(&mut std::io::stderr(), &problem);
            command_audit::refused(&audit, &problem);
            return ExitCode::from(2);
        }
    };

    command_audit::begun(&audit, &invocation);
    let mut said = Echoed::default();
    let exit = answer(&invocation, &audit, &mut said);
    command_audit::ended(&audit, &invocation, exit, &said.text());

    ExitCode::from(u8::try_from(exit).unwrap_or(1))
}

// What this process says of itself while it runs (ADR-0053): the scope it
// was asked about, or the whole tree, and the purpose its document states;
// then the answer, or the MCP server over standard input and output.
fn answer(
    invocation: &Invocation,
    audit: &xaudit::program_audit::ProgramAudit,
    said: &mut Echoed,
) -> i32 {
    let document = surface_open::document();
    let location = if invocation.argument.starts_with("xmip:") {
        invocation.argument.clone()
    } else {
        invocation
            .remote
            .clone()
            .unwrap_or_else(|| operate::surface::scope_tree::ROOT.to_string())
    };
    let _declared = declared(&location, &surface_open::purpose(&document));
    let mut input = std::io::stdin();

    if invocation.command == Command::Mcp {
        return mcp::server::serve(invocation, audit, &surface_open::open, said);
    }

    let mut output = std::io::stdout();
    let mut to = Writers {
        output: &mut output,
        error: said,
        input: &mut input,
        stop: interrupt::stop(),
    };
    let exit = run::answer(invocation, audit, &surface_open::open, &mut to);
    let _ = output.flush();
    exit
}

// This process declared as `xmip` at `location` for `purpose`, standing
// while it is held; nothing where it cannot be.
fn declared(location: &str, purpose: &str) -> Option<runtime::declared::Declared> {
    let purpose = node::Purpose::declared(purpose).ok()?;
    let declaration = node::Declaration::new(command_audit::PROGRAM, location, purpose).ok()?;
    runtime::declared::declare(&declaration).ok()
}

/// Standard error, and what was said on it, for the audit's record of a
/// failure.
#[derive(Default)]
struct Echoed {
    kept: Vec<u8>,
}

impl Echoed {
    fn text(&self) -> String {
        String::from_utf8_lossy(&self.kept).into_owned()
    }
}

impl Write for Echoed {
    fn write(&mut self, bytes: &[u8]) -> std::io::Result<usize> {
        self.kept.extend_from_slice(bytes);
        std::io::stderr().write_all(bytes)?;
        Ok(bytes.len())
    }

    fn flush(&mut self) -> std::io::Result<()> {
        std::io::stderr().flush()
    }
}
