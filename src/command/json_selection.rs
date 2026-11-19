//! What a wildcard answered, as one document of one shape for every command
//! (ADR-0059 clause 7): the pattern, the source, how many scopes it named,
//! whatever the command says once, and an object per answer.

use operate::surface::OperatorSurface;
use operate::surface::scope_selection::ScopeSelection;

use crate::json::Json;

/// One document: `pattern`, `source`, `matched`, what `head` writes, and
/// `scopes`, an object `each` writes for every one of `answers`.
pub fn of<T>(
    surface: &dyn OperatorSurface,
    chosen: &ScopeSelection,
    answers: impl IntoIterator<Item = T>,
    each: impl Fn(&mut Json, T),
    head: impl FnOnce(&mut Json),
) -> String {
    Json::document(|json| {
        json.string("pattern", &chosen.argument);
        json.string("source", &surface.source());
        json.number(
            "matched",
            i128::try_from(chosen.scopes.len()).unwrap_or(i128::MAX),
        );
        head(json);
        json.array("scopes", |json| {
            for answer in answers {
                json.object_value(|json| each(json, answer));
            }
        });
    })
}
