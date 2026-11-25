//! `xmip message [id]`: the Messages that failed, Port by Port — how many
//! wait, a page of them, each its Journey, its place and why — or, with a
//! Journey named, Retry or Dismiss on it (runtime-model.md section 13;
//! ADR-0013). A list says plainly which of three it is: listed, not listed
//! — whether any wait is not known — or asked and not answered.

use observe::Act;
use operate::surface::OperatorSurface;
use operate::surface::failed_message_list::FailedMessageList;
use operate::surface::scope_tree;

use crate::command::noun;
use crate::json::Json;
use crate::run::Writers;

/// Take `act` on the Message of `journey`, failed at `location` — the node
/// that sends its Send Port or runs its Work Process, or the Port's scope
/// beneath it — as `who`.
pub fn over(
    surface: &dyn OperatorSurface,
    (location, journey): (Option<&str>, &str),
    (act, who): (Act, &str),
    json: bool,
    to: &mut Writers<'_>,
) -> i32 {
    let Some(location) =
        location.filter(|location| !location.is_empty() && !scope_tree::node(location).is_empty())
    else {
        crate::say(
            to.error,
            &format!(
                "REFUSED: to {} a Message, name the node that sends its Send Port or runs its \
                 Work Process with --location xmip:///<cluster>/node/<name>, or the Send Port's \
                 scope beneath it.",
                act.word()
            ),
        );
        return 2;
    };

    let (node, acted) = surface.act_message(location, journey, act, who);

    if json {
        noun::document(
            &acted,
            act,
            |json| {
                json.string("node", &node);
                json.string("journey", journey);
            },
            to.output,
        )
    } else {
        noun::said(&acted, to)
    }
}

/// List the Messages that failed at or beneath `location` — the surface's
/// root where none is named — each Send Port's page from `from`, at most
/// `most` of each.
pub fn list(
    surface: &dyn OperatorSurface,
    location: Option<&str>,
    (from, most): (u64, u32),
    json: bool,
    to: &mut Writers<'_>,
) -> i32 {
    let scope = location
        .filter(|location| !location.is_empty())
        .map_or_else(|| surface.root(), str::to_string);
    let failed = surface.failed_messages(&scope, from, most);

    if json {
        crate::say(to.output, &document(&failed));
        return i32::from(!failed.listed);
    }
    if !failed.listed {
        crate::say(to.error, &failed.unlisted_said(&scope, &surface.source()));
        return 1;
    }
    if failed.ports.is_empty() {
        crate::say(
            to.output,
            &format!("No Message that failed waits at or beneath {scope}."),
        );
        return 0;
    }

    for port in &failed.ports {
        crate::say(
            to.output,
            &format!(
                "{} Send Port {}: {} failed in its queue",
                port.node, port.send_port, port.count
            ),
        );
        if port.blocked {
            crate::say(
                to.output,
                "  BLOCKED: every Message of its key waits behind the first that failed until \
                 it is retried or dismissed",
            );
        }
        for journey in &port.messages {
            crate::say(
                to.output,
                &format!(
                    "  {}  place {}  {}",
                    journey.journey, journey.sequence, journey.reason
                ),
            );
        }
        if let Some(next) = port.next {
            crate::say(to.output, &format!("  more: --offset {next}"));
        }
    }

    0
}

/// The list as one document: whether it is one, why not, and each Port's
/// page.
#[must_use]
pub fn document(failed: &FailedMessageList) -> String {
    Json::document(|json| {
        json.boolean("listed", failed.listed);
        json.string("failure", &failed.failure);
        json.array("failed_messages", |json| {
            for port in &failed.ports {
                json.object_value(|json| {
                    json.string("node", &port.node);
                    json.string("send_port", &port.send_port);
                    json.number("count", port.count);
                    json.boolean("blocked", port.blocked);
                    json.number_or_null("next", port.next);
                    json.array("messages", |json| {
                        for journey in &port.messages {
                            json.object_value(|json| {
                                json.string("journey", &journey.journey);
                                json.number("sequence", journey.sequence);
                                json.string("reason", &journey.reason);
                            });
                        }
                    });
                });
            }
        });
    })
}
