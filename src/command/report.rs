//! `xmip report [name]`: every Report in one list, Xmip's own and each one a
//! module adds, each saying whether it is quick — it presents data that
//! already exists — or presents what a Quest probes first; one shown with
//! the command that presents it or how it is probed; and one opened, its
//! Quest probing a node's configuration (ADR-0072, amendment 2026-11-10).
//! The list, each entry's kind, what a probe finds and every refusal are
//! the runtime's (`runtime::report`), each probe audited by it as
//! `quest.probe` under program `xmip`, where this executable's records go,
//! and read back from there. `--json` is the runtime's document whole.

use std::io::Write;
use std::path::{Path, PathBuf};

use audit_file::FileSink;
use serde_json::{Value, json};
use xaudit::program_audit::ProgramAudit;

use crate::command_audit::PROGRAM;
use crate::invocation::Invocation;
use crate::invocation::report_arguments::ReportAsk;
use crate::json::Json;
use crate::text_table;
use crate::{say, surface_open};

/// This surface's command for each reader of the header a quick Report
/// names: the one place a quick entry is opened.
const PRESENTING: [(&str, &str); 4] = [
    (abi::operate::audit::AUDIT_READ_ENTRYPOINT, "xmip audit"),
    (
        abi::operate::dead_message::DEAD_MESSAGES_ENTRYPOINT,
        "xmip dead-messages",
    ),
    (
        abi::operate::subscription::SUBSCRIPTIONS_ENTRYPOINT,
        "xmip subscriptions",
    ),
    (abi::operate::publication::READ_ENTRYPOINT, "xmip list"),
];

/// Where a program's records go: its name and its audit directory, `None`
/// for the one the capability decides.
pub struct Audited<'a> {
    pub program: &'a str,
    pub directory: Option<PathBuf>,
}

/// Answer the line's `report`, audited where this executable's records go.
pub fn run(
    invocation: &Invocation,
    _audit: &ProgramAudit,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    let audited = Audited {
        program: PROGRAM,
        directory: surface_open::audit_directory(&surface_open::document()),
    };
    let asked = invocation.report.clone().unwrap_or_default();

    answer(&asked, &audited, invocation.json, output, error)
}

/// Answer `asked`, each probe audited as `audited` says: exit 0, or 1 with
/// the runtime's refusal.
pub fn answer(
    asked: &ReportAsk,
    audited: &Audited<'_>,
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    let directory = audited
        .directory
        .as_deref()
        .map(|path| path.display().to_string())
        .unwrap_or_default();
    let said = if let Some(configuration) = &asked.configuration {
        let mut arguments = json!({
            "report": asked.name,
            "configuration": configuration,
            "program": audited.program,
            "directory": directory,
        });
        for (name, value) in [
            ("parameter", &asked.parameter),
            ("row", &asked.row),
            ("node", &asked.node),
        ] {
            if let Some(value) = value.as_deref().filter(|value| !value.trim().is_empty()) {
                arguments[name] = json!(value);
            }
        }
        ask("open", &arguments).map(|text| (text, Opened::Probed))
    } else {
        let mut arguments = json!({ "directory": directory });
        if !asked.name.is_empty() {
            arguments["report"] = json!(asked.name);
        }
        let opened = if asked.name.is_empty() {
            Opened::Listed
        } else {
            Opened::Shown
        };
        ask("list", &arguments).map(|text| (text, opened))
    };

    let (text, opened) = match said {
        Ok(said) => said,
        Err(refused) => {
            say(
                error,
                &if json {
                    Json::document(|json| json.string("refused", &refused))
                } else {
                    refused
                },
            );
            return 1;
        }
    };

    if json {
        say(output, &text);
        return 0;
    }

    let answer: Value = serde_json::from_str(&text).unwrap_or_default();
    match opened {
        Opened::Listed => listed(&answer, output),
        Opened::Shown => shown(&answer, output),
        Opened::Probed => probed(&answer, output),
    }
    0
}

/// Which answer the runtime gave.
#[derive(Clone, Copy)]
enum Opened {
    Listed,
    Shown,
    Probed,
}

// The runtime's answer to `act`, its records audited under the program the
// arguments name, where they say.
fn ask(act: &str, arguments: &Value) -> Result<String, String> {
    let audit =
        |program: &str, stated: Option<&Path>| ProgramAudit::new(program, FileSink::stated(stated));

    runtime::report::act(act, &arguments.to_string(), usize::MAX, &audit).map_err(|unanswered| {
        match unanswered {
            runtime::report::Unanswered::Refused(said)
            | runtime::report::Unanswered::Failed(said) => said,
        }
    })
}

// Every Report, the kinds' words first.
fn listed(answer: &Value, output: &mut dyn Write) {
    let kinds = list(answer, "kinds");
    let entries = list(answer, "reports");

    say(
        output,
        &format!("{} Report(s), each one of:", entries.len()),
    );
    let words: Vec<Vec<String>> = kinds
        .iter()
        .map(|kind| {
            vec![
                text(kind, "name").to_string(),
                text(kind, "meaning").to_string(),
            ]
        })
        .collect();
    text_table::write(output, "  ", &words);
    say(output, "");

    let mut rows = vec![
        ["kind", "report", "expect", "covers"]
            .map(String::from)
            .to_vec(),
    ];
    rows.extend(entries.iter().map(|entry| {
        vec![
            kind_of(answer, entry, "name"),
            text(entry, "name").to_string(),
            text(entry, "expect").to_string(),
            text(entry, "covers").to_string(),
        ]
    }));
    text_table::write(output, "  ", &rows);
    unread(answer, output);
}

// One entry, as the runtime listed it alone: what it is, and how it is
// opened.
fn shown(answer: &Value, output: &mut dyn Write) {
    let Some(entry) = list(answer, "reports").first().cloned() else {
        return;
    };
    let name = text(&entry, "name");

    say(
        output,
        &format!(
            "{name} — {}: {}",
            kind_of(answer, &entry, "name"),
            kind_of(answer, &entry, "meaning")
        ),
    );
    say(output, &format!("  {}", text(&entry, "covers")));
    if let Some(expect) = entry["expect"].as_str() {
        say(output, &format!("  Expect: {expect}"));
    }
    if let Some(reader) = entry["read_by"].as_str() {
        say(
            output,
            &PRESENTING
                .iter()
                .find(|(read, _)| *read == reader)
                .map_or_else(
                    || format!("  Read by {reader}, which this command line does not present."),
                    |(_, command)| format!("  Presented by: {command}"),
                ),
        );
    }
    let parameter = entry["parameter"].as_object().map(|_| &entry["parameter"]);
    if let Some(parameter) = parameter {
        say(
            output,
            &format!(
                "  {}: {}",
                text(parameter, "name"),
                strings(parameter, "choices").join(", ")
            ),
        );
    }
    let levels = strings(&entry, "levels");
    if !levels.is_empty() {
        let choice = parameter.map_or_else(String::new, |named| {
            format!(" --parameter <{}>", text(named, "name"))
        });
        say(output, &format!("  Levels: {}", levels.join("; ")));
        say(
            output,
            &format!(
                "  Probe it: xmip report \"{name}\" --configuration <path> [--node <node>]{choice} \
                 [--row <{}>]",
                text(&entry, "row")
            ),
        );
    }
    unread(answer, output);
}

// A probe: the tally and a row per requirement, or one in full; what the
// Quest did not answer is left out, never shown as nothing found.
fn probed(answer: &Value, output: &mut dyn Write) {
    let title = Some(text(answer, "title"))
        .filter(|title| !title.is_empty())
        .unwrap_or(text(answer, "report"));
    let took = answer["took_ms"].as_i64().unwrap_or(0);

    say(
        output,
        &format!(
            "{title} — node {}, probed in {took} ms, nothing changed",
            text(answer, "node")
        ),
    );

    if let Some(one) = answer.get("requirement").filter(|one| one.is_object()) {
        say(
            output,
            &format!(
                "{} ({}): {}",
                text(one, "name"),
                text(one, "citation"),
                text(one, "state")
            ),
        );
        say(output, &format!("  {}", text(one, "says")));
        let behind: Vec<String> = list(one, "behind").iter().map(behind_said).collect();
        let actions: Vec<String> = list(one, "actions").iter().map(action_said).collect();
        said(output, "behind it", &behind);
        said(output, "actions", &actions);
        return;
    }

    let tally: Vec<String> = list(answer, "tally")
        .iter()
        .map(|count| {
            format!(
                "{} {}",
                text(count, "state"),
                count["count"].as_i64().unwrap_or(0)
            )
        })
        .collect();
    if !tally.is_empty() {
        say(output, &format!("  {}", tally.join(", ")));
    }

    let rows = list(answer, "rows");
    if !rows.is_empty() {
        say(output, "");
        let mut table = vec![vec![
            "state".to_string(),
            text(answer, "row").to_string(),
            "citation".to_string(),
        ]];
        table.extend(rows.iter().map(|row| {
            vec![
                text(row, "state").to_string(),
                text(row, "name").to_string(),
                text(row, "citation").to_string(),
            ]
        }));
        text_table::write(output, "  ", &table);
    }
}

// What stands behind a requirement: its kind, then each other field's
// value.
fn behind_said(behind: &Value) -> String {
    let fields: Vec<String> = behind
        .as_object()
        .into_iter()
        .flatten()
        .filter(|(name, _)| name.as_str() != "kind")
        .map(|(_, value)| {
            value
                .as_str()
                .map_or_else(|| value.to_string(), str::to_string)
        })
        .collect();
    format!("{}: {}", text(behind, "kind"), fields.join(" "))
}

// An action that closes a requirement, with its setting where it names one.
fn action_said(action: &Value) -> String {
    match action["setting"].as_str() {
        Some(setting) => format!(
            "{} {setting}: {}",
            text(action, "kind"),
            text(action, "says")
        ),
        None => format!("{}: {}", text(action, "kind"), text(action, "says")),
    }
}

fn said(output: &mut dyn Write, heading: &str, said: &[String]) {
    if said.is_empty() {
        return;
    }
    say(output, &format!("  {heading}:"));
    for each in said {
        say(output, &format!("    {each}"));
    }
}

// Why each Quest's last probe could not be read, where it could not.
fn unread(answer: &Value, output: &mut dyn Write) {
    if let Some(unread) = answer["unread"].as_str() {
        say(output, "");
        say(output, &format!("  {unread}"));
    }
}

// The words of an entry's kind, as the list words it.
fn kind_of(answer: &Value, entry: &Value, word: &str) -> String {
    list(answer, "kinds")
        .iter()
        .find(|kind| kind["kind"] == entry["kind"])
        .map(|kind| text(kind, word).to_string())
        .unwrap_or_default()
}

fn list(value: &Value, name: &str) -> Vec<Value> {
    value[name].as_array().cloned().unwrap_or_default()
}

fn strings(value: &Value, name: &str) -> Vec<String> {
    list(value, name)
        .iter()
        .map(|one| one.as_str().unwrap_or_default().to_string())
        .collect()
}

fn text<'v>(value: &'v Value, name: &str) -> &'v str {
    value[name].as_str().unwrap_or_default()
}
