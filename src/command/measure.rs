//! `xmip measure [scope]`: the six figures at a scope, the cluster when
//! none is named — Streams, Messages, Journeys, bytes, Retrying, Failed, in
//! the order every surface says them (ADR-0027 clause 5). A figure the
//! runtime has not published is a dash, never a zero. With `--follow`, one
//! JSON Lines record each time the figures change.
//!
//! Totals here, a rate in the prompt, and that is deliberate (ADR-0052,
//! amendment 2026-09-20): an invocation is one sample and has no interval
//! to divide by; with `--follow` there are samples, and each record carries
//! the figures and the scope, so a program computes the rate exactly from
//! two documents.

use std::io::Write;

use operate::surface::OperatorSurface;
use operate::surface::english;
use operate::surface::figures::{Figures, KINDS};
use operate::surface::scope_selection::ScopeSelection;

use crate::command::{follow, json_selection};
use crate::invocation::Invocation;
use crate::json::Json;
use crate::run::Writers;
use crate::say;
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

/// Read once and render what the argument selected. A wildcard says one
/// line per scope it named, each line naming its own scope; the figures are
/// not added together, because a sum over several scopes is a figure at a
/// scope that does not exist.
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

    let mut measured = false;
    for scope in &chosen.scopes {
        let figures = surface.figures(scope);
        measured |= figures.has_values();
        say(output, &format!("{scope}  {}", english::figures(&figures)));
    }

    if !measured {
        say(
            error,
            &english::nothing_measured(&chosen.argument, &surface.source()),
        );
    }

    i32::from(!measured)
}

/// Read once and render one scope.
pub fn run(
    surface: &dyn OperatorSurface,
    scope: &str,
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    let figures = surface.figures(scope);

    if !figures.has_values() {
        say(error, &english::nothing_measured(scope, &surface.source()));
        return 1;
    }

    let said = if json {
        document(surface, &figures)
    } else {
        english::figures(&figures)
    };
    say(output, &said);
    0
}

/// What the argument selected, as one document.
pub fn answer(surface: &dyn OperatorSurface, chosen: &ScopeSelection) -> String {
    if chosen.patterned {
        documents(surface, chosen)
    } else {
        let scope = chosen.scopes.first().map_or("", String::as_str);
        document(surface, &surface.figures(scope))
    }
}

/// What a wildcard measured, as one document: the six figures at each scope
/// it named.
pub fn documents(surface: &dyn OperatorSurface, chosen: &ScopeSelection) -> String {
    json_selection::of(
        surface,
        chosen,
        &chosen.scopes,
        |json, scope| {
            json.string("scope", scope);
            write(json, &surface.figures(scope));
        },
        |_| {},
    )
}

/// The six figures as one document, for a program.
pub fn document(surface: &dyn OperatorSurface, figures: &Figures) -> String {
    Json::document(|json| {
        json.string("scope", &figures.scope);
        json.string("source", &surface.source());
        write(json, figures);
    })
}

/// The six figures and when they were observed, into an open object, each
/// named by its kind's word (`observe::Counted::word`).
pub fn write(json: &mut Json, figures: &Figures) {
    for counted in KINDS {
        json.number_or_null(english::kind(counted), figures.of(counted));
    }

    match figures.observed {
        Some(observed) => json.moment("observed", &time::document(observed)),
        None => json.null("observed"),
    }
}
