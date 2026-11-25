//! `xmip instances [work-process]`: the Instances of each Work Process the
//! nodes at or beneath `--location` run (runtime-model.md section 22;
//! ADR-0066, amendment 2026-11-25) — each running, waiting for a Message
//! of its key until its deadline, failed with its code and why, dismissed
//! or completed, its own values and turns — and the Messages that failed
//! in its queue, each by the Journey `xmip message <journey> --retry` or
//! `--dismiss` names; with `--instance`, one opened with its history of
//! Messages. An Instance takes no act of its own.

use observe::{WorkProcessInstance, WorkProcessInstances};
use operate::surface::OperatorSurface;
use operate::surface::instance_query::InstanceQuery;

use crate::command::noun::count;
use crate::invocation::NounAsk;
use crate::json::Json;
use crate::run::Writers;
use crate::text_table;
use crate::time;

/// The columns a person reads of an Instance.
const HEADINGS: [&str; 7] = [
    "instance",
    "state",
    "waits for",
    "until",
    "correlation",
    "values",
    "turns",
];

/// The query the line states: where, the Work Process its argument names,
/// and the Instance `--instance` names (`operate::surface::instance_query`,
/// the one every surface asks).
#[must_use]
pub fn query(asked: &NounAsk) -> InstanceQuery {
    InstanceQuery {
        location: asked.location.clone(),
        work_process: asked.pattern.clone(),
        instance: asked.selectors.instance.clone(),
    }
}

/// List what `asked` selects over `surface`, or open the Instance it names.
pub fn over(
    surface: &dyn OperatorSurface,
    asked: &NounAsk,
    json: bool,
    to: &mut Writers<'_>,
) -> i32 {
    let chosen = query(asked).apply(&surface.work_process_instances());
    let source = surface.source();
    if let Some(id) = asked.selectors.instance.as_deref() {
        return opened(&chosen, id, json, to);
    }
    if json {
        crate::say(to.output, &document(&source, &chosen));
        return 0;
    }
    if chosen.is_empty() {
        crate::say(
            to.output,
            &format!("No Work Process runs at or beneath where it was asked — {source}"),
        );
        return 0;
    }
    for list in &chosen {
        listed(list, to);
    }
    0
}

/// One Work Process's Instances, and the Messages that failed in its queue.
fn listed(list: &WorkProcessInstances, to: &mut Writers<'_>) {
    crate::say(
        to.output,
        &format!(
            "{} Work Process {}: {} Instance(s), {} failed in its queue",
            list.node,
            list.work_process,
            list.instances.len(),
            list.failed_count
        ),
    );
    if !list.instances.is_empty() {
        let mut rows = vec![HEADINGS.map(String::from).to_vec()];
        rows.extend(list.instances.iter().map(row));
        text_table::write(to.output, "  ", &rows);
    }
    for failed in &list.failed {
        crate::say(
            to.output,
            &format!(
                "  FAILED  {}  place {}  {}",
                failed.journey, failed.sequence, failed.reason
            ),
        );
    }
    if let Some(first) = list.failed.first() {
        crate::say(
            to.output,
            &format!(
                "  retry or dismiss: xmip message {} --retry --location {}",
                first.journey, list.node
            ),
        );
    }
}

fn row(one: &WorkProcessInstance) -> Vec<String> {
    vec![
        one.instance.clone(),
        one.state.clone(),
        one.waits_for.clone(),
        one.until_unix_nanos
            .map_or_else(String::new, time::round_trip),
        pairs(&one.correlations),
        pairs(&one.values),
        one.turns.to_string(),
    ]
}

/// Name and value pairs as one cell: `name value, name value`.
fn pairs(pairs: &[[String; 2]]) -> String {
    pairs
        .iter()
        .map(|[name, value]| format!("{name} {value}"))
        .collect::<Vec<_>>()
        .join(", ")
}

/// The Instance `id` among `chosen`, opened, or refused where none is.
fn opened(chosen: &[WorkProcessInstances], id: &str, json: bool, to: &mut Writers<'_>) -> i32 {
    let found = chosen.iter().find_map(|list| {
        list.instances
            .iter()
            .find(|one| one.instance == id)
            .map(|one| (list, one))
    });
    let Some((list, one)) = found else {
        crate::say(
            to.error,
            &format!("REFUSED: no Instance {id} is among the Instances listed here."),
        );
        return 1;
    };
    if json {
        let text = Json::document(|json| {
            json.string("node", &list.node);
            json.string("work_process", &list.work_process);
            json.object("instance", |json| instance(json, one));
        });
        crate::say(to.output, &text);
        return 0;
    }
    let say = |to: &mut Writers<'_>, line: String| crate::say(to.output, &line);
    say(
        to,
        format!(
            "Instance {} of {} on {}: {}",
            one.instance, list.work_process, list.node, one.state
        ),
    );
    if !one.waits_for.is_empty() {
        let until = one.until_unix_nanos.map_or_else(String::new, |due| {
            format!(" until {}", time::round_trip(due))
        });
        say(to, format!("  waits for a {}{until}", one.waits_for));
    }
    if !one.code.is_empty() {
        say(to, format!("  threw {}: {}", one.code, one.reason));
        say(
            to,
            format!(
                "  retry or dismiss: xmip message {} --retry --location {}",
                one.failed_by, list.node
            ),
        );
    }
    say(
        to,
        format!("  correlation: {}", or_none(&pairs(&one.correlations))),
    );
    say(to, format!("  values: {}", or_none(&pairs(&one.values))));
    say(to, format!("  turns: {}", one.turns));
    say(to, "  history:".to_string());
    if one.earlier > 0 {
        say(to, format!("    ({} earlier)", one.earlier));
    }
    let rows: Vec<Vec<String>> = one
        .history
        .iter()
        .map(|entry| {
            vec![
                time::round_trip(entry.at_unix_nanos),
                entry.what.clone(),
                entry.message.clone(),
                entry.journey.clone(),
            ]
        })
        .collect();
    text_table::write(to.output, "    ", &rows);
    0
}

fn or_none(text: &str) -> &str {
    if text.is_empty() { "none" } else { text }
}

/// The lists as one document, in the shape `xmip_operate.h` section 29
/// writes them.
#[must_use]
pub fn document(source: &str, chosen: &[WorkProcessInstances]) -> String {
    Json::document(|json| {
        json.string("source", source);
        json.number("matched", count(chosen.len()));
        json.array("work_process_instances", |json| {
            for list in chosen {
                json.object_value(|json| {
                    json.string("node", &list.node);
                    json.string("work_process", &list.work_process);
                    json.number("failed_count", list.failed_count);
                    json.array("failed", |json| {
                        for failed in &list.failed {
                            json.object_value(|json| {
                                json.string("journey", &failed.journey);
                                json.number("sequence", failed.sequence);
                                json.string("reason", &failed.reason);
                            });
                        }
                    });
                    json.array("instances", |json| {
                        for one in &list.instances {
                            json.object_value(|json| instance(json, one));
                        }
                    });
                });
            }
        });
    })
}

/// One Instance's members.
fn instance(json: &mut Json, one: &WorkProcessInstance) {
    json.string("instance", &one.instance);
    json.string("state", &one.state);
    json.string("waits_for", &one.waits_for);
    match one.until_unix_nanos {
        Some(due) => json.moment("until", &time::document(due)),
        None => json.null("until"),
    }
    pairs_of(json, "correlations", &one.correlations);
    json.string("code", &one.code);
    json.string("reason", &one.reason);
    json.string("failed_by", &one.failed_by);
    pairs_of(json, "values", &one.values);
    json.array("history", |json| {
        for entry in &one.history {
            json.object_value(|json| {
                json.moment("at", &time::document(entry.at_unix_nanos));
                json.string("what", &entry.what);
                json.string("message", &entry.message);
                json.string("journey", &entry.journey);
            });
        }
    });
    json.number("earlier", one.earlier);
    json.number("turns", one.turns);
}

fn pairs_of(json: &mut Json, name: &str, pairs: &[[String; 2]]) {
    json.array(name, |json| {
        for [key, value] in pairs {
            json.object_value(|json| {
                json.string("name", key);
                json.string("value", value);
            });
        }
    });
}
