//! `xmip subscriptions [pattern]`: the Subscriptions the cluster's nodes
//! route by (ADR-0013, amendment 2026-09-30; ADR-0052) — each one's name,
//! the cluster, the node, its filter, where it leads, the Send Ports it
//! reaches, its state, what it picked up, what it holds and since — and
//! pause or resume on the one an act names. Which ones and in what order is
//! `SubscriptionQuery`'s, the one every surface asks.

use observe::{Act, Noun, Subscription};
use operate::surface::OperatorSurface;
use operate::surface::english;
use operate::surface::scope_tree;
use operate::surface::subscription_query::{self as query, SubscriptionQuery};

use crate::command::noun::{self, age, count};
use crate::invocation::NounAsk;
use crate::json::Json;
use crate::run::Writers;
use crate::text_table;
use crate::time;

/// The columns a person reads, in the order the query lists them.
const HEADINGS: [&str; 10] = [
    "subscription",
    "cluster",
    "node",
    "filter",
    "destination",
    "send-ports",
    "state",
    "picked-up",
    "held",
    "since",
];

/// The query the line states.
#[must_use]
pub fn query(asked: &NounAsk) -> SubscriptionQuery {
    let (pattern, location, sort, order) = noun::words(asked);

    SubscriptionQuery {
        pattern,
        location,
        name: asked.selectors.name.clone(),
        sort,
        order,
    }
}

/// List what `asked` selects over `surface`, or take `act` on the one it
/// names, as `who`.
pub fn over(
    surface: &dyn OperatorSurface,
    asked: &SubscriptionQuery,
    act: Option<Act>,
    (who, json): (&str, bool),
    to: &mut Writers<'_>,
) -> i32 {
    let chosen = match asked.apply(&surface.subscriptions()) {
        Ok(chosen) => chosen,
        Err(refused) => return noun::refused(&refused, json, to.error),
    };

    match act {
        Some(act) => taken(surface, asked, &chosen, (act, who), json, to),
        None => listed(surface, &chosen, json, to),
    }
}

/// The Subscriptions as one document, for a program.
#[must_use]
pub fn document(source: &str, chosen: &[Subscription]) -> String {
    Json::document(|json| {
        json.string("source", source);
        json.number("matched", count(chosen.len()));
        json.array("subscriptions", |json| {
            for entry in chosen {
                json.object_value(|json| {
                    json.string("node", &entry.node);
                    json.string("name", &entry.name);
                    json.string("cluster", &query::cluster_of(entry));
                    json.string("application", &entry.application);
                    json.string("filter", &entry.filter);
                    json.string("destination", &entry.destination);
                    json.array("send_ports", |json| {
                        for port in &entry.send_ports {
                            json.string_value(port);
                        }
                    });
                    json.string("file", &entry.file);
                    json.string("configuration", &entry.configuration);
                    json.string("state", entry.state.word());
                    json.boolean("paused", query::paused(entry));
                    json.string("by", &entry.by);
                    json.number("picked_up", entry.picked_up);
                    json.number("held", entry.held);
                    json.moment("since", &time::document(entry.since_unix_nanos));
                });
            }
        });
    })
}

fn listed(
    surface: &dyn OperatorSurface,
    chosen: &[Subscription],
    json: bool,
    to: &mut Writers<'_>,
) -> i32 {
    let source = surface.source();

    if json {
        crate::say(to.output, &document(&source, chosen));
        return 0;
    }

    let paused = chosen.iter().filter(|entry| query::paused(entry)).count();
    let held = chosen
        .iter()
        .fold(0u64, |sum, entry| sum.wrapping_add(entry.held));
    crate::say(
        to.output,
        &format!(
            "{} Subscription(s), {paused} paused, {} held — {source}",
            chosen.len(),
            english::figure(Some(held))
        ),
    );

    if !chosen.is_empty() {
        let mut rows = vec![HEADINGS.map(String::from).to_vec()];
        rows.extend(chosen.iter().map(row));
        text_table::write(to.output, "  ", &rows);
    }

    0
}

fn row(entry: &Subscription) -> Vec<String> {
    vec![
        entry.name.clone(),
        query::cluster_of(entry),
        query::node_name(entry),
        entry.filter.clone(),
        entry.destination.clone(),
        entry.send_ports.join(", "),
        entry.state.word().to_string(),
        english::figure(Some(entry.picked_up)),
        english::figure(Some(entry.held)),
        age(entry.since_unix_nanos),
    ]
}

// An act names one Subscription: a node, by --location, and its name, by
// --name. Anything else is refused before the node is asked.
fn taken(
    surface: &dyn OperatorSurface,
    asked: &SubscriptionQuery,
    chosen: &[Subscription],
    (act, who): (Act, &str),
    json: bool,
    to: &mut Writers<'_>,
) -> i32 {
    let name = asked.name.as_deref().filter(|name| !name.is_empty());
    let location = asked
        .location
        .as_deref()
        .filter(|location| !location.is_empty());

    let (Some(name), Some(location)) = (name, location) else {
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
                "REFUSED: no Subscription '{name}' is listed on {location}; no Xmip Application \
                 draws it there."
            ),
        );
        return 1;
    };

    let acted = surface.act(&one.node, Noun::Subscription, &one.name, act, who);

    if json {
        noun::document(
            &acted,
            act,
            |json| {
                json.string("node", &one.node);
                json.string("name", &one.name);
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
            "REFUSED: to {} a Subscription, name its node with --location \
             xmip:///<cluster>/node/<name> and it with --name.",
            act.word()
        ),
    );
    2
}
