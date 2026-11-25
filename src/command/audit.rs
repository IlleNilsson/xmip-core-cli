//! `xmip audit [pattern]`: what the audit recorded, read back from the file
//! this executable's own records go to — every program's records in it,
//! not only this one's — through the audit capability's one reader
//! (`operate::audit_read`; ADR-0062, amendment 2026-09-29), the reader the
//! web's Audit view and `Get-XmipAudit` call. Its argument and options are
//! the query's words, and what each means is the capability's.
//!
//! Text is a header, the groups one step down the drill, then a table, or
//! with `--record` that record's every field; `--json` is the read whole.
//! `--verify` says each writer's chain and each fragment no write finished
//! after the records (ADR-0070 clause 5). A query the capability does not
//! take is its REFUSED sentence, exit 2; a chain that breaks, a sealed
//! fragment or a record not found exits 1.

use std::collections::BTreeMap;
use std::io::Write;
use std::path::Path;

use operate::audit_read::{self, AuditRead, Unread};
use operate::surface::english;
use xaudit::audit_entry::AuditEntry;

use crate::invocation::Invocation;
use crate::invocation::audit_arguments::AuditAsk;
use crate::json::Json;
use crate::text_table::{self, left};
use crate::{say, surface_open};

/// The columns of the table, as the capability names them.
const HEADINGS: [&str; 7] = [
    "at", "node", "program", "action", "phase", "severity", "summary",
];

/// Read back what `invocation` asks of the audit this executable's records
/// go to.
pub fn run(invocation: &Invocation, output: &mut dyn Write, error: &mut dyn Write) -> i32 {
    let ask = invocation.audit.clone().unwrap_or_default();
    let directory = surface_open::audit_directory(&surface_open::document());

    read_back(directory.as_deref(), &ask, invocation.json, output, error)
}

/// Read back what `ask` asks of the audit kept in `directory` — or, where
/// none is named, where the capability decides — and render it.
pub fn read_back(
    directory: Option<&Path>,
    ask: &AuditAsk,
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    let read = match audit_read::read(directory, &ask.pairs()) {
        Ok(read) => read,
        Err(Unread::Refused(said)) => {
            let said = if json {
                Json::document(|json| json.string("refused", &said))
            } else {
                said
            };
            say(error, &said);
            return 2;
        }
        Err(Unread::Io(unread)) => {
            say(error, &unread);
            return 1;
        }
    };

    if read.file.is_empty() {
        say(error, english::NO_AUDIT_DIRECTORY);
        return 1;
    }

    let one = ask
        .record
        .as_deref()
        .is_some_and(|record| !record.trim().is_empty());
    if one && read.records.is_empty() {
        let record = ask.record.as_deref().unwrap_or_default();
        say(error, &format!("No record {record} in {}.", read.file));
    }

    if json {
        say(output, &document(&read));
    } else if one {
        if let Some(entry) = read.records.first() {
            write_record(output, entry);
        }
    } else {
        write_text(output, &read);
    }

    if !json && read.chains.len() + read.fragments.len() > 0 {
        // Each writer's chain, in words: OK or FAILED, and where; then each
        // fragment no write finished, FAILED or UNFINISHED.
        say(output, "");
        for said in read.chains.iter().map(xaudit::audit_chain::Verdict::said) {
            say(output, &said);
        }
        for said in read
            .fragments
            .iter()
            .map(xaudit::audit_fragment::Fragment::said)
        {
            say(output, &said);
        }
    }

    // A chain that breaks is a failure, as a record not found is, and so is
    // a sealed fragment, a write that was interrupted; the file's unfinished
    // end is not, as a write may still be going on.
    let broken = read.chains.iter().any(|chain| !chain.whole())
        || read.fragments.iter().any(|fragment| fragment.sealed);

    i32::from((one && read.records.is_empty()) || broken)
}

/// The read whole, as one document.
#[must_use]
pub fn document(read: &AuditRead) -> String {
    Json::document(|json| {
        json.string("file", &read.file);
        json.number("read", count(read.read));
        json.number("matched", count(read.matched));
        json.number("offset", count(read.offset));
        json.number("limit", count(read.limit));
        json.array("groups", |json| {
            for group in &read.groups {
                json.object_value(|json| {
                    json.string("kind", group.kind);
                    json.string("who", &group.who);
                    json.number("count", count(group.count));
                    json.number("warnings", count(group.warnings));
                    json.number("errors", count(group.errors));
                    json.string("latest", &group.latest);
                });
            }
        });
        json.array("records", |json| {
            for entry in &read.records {
                json.object_value(|json| write_entry(json, entry));
            }
        });
        words(json, "actions", read.actions.iter().map(String::as_str));
        json.array("chains", |json| {
            for chain in &read.chains {
                json.object_value(|json| {
                    json.string("writer", &chain.writer);
                    json.number("records", chain.records);
                    json.boolean("whole", chain.whole());
                    json.string("said", &chain.said());
                });
            }
        });
        json.array("fragments", |json| {
            for fragment in &read.fragments {
                json.object_value(|json| {
                    json.number("offset", count(fragment.offset));
                    json.number("length", count(fragment.length));
                    json.boolean("sealed", fragment.sealed);
                    json.string("said", &fragment.said());
                });
            }
        });
        words(json, "columns", read.columns.iter().copied());
        words(json, "severities", read.severities.iter().copied());
    })
}

// The header, the groups one step down, then one row per record.
fn write_text(output: &mut dyn Write, read: &AuditRead) {
    say(
        output,
        &format!("{} of {} records in {}", read.matched, read.read, read.file),
    );

    if read.records.len() < read.matched {
        let last = read.offset + read.records.len();
        say(
            output,
            &if read.records.is_empty() {
                format!("none from offset {}", read.offset)
            } else {
                format!(
                    "showing {} to {last}; --offset {last} for the next page",
                    read.offset + 1
                )
            },
        );
    }

    if !read.groups.is_empty() {
        say(output, "");
        let kind = read
            .groups
            .iter()
            .map(|group| text_table::width(group.kind))
            .max()
            .unwrap_or(0);
        let who = read
            .groups
            .iter()
            .map(|group| text_table::width(&group.who))
            .max()
            .unwrap_or(0);
        for group in &read.groups {
            say(
                output,
                &format!(
                    "  {}  {}  {} records, {} warnings, {} errors, latest {}",
                    left(group.kind, kind),
                    left(&group.who, who),
                    group.count,
                    group.warnings,
                    group.errors,
                    english::to_the_second(&group.latest)
                ),
            );
        }
    }

    if read.records.is_empty() {
        return;
    }

    say(output, "");
    let mut rows = vec![HEADINGS.iter().map(ToString::to_string).collect::<Vec<_>>()];
    rows.extend(read.records.iter().map(row));
    text_table::write(output, "", &rows);
}

// One record, every field and every property, one to a line.
fn write_record(output: &mut dyn Write, entry: &AuditEntry) {
    let fields = [
        ("audit_id", Some(entry.audit_id.as_str())),
        ("at", Some(entry.at.as_str())),
        ("program", Some(entry.program.as_str())),
        ("host", Some(entry.host.as_str())),
        ("process", Some(entry.process.as_str())),
        ("location", entry.location.as_deref()),
        ("node", entry.node()),
        ("cluster", entry.cluster()),
        ("action", Some(entry.action.as_str())),
        ("phase", Some(entry.phase.as_str())),
        ("severity", Some(entry.severity.as_str())),
        ("message", entry.message.as_deref()),
    ];

    for (name, value) in fields {
        say(
            output,
            &format!(
                "{}  {}",
                left(name, 10),
                english::value(value.unwrap_or_default())
            ),
        );
    }

    table(output, "scope", &entry.scope);
    table(output, "properties", &entry.properties);
}

fn table(output: &mut dyn Write, heading: &str, pairs: &BTreeMap<String, String>) {
    if pairs.is_empty() {
        return;
    }

    say(output, heading);
    let width = pairs
        .keys()
        .map(|key| text_table::width(key))
        .max()
        .unwrap_or(0);
    for (key, value) in pairs {
        say(output, &format!("  {}  {value}", left(key, width)));
    }
}

fn row(entry: &AuditEntry) -> Vec<String> {
    vec![
        english::to_the_second(&entry.at),
        english::value(entry.node().unwrap_or_default()).to_string(),
        entry.program.clone(),
        entry.action.clone(),
        entry.phase.clone(),
        entry.severity.clone(),
        entry.summary(),
    ]
}

fn write_entry(json: &mut Json, entry: &AuditEntry) {
    json.string("audit_id", &entry.audit_id);
    json.string("at", &entry.at);
    json.string("program", &entry.program);
    json.string("host", &entry.host);
    json.string("process", &entry.process);
    json.string_or_null("location", entry.location.as_deref());
    json.string_or_null("node", entry.node());
    json.string_or_null("cluster", entry.cluster());
    json.string("action", &entry.action);
    json.string("phase", &entry.phase);
    json.string("severity", &entry.severity);
    json.string_or_null("message", entry.message.as_deref());
    json.string("summary", &entry.summary());
    pairs(json, "scope", &entry.scope);
    pairs(json, "properties", &entry.properties);
}

fn pairs(json: &mut Json, name: &str, pairs: &BTreeMap<String, String>) {
    json.object(name, |json| {
        for (key, value) in pairs {
            json.string(key, value);
        }
    });
}

fn words<'a>(json: &mut Json, name: &str, words: impl Iterator<Item = &'a str>) {
    json.array(name, |json| {
        for word in words {
            json.string_value(word);
        }
    });
}

fn count(value: usize) -> u64 {
    u64::try_from(value).unwrap_or(u64::MAX)
}
