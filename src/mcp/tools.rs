//! The read commands as MCP tools, named by their word on the line, each
//! taking its options without the dashes as arguments. No act is a tool:
//! `pause`, `resume`, the nouns' acts and `--who` are not arguments, and
//! `probe` is left out because loading a library runs its code. `report` is
//! one, because a Quest reads what it probes and changes nothing (ADR-0072,
//! amendment 2026-11-10).

use serde_json::Value;

use crate::invocation::audit_arguments::{self, VERIFY};
use crate::invocation::noun_arguments::{
    ID, INSTANCE, MESSAGE, MESSAGE_SHARED, NAME, SECRET_SHARED, SHARED,
};
use crate::invocation::report_arguments;
use crate::json::Json;

/// A tool: its word on the line, what it answers, the name and the
/// requiredness of its one argument where it takes one, the options that
/// take a value, and the options that are a flag.
struct Tool {
    name: &'static str,
    description: &'static str,
    argument: Option<&'static str>,
    required: bool,
    valued: Vec<&'static str>,
    flags: Vec<&'static str>,
}

/// The options whose value is a whole number.
const COUNTED: [&str; 3] = ["--offset", "--limit", "--id"];

fn tool(
    (name, description): (&'static str, &'static str),
    (argument, required): (Option<&'static str>, bool),
    valued: Vec<&'static str>,
    flags: Vec<&'static str>,
) -> Tool {
    Tool {
        name,
        description,
        argument,
        required,
        valued,
        flags,
    }
}

fn with(options: &[&'static str], more: &[&'static str]) -> Vec<&'static str> {
    options.iter().chain(more).copied().collect()
}

fn all() -> Vec<Tool> {
    vec![
        tool(
            ("abi", "The two boundaries this build speaks."),
            (None, false),
            vec![],
            vec![],
        ),
        tool(
            ("status", "What an Xmip status code means."),
            (Some("code"), true),
            vec![],
            vec![],
        ),
        tool(
            (
                "health",
                "Health at and beneath a scope, worst first, with the rollup and the worst \
                 leaf's evidence. scope is an Xmip URI, or a wildcard over the scopes that \
                 exist (* and ?).",
            ),
            (Some("scope"), true),
            vec![],
            vec![],
        ),
        tool(
            (
                "measure",
                "The six figures at a scope: Streams, Messages, Journeys, bytes, Retrying and \
                 Failed; an unpublished figure is null, never zero. The cluster when no scope \
                 is named.",
            ),
            (Some("scope"), false),
            vec![],
            vec![],
        ),
        tool(
            (
                "list",
                "The direct children of a scope, one row each: its mood, its worst leaf and \
                 why, its figures. The cluster when no scope is named.",
            ),
            (Some("scope"), false),
            vec![],
            vec![],
        ),
        tool(
            (
                "show",
                "One scope as a row: its mood, its worst leaf and why, its figures.",
            ),
            (Some("scope"), true),
            vec![],
            vec![],
        ),
        tool(
            (
                "validate",
                "Check a node configuration file without starting it.",
            ),
            (Some("toml"), true),
            vec![],
            vec![],
        ),
    ]
    .into_iter()
    .chain(records())
    .collect()
}

// The tools over what the nodes keep: the audit, the nouns and the Reports.
fn records() -> Vec<Tool> {
    vec![
        tool(
            (
                "audit",
                "What the audit recorded, newest first; verify walks each writer's audit chain \
                 and says where it breaks.",
            ),
            (Some("pattern"), false),
            audit_arguments::OPTIONS.to_vec(),
            vec![VERIFY],
        ),
        tool(
            (
                "subscriptions",
                "The Subscriptions the cluster's nodes route by.",
            ),
            (Some("pattern"), false),
            with(&SHARED, &[NAME]),
            vec![],
        ),
        tool(
            (
                "event-subscriptions",
                "The Event subscriptions the cluster's nodes hold.",
            ),
            (Some("pattern"), false),
            with(&SHARED, &[ID]),
            vec![],
        ),
        tool(
            (
                "dead-messages",
                "The Messages no Subscription matched, in each node's Dead Message Queue; \
                 message opens one.",
            ),
            (Some("pattern"), false),
            with(&SHARED, &[MESSAGE]),
            vec![],
        ),
        tool(
            (
                "message",
                "The Messages that failed at the Send Ports at or beneath location, Port by \
                 Port.",
            ),
            (None, false),
            MESSAGE_SHARED.to_vec(),
            vec![],
        ),
        tool(
            (
                "instances",
                "The Instances of each Work Process the nodes at or beneath location run, \
                 newest first: running, waiting, failed, dismissed or completed, with their \
                 values, and the Messages that failed in its queue; instance opens one with \
                 its history of Messages.",
            ),
            (Some("work-process"), false),
            with(&SECRET_SHARED, &[INSTANCE]),
            vec![],
        ),
        tool(
            (
                "report",
                "Every Report, each quick (it presents data that already exists) or taking \
                 longer (it presents what a Quest probes first); name shows one. With \
                 configuration, a node's configuration or a cluster.xmip with node, a Report \
                 that presents what a Quest probes is opened: its Quest probes the \
                 configuration and changes nothing.",
            ),
            (Some("name"), false),
            with(&report_arguments::OPTIONS, &["--node"]),
            vec![],
        ),
    ]
}

/// Every tool's name.
#[must_use]
pub fn names() -> Vec<&'static str> {
    all().iter().map(|tool| tool.name).collect()
}

/// `tools/list`'s result: every tool, its arguments' schema, and that it
/// reads and reaches nothing beyond Xmip.
pub fn write_list(json: &mut Json) {
    json.array("tools", |json| {
        for tool in all() {
            json.object_value(|json| {
                json.string("name", tool.name);
                json.string("description", tool.description);
                json.object("inputSchema", |json| schema(json, &tool));
                json.object("annotations", |json| {
                    json.boolean("readOnlyHint", true);
                    json.boolean("openWorldHint", false);
                });
            });
        }
    });
}

fn schema(json: &mut Json, tool: &Tool) {
    let typed = |json: &mut Json, name: &str, kind: &str| {
        json.object(name, |json| json.string("type", kind));
    };

    json.string("type", "object");
    json.object("properties", |json| {
        if let Some(argument) = tool.argument {
            typed(
                json,
                argument,
                if argument == "code" {
                    "integer"
                } else {
                    "string"
                },
            );
        }
        for option in &tool.valued {
            let kind = if COUNTED.contains(option) {
                "integer"
            } else {
                "string"
            };
            typed(json, &option[2..], kind);
        }
        for flag in &tool.flags {
            typed(json, &flag[2..], "boolean");
        }
    });
    if tool.required {
        json.array("required", |json| {
            json.string_value(tool.argument.unwrap_or_default());
        });
    }
    json.boolean("additionalProperties", false);
}

/// The line the tool `name` called with `arguments` types, `after`
/// following it: the session's surface and `--json`.
///
/// # Errors
///
/// A name that is no tool, arguments that are no object, an argument the
/// tool does not take or of another kind, or a required argument missing.
pub fn line(
    name: &str,
    arguments: Option<&Value>,
    after: &[String],
) -> Result<Vec<String>, String> {
    let tools = all();
    let Some(tool) = tools.iter().find(|each| each.name == name) else {
        return Err(format!(
            "'{name}' is not a tool. The tools are: {}.",
            names().join(", ")
        ));
    };

    let mut line = vec![tool.name.to_string()];
    let mut argued = false;
    let given = match arguments {
        None | Some(Value::Null) => None,
        Some(Value::Object(given)) => Some(given),
        Some(_) => return Err("arguments is an object.".to_string()),
    };

    for (named, value) in given.into_iter().flatten() {
        let option = format!("--{named}");

        if Some(named.as_str()) == tool.argument {
            let code = named == "code";
            match word(value, code) {
                Some(word) if code || !word.starts_with('-') => line.insert(1, word),
                _ => return Err(format!("{named} is a {}, and not an option.", kind(code))),
            }
            argued = true;
        } else if tool.valued.contains(&option.as_str()) {
            let counted = COUNTED.contains(&option.as_str());
            let Some(value) = word(value, counted) else {
                return Err(format!("{named} is a {}.", kind(counted)));
            };
            line.push(option);
            line.push(value);
        } else if tool.flags.contains(&option.as_str()) {
            match value {
                Value::Bool(true) => line.push(option),
                Value::Bool(false) => {}
                _ => return Err(format!("{named} is true or false.")),
            }
        } else {
            return Err(format!("'{named}' is not an argument of {}.", tool.name));
        }
    }

    if tool.required && !argued {
        return Err(format!(
            "{} needs {}.",
            tool.name,
            tool.argument.unwrap_or_default()
        ));
    }

    line.extend(after.iter().cloned());
    Ok(line)
}

// A value as the line types it: text as it is, a whole number in digits;
// none where it is of another kind.
fn word(value: &Value, counted: bool) -> Option<String> {
    match (value, counted) {
        (Value::String(text), false) => Some(text.clone()),
        (Value::Number(number), true) => number.as_i64().map(|number| number.to_string()),
        _ => None,
    }
}

const fn kind(counted: bool) -> &'static str {
    if counted { "whole number" } else { "string" }
}
