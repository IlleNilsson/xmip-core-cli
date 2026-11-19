//! `xmip list`, `xmip show`, `xmip pause` and `xmip resume`: the rows of the
//! scope tree, and the two acts the boundary carries (ADR-0027 clause 5).
//! Every row is the one `ScopeItem` shape every surface reads, and which
//! rows a selection names, in what order, is `ScopeItem::selected`
//! (ADR-0052); only the rendering is here.

use std::io::Write;

use operate::surface::OperatorSurface;
use operate::surface::acted::{ScopeAction, ScopeOperation};
use operate::surface::english;
use operate::surface::scope_item::ScopeItem;
use operate::surface::scope_selection::ScopeSelection;

use crate::command::{json_selection, measure};
use crate::json::Json;
use crate::say;
use crate::text_table::left;

/// The direct children of what the argument selected. A wildcard lists the
/// children of every scope it named, in one list: every row names its own
/// scope, so nothing is lost by running them together.
pub fn list(
    surface: &dyn OperatorSurface,
    chosen: &ScopeSelection,
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    if !chosen.patterned {
        return list_scope(surface, &chosen.scopes[0], json, output, error);
    }

    if json {
        let document = json_selection::of(
            surface,
            chosen,
            &chosen.scopes,
            |json, scope| {
                json.string("scope", scope);
                json.array("items", |json| {
                    for item in surface.children(scope) {
                        json.object_value(|json| write_item(json, &item));
                    }
                });
            },
            |_| {},
        );
        say(output, &document);
        return 0;
    }

    let mut rows = 0;
    for item in chosen
        .scopes
        .iter()
        .flat_map(|scope| surface.children(scope))
    {
        write_row(output, &item, item.troubled());
        rows += 1;
    }

    if rows == 0 {
        say(
            error,
            &english::nothing_beneath(&chosen.argument, &surface.source()),
        );
        return 1;
    }
    0
}

/// One row per scope the argument selected that exists, worst first where
/// a wildcard named several (`ScopeItem::selected`). None at all is a
/// complaint on stderr and exit 1.
pub fn show(
    surface: &dyn OperatorSurface,
    chosen: &ScopeSelection,
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    let rows = ScopeItem::selected(surface, chosen);

    let Some(first) = rows.first() else {
        say(
            error,
            &english::nothing_at(&chosen.argument, &surface.source()),
        );
        return 1;
    };

    if json {
        let document = if chosen.patterned {
            json_selection::of(surface, chosen, &rows, write_item, |_| {})
        } else {
            Json::document(|json| write_item(json, first))
        };
        say(output, &document);
        return 0;
    }

    for item in &rows {
        write_row(output, item, true);
    }
    0
}

/// One scope as a row, with its evidence beneath.
pub fn show_scope(
    surface: &dyn OperatorSurface,
    scope: &str,
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    show(
        surface,
        &ScopeSelection::exactly(scope),
        json,
        output,
        error,
    )
}

/// Pause or resume every scope the argument selected, as `who`. A wildcard
/// adds no reach: one act already reaches everything beneath the scope it
/// names (ADR-0027), so a pattern names several subtrees rather than
/// opening a wider one. The exit is non-zero unless every one was applied.
pub fn apply(
    surface: &dyn OperatorSurface,
    chosen: &ScopeSelection,
    (action, who): (ScopeAction, &str),
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    if !chosen.patterned {
        return apply_scope(
            surface,
            &chosen.scopes[0],
            (action, who),
            json,
            output,
            error,
        );
    }

    let done: Vec<ScopeOperation> = chosen
        .scopes
        .iter()
        .map(|scope| surface.control(scope, action, who))
        .collect();

    if json {
        let document = json_selection::of(
            surface,
            chosen,
            &done,
            |json, operation| {
                json.string("scope", &operation.scope);
                json.boolean("applied", operation.applied);
                json.string("result", &operation.result);
            },
            |json| json.string("action", word(action)),
        );
        say(output, &document);
    } else {
        for operation in &done {
            let said: &mut dyn Write = if operation.applied { output } else { error };
            say(said, &operation.result);
        }
    }

    i32::from(!done.iter().all(|operation| operation.applied))
}

/// The direct children of one scope, one row each.
pub fn list_scope(
    surface: &dyn OperatorSurface,
    scope: &str,
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    let children = surface.children(scope);

    if children.is_empty() {
        say(error, &english::nothing_beneath(scope, &surface.source()));
        return 1;
    }

    if json {
        let document = Json::document(|json| {
            json.string("scope", scope);
            json.string("source", &surface.source());
            json.array("items", |json| {
                for item in &children {
                    json.object_value(|json| write_item(json, item));
                }
            });
        });
        say(output, &document);
        return 0;
    }

    for item in &children {
        write_row(output, item, item.troubled());
    }
    0
}

/// Pause or resume one scope and say what the runtime said: exit 1 where it
/// was not applied.
pub fn apply_scope(
    surface: &dyn OperatorSurface,
    scope: &str,
    (action, who): (ScopeAction, &str),
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    let operation = surface.control(scope, action, who);

    if json {
        let document = Json::document(|json| {
            json.string("scope", scope);
            json.string("action", word(action));
            json.boolean("applied", operation.applied);
            json.string("result", &operation.result);
        });
        say(output, &document);
    } else if operation.applied {
        say(output, &operation.result);
    } else {
        say(error, &operation.result);
    }

    i32::from(!operation.applied)
}

/// A pattern that named nothing, for a program: `--json` promises
/// structure, and the refusal is an answer like any other, on stderr with
/// the same non-zero exit the words have.
#[must_use]
pub fn unmatched(pattern: &str, refusal: &str) -> String {
    Json::document(|json| {
        json.string("pattern", pattern);
        json.number("matched", 0);
        json.string("refused", refusal);
    })
}

// One row: the mood, the scope, the figures; on a node's row what the node
// is for, a role a line; from and to; and, where asked, why — for a
// container the leaf that explains it and its evidence, the next scope to
// drill to, and for a leaf its own evidence (ADR-0052 clause 2).
fn write_row(output: &mut dyn Write, item: &ScopeItem, why: bool) {
    let mood = english::mood_or_nothing(item.health);
    say(
        output,
        &format!(
            "{}  {}  {}",
            left(mood, 9),
            item.scope,
            english::figures(&item.figures)
        ),
    );

    // What the node is for, by the roles it declares (ADR-0056, amendment
    // 2026-11-13).
    for line in item.purpose.split('\n').filter(|line| !line.is_empty()) {
        say(output, &format!("  {line}"));
    }

    // From and to, as the runtime says them (ADR-0052, amendment 2026-11-11).
    if let Some(path) = item.from_to.as_ref().filter(|path| !path.hops.is_empty()) {
        say(output, &format!("  {}", path.line));
    }

    if !why || item.evidence.is_empty() {
        return;
    }

    match (&item.worst, item.is_container) {
        (Some(worst), true) => say(output, &format!("  worst {worst}: {}", item.evidence)),
        _ => say(output, &format!("  {}", item.evidence)),
    }
}

fn write_item(json: &mut Json, item: &ScopeItem) {
    json.string("name", &item.name);
    json.string("scope", &item.scope);
    json.boolean("container", item.is_container);
    json.string_or_null("health", item.health.map(english::mood));
    json.number_or_null("severity", item.severity);
    json.string("evidence", &item.evidence);
    json.string_or_null("worst", item.worst.as_deref());
    json.string("from", item.came_from());
    json.string("to", item.went_to());
    // A node's row says what the node is for; no other row has a purpose.
    if !item.purpose.is_empty() {
        json.string("purpose", &item.purpose);
    }
    measure::write(json, &item.figures);
}

// The act as the word a document names it by: pause, resume.
fn word(action: ScopeAction) -> &'static str {
    match action {
        ScopeAction::Pause => "pause",
        ScopeAction::Resume => "resume",
    }
}
