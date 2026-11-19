//! `xmip event-subscriptions [pattern]`: the Event subscriptions the
//! cluster's nodes hold (ADR-0065, amendment 2026-09-29) — each one's
//! number on its node, the subscriber, the cluster, the node, the action it
//! subscribes to, its state, queued against capacity, delivered, missed and
//! since — the members a node there does not hear, read-only, and pause,
//! resume or remove on the one an act names. Which ones and in what order
//! is `EventSubscriptionQuery`'s, the one every surface asks.

use observe::{Act, EventSubscription, Noun, Unheard};
use operate::surface::OperatorSurface;
use operate::surface::english;
use operate::surface::event_subscription_query::{self as query, EventSubscriptionQuery};
use operate::surface::scope_tree;

use crate::command::noun::{self, age, count};
use crate::invocation::NounAsk;
use crate::json::Json;
use crate::run::Writers;
use crate::text_table;
use crate::time;

/// The columns a person reads, the number that names one on its node first.
const HEADINGS: [&str; 10] = [
    "id",
    "subscriber",
    "cluster",
    "node",
    "action",
    "state",
    "queued",
    "delivered",
    "missed",
    "since",
];

/// The query the line states.
#[must_use]
pub fn query(asked: &NounAsk) -> EventSubscriptionQuery {
    let (pattern, location, sort, order) = noun::words(asked);

    EventSubscriptionQuery {
        pattern,
        location,
        id: asked.selectors.id,
        sort,
        order,
    }
}

/// List what `asked` selects over `surface`, or take `act` on the one it
/// names, as `who`.
pub fn over(
    surface: &dyn OperatorSurface,
    asked: &EventSubscriptionQuery,
    act: Option<Act>,
    (who, json): (&str, bool),
    to: &mut Writers<'_>,
) -> i32 {
    let (listed, unheard) = surface.event_subscriptions();
    let chosen = match asked.apply(&listed) {
        Ok(chosen) => chosen,
        Err(refused) => return noun::refused(&refused, json, to.error),
    };
    let unheard = asked.unheard(&unheard);

    match act {
        Some(act) => taken(surface, asked, &chosen, (act, who), json, to),
        None => listed_out(surface, (&chosen, &unheard), json, to),
    }
}

/// The Event subscriptions and the members not heard, as one document.
#[must_use]
pub fn document(source: &str, chosen: &[EventSubscription], unheard: &[Unheard]) -> String {
    Json::document(|json| {
        json.string("source", source);
        json.number("matched", count(chosen.len()));
        json.array("event_subscriptions", |json| {
            for entry in chosen {
                json.object_value(|json| {
                    json.string("node", &entry.node);
                    json.number("id", entry.id);
                    json.string("subscriber", query::who(entry));
                    json.string("party", &entry.party);
                    json.string("cluster", &query::cluster_of(entry));
                    json.string("action", &entry.action);
                    json.string("scope", &entry.scope);
                    json.string("state", entry.state.word());
                    json.boolean("paused", query::paused(entry));
                    json.number("queued", entry.queued);
                    json.number("capacity", entry.capacity);
                    json.number("delivered", entry.delivered);
                    json.number("missed", entry.missed);
                    json.moment("since", &time::document(entry.since_unix_nanos));
                });
            }
        });
        json.array("unheard", |json| {
            for gone in unheard {
                json.object_value(|json| {
                    json.string("by", &gone.by);
                    json.string("node", &gone.node);
                    json.moment("since", &time::document(gone.since_unix_nanos));
                    json.string("why", &gone.why);
                    json.string("said", &gone.said());
                });
            }
        });
    })
}

fn listed_out(
    surface: &dyn OperatorSurface,
    (chosen, unheard): (&[EventSubscription], &[Unheard]),
    json: bool,
    to: &mut Writers<'_>,
) -> i32 {
    let source = surface.source();

    if json {
        crate::say(to.output, &document(&source, chosen, unheard));
        return 0;
    }

    let paused = chosen.iter().filter(|entry| query::paused(entry)).count();
    crate::say(
        to.output,
        &format!(
            "{} Event subscription(s), {paused} paused — {source}",
            chosen.len()
        ),
    );

    if !chosen.is_empty() {
        let mut rows = vec![HEADINGS.map(String::from).to_vec()];
        rows.extend(chosen.iter().map(row));
        text_table::write(to.output, "  ", &rows);
    }

    // Read-only: what the nodes here do not hear, so no Event is missing
    // silently; the links between nodes are never listed or acted on.
    for gone in unheard {
        crate::say(to.output, &format!("  {}", query::line(gone)));
    }

    0
}

fn row(entry: &EventSubscription) -> Vec<String> {
    vec![
        entry.id.to_string(),
        query::who(entry).to_string(),
        query::cluster_of(entry),
        query::node_name(entry),
        entry.action.clone(),
        entry.state.word().to_string(),
        format!(
            "{}/{}",
            english::figure(Some(entry.queued)),
            english::figure(Some(entry.capacity))
        ),
        english::figure(Some(entry.delivered)),
        english::figure(Some(entry.missed)),
        age(entry.since_unix_nanos),
    ]
}

// An act names one Event subscription: a node, by --location, and its
// number, by --id. Anything else is refused before the node is asked.
fn taken(
    surface: &dyn OperatorSurface,
    asked: &EventSubscriptionQuery,
    chosen: &[EventSubscription],
    (act, who): (Act, &str),
    json: bool,
    to: &mut Writers<'_>,
) -> i32 {
    let location = asked
        .location
        .as_deref()
        .filter(|location| !location.is_empty());

    let (Some(id), Some(location)) = (asked.id, location) else {
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
            &format!(
                "REFUSED: no Event subscription {id} is listed on {location}; it was removed, \
                 or never made."
            ),
        );
        return 1;
    };

    let acted = surface.act(
        &one.node,
        Noun::EventSubscription,
        &one.id.to_string(),
        act,
        who,
    );

    if json {
        noun::document(
            &acted,
            act,
            |json| {
                json.string("node", &one.node);
                json.number("id", one.id);
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
            "REFUSED: to {} an Event subscription, name its node with --location \
             xmip:///<cluster>/node/<name> and its number with --id.",
            act.word()
        ),
    );
    2
}
