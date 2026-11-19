//! The nouns a node publishes for an operator to act on, and a node's
//! secrets: `xmip event-subscriptions` (ADR-0065, amendment 2026-09-29),
//! `subscriptions` (ADR-0013, amendment 2026-09-30), `dead-messages`
//! (ADR-0052, amendment 2026-10-01), `message` (runtime-model.md section
//! 13) and `secret` (ADR-0019, amendment 2026-10-29). Each lists what the
//! line's query selects, or takes the one act it names on the one it names;
//! what they share — a refused query, an act's sentence, an age — is said
//! here once.

pub mod dead_message;
pub mod event_subscription;
pub mod message;
pub mod secret;
pub mod subscription;

use std::io::Write;

use operate::surface::OperatorSurface;
use operate::surface::acted::{ActOutcome, Acted};
use xcore::Clock;

use crate::command::Command;
use crate::invocation::{Invocation, NounAsk};
use crate::json::Json;
use crate::run::Writers;

/// Answer a noun's line over `surface`, as `who` where it acts.
pub fn answer(
    surface: &dyn OperatorSurface,
    invocation: &Invocation,
    who: &str,
    to: &mut Writers<'_>,
) -> i32 {
    let json = invocation.json;
    let asked = invocation.noun.clone().unwrap_or_default();
    let location = invocation.location.as_deref();

    match invocation.command {
        Command::EventSubscriptions => event_subscription::over(
            surface,
            &event_subscription::query(&asked),
            invocation.act,
            (who, json),
            to,
        ),
        Command::Subscriptions => subscription::over(
            surface,
            &subscription::query(&asked),
            invocation.act,
            (who, json),
            to,
        ),
        Command::DeadMessages => dead_message::over(
            surface,
            &dead_message::query(&asked),
            invocation.act,
            (who, json),
            to,
        ),
        Command::Message => match invocation.act {
            Some(act) => message::over(
                surface,
                (location, &invocation.argument),
                (act, who),
                json,
                to,
            ),
            None => message::list(
                surface,
                location,
                (invocation.from, invocation.most),
                json,
                to,
            ),
        },
        _ => match invocation.secret.as_ref().filter(|asked| asked.set) {
            Some(asked) => secret::set(
                surface,
                location.unwrap_or_default(),
                asked,
                (who, json),
                to,
            ),
            None => secret::list(surface, location.unwrap_or_default(), json, to),
        },
    }
}

/// A query the list does not take, said on `error` — as a document under
/// `--json` — and exit 2.
pub(crate) fn refused(said: &str, json: bool, error: &mut dyn Write) -> i32 {
    let said = if json {
        Json::document(|document| document.string("refused", said))
    } else {
        said.to_string()
    };
    crate::say(error, &said);
    2
}

/// What came of an act, as text: `OK.` or `ASKED.` on `output`,
/// `REFUSED:` on `error`; exit 1 when it was refused.
pub(crate) fn said(acted: &Acted, to: &mut Writers<'_>) -> i32 {
    let line = acted.outcome.said(&acted.said);

    if acted.outcome == ActOutcome::Refused {
        crate::say(to.error, &line);
        1
    } else {
        crate::say(to.output, &line);
        0
    }
}

/// What came of an act, as a document on `output`, opening with what names
/// the one it was taken on; exit 1 when it was refused.
pub(crate) fn document(
    acted: &Acted,
    act: observe::Act,
    named: impl FnOnce(&mut Json),
    output: &mut dyn Write,
) -> i32 {
    crate::say(
        output,
        &Json::document(|document| {
            named(document);
            document.string("act", act.word());
            document.boolean("applied", acted.applied());
            document.string("outcome", acted.outcome.word());
            document.string("result", &acted.said);
        }),
    );
    i32::from(acted.outcome == ActOutcome::Refused)
}

/// How long ago `observed` was, now.
pub(crate) fn age(observed: i64) -> String {
    let now = i64::try_from(xcore::SystemClock.unix_timestamp_nanos()).unwrap_or(i64::MAX);
    operate::surface::english::age(observed, now)
}

/// A count as a document writes it.
pub(crate) fn count(value: usize) -> u64 {
    u64::try_from(value).unwrap_or(u64::MAX)
}

/// The query words every noun shares, from the line.
pub(crate) fn words(
    asked: &NounAsk,
) -> (
    Option<String>,
    Option<String>,
    Option<String>,
    Option<String>,
) {
    (
        asked.pattern.clone(),
        asked.location.clone(),
        asked.sort.clone(),
        asked.order.clone(),
    )
}
