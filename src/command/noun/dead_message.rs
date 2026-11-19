//! `xmip dead-messages [pattern]`: what each node's Dead Message Queue
//! keeps (ADR-0052, amendment 2026-10-01) — every accepted Message no
//! Subscription matched, with its receive context, its gates' verdicts, its
//! promoted properties and every Subscription's decline — one opened, and
//! Replay on the one an act names. Which entries and in what order is
//! `DeadMessageQuery`'s, the one every surface asks.

use observe::{Act, DeadMessage, Noun};
use operate::surface::OperatorSurface;
use operate::surface::dead_message_query::{self as query, DeadMessageQuery};
use operate::surface::scope_tree;

use crate::command::noun::{self, age, count};
use crate::invocation::NounAsk;
use crate::json::Json;
use crate::run::Writers;
use crate::text_table;
use crate::time;

/// The columns a person reads, in the order the query lists them.
const HEADINGS: [&str; 6] = [
    "received",
    "message",
    "cluster",
    "node",
    "receive-location",
    "declines",
];

/// The query the line states.
#[must_use]
pub fn query(asked: &NounAsk) -> DeadMessageQuery {
    let (pattern, location, sort, order) = noun::words(asked);

    DeadMessageQuery {
        pattern,
        location,
        message: asked.selectors.message.clone(),
        sort,
        order,
    }
}

/// List what `asked` selects over `surface`, open the one it names, or
/// Replay it, as `who`.
pub fn over(
    surface: &dyn OperatorSurface,
    asked: &DeadMessageQuery,
    act: Option<Act>,
    (who, json): (&str, bool),
    to: &mut Writers<'_>,
) -> i32 {
    let chosen = match asked.apply(&surface.dead_messages()) {
        Ok(chosen) => chosen,
        Err(refused) => return noun::refused(&refused, json, to.error),
    };

    match act {
        Some(act) => replayed(surface, asked, &chosen, (act, who), json, to),
        None => listed(surface, asked, &chosen, json, to),
    }
}

/// The entries as one document, each with its verdicts, its promoted
/// properties and its declines as `name` and `value`.
#[must_use]
pub fn document(source: &str, chosen: &[DeadMessage]) -> String {
    Json::document(|json| {
        json.string("source", source);
        json.number("matched", count(chosen.len()));
        json.array("dead_messages", |json| {
            for entry in chosen {
                json.object_value(|json| {
                    json.string("node", &entry.node);
                    json.string("message", &entry.message);
                    json.string("cluster", &query::cluster_of(entry));
                    json.number("sequence", entry.sequence);
                    json.string("location", &entry.location);
                    json.moment("received", &time::document(entry.received_unix_nanos));
                    pairs(json, "validation", &entry.validation);
                    pairs(json, "promoted", &entry.promoted);
                    pairs(json, "declines", &entry.declines);
                });
            }
        });
    })
}

fn pairs(json: &mut Json, name: &str, pairs: &[[String; 2]]) {
    json.array(name, |json| {
        for [key, value] in pairs {
            json.object_value(|json| {
                json.string("name", key);
                json.string("value", value);
            });
        }
    });
}

fn listed(
    surface: &dyn OperatorSurface,
    asked: &DeadMessageQuery,
    chosen: &[DeadMessage],
    json: bool,
    to: &mut Writers<'_>,
) -> i32 {
    let source = surface.source();

    if json {
        crate::say(to.output, &document(&source, chosen));
        return 0;
    }

    // One Message named and found: opened.
    if asked
        .message
        .as_deref()
        .is_some_and(|message| !message.is_empty())
        && let [one] = chosen
    {
        open(one, to);
        return 0;
    }

    crate::say(
        to.output,
        &format!(
            "{} Message(s) in the Dead Message Queue — {source}",
            chosen.len()
        ),
    );

    if !chosen.is_empty() {
        let mut rows = vec![HEADINGS.map(String::from).to_vec()];
        rows.extend(chosen.iter().map(row));
        text_table::write(to.output, "  ", &rows);
    }

    0
}

fn row(entry: &DeadMessage) -> Vec<String> {
    vec![
        age(entry.received_unix_nanos),
        entry.message.clone(),
        query::cluster_of(entry),
        query::node_name(entry),
        entry.location.clone(),
        entry.declines.len().to_string(),
    ]
}

// One entry, every pair it carries, under the heading of each kind.
fn open(entry: &DeadMessage, to: &mut Writers<'_>) {
    crate::say(
        to.output,
        &format!(
            "Message {} in the Dead Message Queue of {}",
            entry.message, entry.node
        ),
    );
    crate::say(
        to.output,
        &format!(
            "  received at {}, {}, place {}",
            entry.location,
            time::round_trip(entry.received_unix_nanos),
            entry.sequence
        ),
    );
    section(to, "gate verdicts", &entry.validation);
    section(to, "promoted properties", &entry.promoted);
    section(to, "declines", &entry.declines);
}

fn section(to: &mut Writers<'_>, heading: &str, pairs: &[[String; 2]]) {
    if pairs.is_empty() {
        crate::say(to.output, &format!("  {heading}: none"));
        return;
    }

    crate::say(to.output, &format!("  {heading}:"));
    let rows: Vec<Vec<String>> = pairs.iter().map(|pair| pair.to_vec()).collect();
    text_table::write(to.output, "    ", &rows);
}

// A Replay names one Message: a node, by --location, and the Message, by
// --message. Anything else is refused before the node is asked.
fn replayed(
    surface: &dyn OperatorSurface,
    asked: &DeadMessageQuery,
    chosen: &[DeadMessage],
    (act, who): (Act, &str),
    json: bool,
    to: &mut Writers<'_>,
) -> i32 {
    let message = asked
        .message
        .as_deref()
        .filter(|message| !message.is_empty());
    let location = asked
        .location
        .as_deref()
        .filter(|location| !location.is_empty());

    let (Some(message), Some(location)) = (message, location) else {
        return refused_unnamed(act, to);
    };
    if scope_tree::node(location).is_empty() {
        return refused_unnamed(act, to);
    }

    let mut on = chosen
        .iter()
        .filter(|entry| scope_tree::beneath(location, &entry.node));
    let (Some(one), None) = (on.next(), on.next()) else {
        crate::say(
            to.error,
            &format!("REFUSED: no Message {message} is in the Dead Message Queue of {location}."),
        );
        return 1;
    };

    let acted = surface.act(&one.node, Noun::DeadMessage, &one.message, act, who);

    if json {
        noun::document(
            &acted,
            act,
            |json| {
                json.string("node", &one.node);
                json.string("message", &one.message);
            },
            to.output,
        )
    } else {
        noun::said(&acted, to)
    }
}

fn refused_unnamed(act: Act, to: &mut Writers<'_>) -> i32 {
    crate::say(
        to.error,
        &format!(
            "REFUSED: to {} a Message, name its node with --location \
             xmip:///<cluster>/node/<name> and it with --message.",
            act.word()
        ),
    );
    2
}
