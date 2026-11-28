//! `xmip health <scope>`: every leaf at and beneath a scope, worst first,
//! with the rollup for the scope itself (ADR-0041). A scope that is Holding
//! says why on the spot — the worst leaf beneath it and that leaf's
//! evidence (ADR-0052 clause 2). The cluster is named as every surface names
//! it, its short name and what its tooltip carries (`cluster_named`;
//! ADR-0028, amendment 2026-11-28). Text for a person, one document with
//! `--json`, and with `--follow` one JSON Lines record each time the health
//! changes (ADR-0014 clause 10).

use std::io::Write;

use observe::{Health, HealthRecord};
use operate::surface::OperatorSurface;
use operate::surface::cluster_named::ClusterNamed;
use operate::surface::english;
use operate::surface::scope_selection::ScopeSelection;
use operate::surface::scope_tree;

use crate::command::{follow, json_selection};
use crate::invocation::Invocation;
use crate::json::Json;
use crate::run::Writers;
use crate::say;
use crate::text_table::{left, right};
use crate::time;

/// What the line asks: a follow, or one answer.
pub fn over(
    surface: &dyn OperatorSurface,
    chosen: &ScopeSelection,
    invocation: &Invocation,
    to: &mut Writers<'_>,
) -> i32 {
    if invocation.follow {
        follow::run(surface, chosen, &answer, to.output, to.stop)
    } else {
        selected(surface, chosen, invocation.json, to.output, to.error)
    }
}

/// Read once and render what the argument selected. One scope is what it
/// always was; a wildcard answers for every scope it named, one banner and
/// one list each, because a rollup over several scopes would be a rollup of
/// a scope that is not in the tree (ADR-0041).
pub fn selected(
    surface: &dyn OperatorSurface,
    chosen: &ScopeSelection,
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    if !chosen.patterned {
        return run(surface, &chosen.scopes[0], json, output, error);
    }

    if json {
        say(output, &documents(surface, chosen));
        return 0;
    }

    for scope in &chosen.scopes {
        let records = surface.health(scope);
        // A publication that advanced between the match and the read can
        // have lost a scope; what is gone is not said, and what is left is.
        if records.is_empty() {
            continue;
        }
        write_text(surface, scope, &records, output);
        say(output, "");
    }

    0
}

/// Read once and render one scope.
pub fn run(
    surface: &dyn OperatorSurface,
    scope: &str,
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    let records = surface.health(scope);

    if records.is_empty() {
        say(error, &english::nothing_at(scope, &surface.source()));
        return 1;
    }

    if json {
        say(output, &document(surface, scope, &records));
    } else {
        write_text(surface, scope, &records, output);
    }

    0
}

/// What the argument selected, as one document: a single scope's, or for a
/// wildcard one object per scope.
pub fn answer(surface: &dyn OperatorSurface, chosen: &ScopeSelection) -> String {
    if chosen.patterned {
        documents(surface, chosen)
    } else {
        let scope = chosen.scopes.first().map_or("", String::as_str);
        document(surface, scope, &surface.health(scope))
    }
}

/// What a wildcard answered, as one document with the same object per scope
/// a single read emits.
pub fn documents(surface: &dyn OperatorSurface, chosen: &ScopeSelection) -> String {
    json_selection::of(
        surface,
        chosen,
        &chosen.scopes,
        |json, scope| body(json, surface, scope, &surface.health(scope)),
        |_| {},
    )
}

/// The scope, its rollup, the worst leaf and every leaf, as one document.
pub fn document(surface: &dyn OperatorSurface, scope: &str, records: &[HealthRecord]) -> String {
    Json::document(|json| body(json, surface, scope, records))
}

fn body(json: &mut Json, surface: &dyn OperatorSurface, scope: &str, records: &[HealthRecord]) {
    json.string("scope", scope);
    json.string("source", &surface.source());
    json.string("state", english::rollup(records));

    // The cluster as every surface shows it, where the surface names one.
    let named = ClusterNamed::of(surface);
    if !named.shown().is_empty() {
        json.object("cluster", |json| {
            json.string("shown", named.shown());
            json.string("title", &named.title());
        });
    }

    // What the run was started with, where its publisher says; a surface
    // that says nothing of a run writes no key at all.
    let run = surface.run();
    if run.said() {
        json.string("run", &run.line());
    }

    if let Some(worst) = scope_tree::worst(records) {
        json.object("worst", |json| record(json, surface, &worst));
    }

    json.array("records", |json| {
        for each in records {
            json.object_value(|json| record(json, surface, each));
        }
    });
}

fn record(json: &mut Json, surface: &dyn OperatorSurface, record: &HealthRecord) {
    let path = surface.ends(&record.scope);

    json.string("scope", &record.scope);
    json.string("state", english::mood(record.health));
    json.number("severity", record.severity);
    json.string("evidence", &record.evidence);
    json.string("from", &path.from);
    json.string("to", &path.to);
    json.moment("observed", &time::document(record.observed_unix_nanos));
}

fn write_text(
    surface: &dyn OperatorSurface,
    scope: &str,
    records: &[HealthRecord],
    output: &mut dyn Write,
) {
    let rollup = scope_tree::rollup(records).unwrap_or(Health::Fine);
    let Some(worst) = scope_tree::worst(records) else {
        return;
    };
    let gap = " ".repeat(14);

    // The banner: the scope, its rollup, and — when Holding — why, on the
    // spot. The word alone is not a state an operator can act on.
    say(
        output,
        &format!(
            "{} {}  {scope}",
            left(english::mood(rollup), 9),
            right("", 3)
        ),
    );
    if rollup == Health::Holding {
        say(
            output,
            &format!("{gap} {} at {}", english::mood(worst.health), worst.scope),
        );
        if !worst.evidence.is_empty() {
            say(output, &format!("{gap} {}", worst.evidence));
        }
    }
    say(output, &format!("{gap} source {}", surface.source()));
    let named = ClusterNamed::of(surface);
    if !named.shown().is_empty() {
        // What a tooltip carries, said beside it where the publisher names
        // the cluster for a person.
        let shown = if named.naming.said() {
            format!("{} ({})", named.shown(), named.title())
        } else {
            named.shown().to_string()
        };
        say(output, &format!("{gap} cluster {shown}"));
    }
    let run = surface.run();
    if run.said() {
        say(output, &format!("{gap} run {}", run.line()));
    }
    say(output, "");

    for each in records {
        let mood = english::mood(each.health);
        say(
            output,
            &format!(
                "{} {}  {}",
                left(mood, 9),
                right(&each.severity.to_string(), 3),
                each.scope
            ),
        );
        if !each.evidence.is_empty() {
            say(output, &format!("{gap} {}", each.evidence));
        }
        // From and to on every observation (ADR-0052, amendment 2026-11-11).
        let path = surface.ends(&each.scope);
        if !path.hops.is_empty() {
            say(output, &format!("{gap} {}", path.line));
        }
        say(
            output,
            &format!(
                "{gap} observed {}",
                time::round_trip(each.observed_unix_nanos)
            ),
        );
    }
}
