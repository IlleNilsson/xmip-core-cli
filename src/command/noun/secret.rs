//! `xmip secret [name]`: the secrets a node keeps — each one's name, kind,
//! and when and by whom it was set, never a value — or, with `--set`, one
//! set through the node, its value read from `--file` or standard input
//! and never from the line (ADR-0019 and ADR-0063, amendments 2026-10-29).

use operate::surface::OperatorSurface;
use operate::surface::acted::SecretRecord;

use crate::invocation::secret_arguments::SecretAsk;
use crate::json::Json;
use crate::run::Writers;
use crate::text_table;
use crate::time;

/// The columns a person reads.
const HEADINGS: [&str; 4] = ["name", "kind", "set", "set by"];

/// List the secrets the node at `node` keeps.
pub fn list(surface: &dyn OperatorSurface, node: &str, json: bool, to: &mut Writers<'_>) -> i32 {
    let secrets = match surface.secrets(node) {
        Ok(secrets) => secrets,
        Err(refused) => {
            let said = if json {
                Json::document(|document| document.string("refused", &refused))
            } else {
                refused
            };
            crate::say(to.error, &said);
            return 1;
        }
    };
    let source = surface.source();

    if json {
        crate::say(to.output, &document(&source, &secrets));
        return 0;
    }

    crate::say(
        to.output,
        &format!("{} secret(s) — {source}", secrets.len()),
    );
    if !secrets.is_empty() {
        let mut rows = vec![HEADINGS.map(String::from).to_vec()];
        rows.extend(secrets.iter().map(row));
        text_table::write(to.output, "  ", &rows);
    }

    0
}

/// Set the secret `asked` names on the node at `node`, as `who`, its value
/// read from its file or the writers' input.
pub fn set(
    surface: &dyn OperatorSurface,
    node: &str,
    asked: &SecretAsk,
    (who, json): (&str, bool),
    to: &mut Writers<'_>,
) -> i32 {
    let value = match asked.value(to.input) {
        Ok(value) => value,
        Err(unread) => {
            crate::say(
                to.error,
                &format!("FAILED: the value could not be read: {unread}"),
            );
            return 1;
        }
    };

    let kind = asked.kind.as_deref().unwrap_or_default();
    let done = surface.set_secret(node, &asked.name, (kind, &value), who);
    let said = if json {
        Json::document(|document| {
            document.string("node", &done.node);
            document.string("name", &done.name);
            document.boolean("applied", done.applied);
            document.string("result", &done.result);
        })
    } else if done.applied {
        format!("OK. {}", done.result)
    } else {
        done.result.clone()
    };

    if done.applied {
        crate::say(to.output, &said);
        0
    } else {
        crate::say(to.error, &said);
        1
    }
}

/// The secrets as one document, never a value.
#[must_use]
pub fn document(source: &str, secrets: &[SecretRecord]) -> String {
    Json::document(|json| {
        json.string("source", source);
        json.array("secrets", |json| {
            for secret in secrets {
                json.object_value(|json| {
                    json.string("name", &secret.name);
                    json.string("kind", &secret.kind);
                    json.moment("set", &time::document(set_at(secret)));
                    json.string("set_by", &secret.set_by);
                });
            }
        });
    })
}

fn row(secret: &SecretRecord) -> Vec<String> {
    vec![
        secret.name.clone(),
        secret.kind.clone(),
        format!("{}Z", time::to_the_second(set_at(secret))),
        secret.set_by.clone(),
    ]
}

fn set_at(secret: &SecretRecord) -> i64 {
    i64::try_from(secret.set_unix_nanos).unwrap_or(i64::MAX)
}
