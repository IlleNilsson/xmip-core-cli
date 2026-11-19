//! `--follow` with `health` or `measure`: the answer for the publication as
//! it is, then a new answer each time the surface says its publication may
//! have advanced — read again, the selection matched again, and said only
//! where it differs from the last one said — one JSON Lines record each,
//! until Ctrl+C, which is the normal end of a follow (ADR-0014 clause 10).

use std::io::Write;
use std::sync::atomic::AtomicBool;

use operate::surface::OperatorSurface;
use operate::surface::scope_selection::ScopeSelection;

/// Follow `chosen` on `surface`, each answer `document`'s, until `stop`
/// is set: exit 0.
pub fn run(
    surface: &dyn OperatorSurface,
    chosen: &ScopeSelection,
    document: &dyn Fn(&dyn OperatorSurface, &ScopeSelection) -> String,
    output: &mut dyn Write,
    stop: &AtomicBool,
) -> i32 {
    let mut last: Option<String> = None;

    surface.watch(stop, &mut |_| {
        // A pattern names the scopes that exist now; one that names none now
        // answers for none rather than ending the follow.
        let now =
            ScopeSelection::of(surface, &chosen.argument).unwrap_or_else(|_| ScopeSelection {
                scopes: Vec::new(),
                ..chosen.clone()
            });
        let answer = document(surface, &now);

        if last.as_ref() != Some(&answer) {
            crate::say(output, &answer);
            let _ = output.flush();
            last = Some(answer);
        }
    });

    0
}
