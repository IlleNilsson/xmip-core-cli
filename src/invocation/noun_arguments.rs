//! What the nouns a node publishes for an operator to act on share on the
//! line (`observe::Noun`): `event-subscriptions`, `subscriptions`,
//! `dead-messages` and `message`, a Work Process's `instances` and a node's
//! `secret`. The act — one of
//! `--pause`, `--resume`, `--remove`, `--replay`, `--retry`, `--dismiss`
//! and `--set` — read as the word the runtime names it by, which each noun
//! then takes or refuses; each noun's own selector, `--id`, `--name`,
//! `--message` or `--instance`; and which of audit's words each takes.

use observe::Act;

use crate::command::Command;

/// Every act option, as typed.
pub const ACTS: [&str; 7] = [
    "--pause",
    "--resume",
    "--remove",
    "--replay",
    "--retry",
    "--dismiss",
    "--set",
];

/// The query options every noun shares with `audit`.
pub const SHARED: [&str; 3] = ["--location", "--sort", "--order"];

/// What a failed Message's list takes of audit's options.
pub const MESSAGE_SHARED: [&str; 3] = ["--location", "--offset", "--limit"];

/// What `secret` takes of audit's options.
pub const SECRET_SHARED: [&str; 1] = ["--location"];

/// What `database` takes of audit's options.
pub const DATABASE_SHARED: [&str; 2] = ["--from", "--to"];

/// Each noun's own option.
pub const ID: &str = "--id";
pub const NAME: &str = "--name";
pub const MESSAGE: &str = "--message";
pub const INSTANCE: &str = "--instance";

/// What a noun's selectors hold.
#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct Selectors {
    pub id: Option<u64>,
    pub name: Option<String>,
    pub message: Option<String>,
    /// The Instance `instances` opens, by its identifier.
    pub instance: Option<String>,
}

/// Take a noun's own option at `index` into `selectors`: `None` where it is
/// none of them; else `Some`, with why the line cannot be obeyed where its
/// value is missing.
pub fn take_selector(
    args: &[String],
    index: &mut usize,
    selectors: &mut Selectors,
) -> Option<Result<(), String>> {
    let option = args[*index].as_str();
    let next = args.get(*index + 1);

    let taken = match option {
        ID => match next.and_then(|value| digits(value)) {
            Some(number) => {
                selectors.id = Some(number);
                Ok(())
            }
            None => Err("--id needs an Event subscription's number.".to_string()),
        },
        NAME | MESSAGE | INSTANCE => match next.filter(|value| !value.trim().is_empty()) {
            Some(value) if option == NAME => {
                selectors.name = Some(value.clone());
                Ok(())
            }
            Some(value) if option == INSTANCE => {
                selectors.instance = Some(value.clone());
                Ok(())
            }
            Some(value) => {
                selectors.message = Some(value.clone());
                Ok(())
            }
            None if option == NAME => Err("--name needs a Subscription's name.".to_string()),
            None if option == INSTANCE => {
                Err("--instance needs an Instance's identifier.".to_string())
            }
            None => Err("--message needs a Message's identifier.".to_string()),
        },
        _ => return None,
    };

    if taken.is_ok() {
        *index += 1;
    }
    Some(taken)
}

// A whole number with no sign, no space and no separator.
fn digits(text: &str) -> Option<u64> {
    (!text.is_empty() && text.bytes().all(|byte| byte.is_ascii_digit()))
        .then(|| text.parse().ok())
        .flatten()
}

/// Take the act option at `index`: `None` where it is no act; else `Some`,
/// its word in `act`, with why where a second act is named.
pub fn take_act(option: &str, act: &mut Option<String>) -> Option<Result<(), String>> {
    if !ACTS.contains(&option) {
        return None;
    }

    let named = &option[2..];

    if act.as_deref().is_some_and(|before| before != named) {
        return Some(Err(
            "--pause, --resume, --remove, --replay, --retry, --dismiss and --set \
                         are one act; name one."
                .to_string(),
        ));
    }

    *act = Some(named.to_string());
    Some(Ok(()))
}

/// The first of audit's options `command` does not take, said as the line's
/// problem.
#[must_use]
pub fn foreign(command: Command, audit_options: &[String]) -> Option<String> {
    let takes = |option: &str| match command {
        Command::Message => MESSAGE_SHARED.contains(&option),
        Command::Secret | Command::Instances => SECRET_SHARED.contains(&option),
        Command::Database => DATABASE_SHARED.contains(&option),
        Command::EventSubscriptions | Command::Subscriptions | Command::DeadMessages => {
            SHARED.contains(&option)
        }
        Command::Audit => true,
        _ => false,
    };

    audit_options
        .iter()
        .find(|option| !takes(option))
        .map(|option| format!("{option} only applies to 'audit'."))
}

/// The first of the nouns' options `command` does not take, said as the
/// line's problem; `certificate`'s are its own to judge.
#[must_use]
pub fn stray(command: Command, noun_options: &[String]) -> Option<String> {
    use crate::invocation::{certificate_arguments, report_arguments, secret_arguments};

    for option in noun_options
        .iter()
        .filter(|option| !certificate_arguments::OPTIONS.contains(&option.as_str()))
    {
        let option = option.as_str();
        let owner = if option == ID {
            (command != Command::EventSubscriptions).then_some("'event-subscriptions'")
        } else if option == NAME {
            (command != Command::Subscriptions).then_some("'subscriptions'")
        } else if option == MESSAGE {
            (command != Command::DeadMessages).then_some("'dead-messages'")
        } else if option == INSTANCE {
            (command != Command::Instances).then_some("'instances'")
        } else if secret_arguments::OPTIONS.contains(&option) {
            (command != Command::Secret).then_some("'secret'")
        } else if report_arguments::OPTIONS.contains(&option) {
            (command != Command::Report).then_some("'report'")
        } else {
            (!command.is_noun()).then_some(
                "'event-subscriptions', 'subscriptions', 'dead-messages', 'message' and 'instances'",
            )
        };

        if let Some(owner) = owner {
            return Some(format!("{option} only applies to {owner}."));
        }
    }

    None
}

/// Why `act` is no act of `command` where it is one noun's alone.
#[must_use]
pub fn misplaced(command: Command, act: Option<&str>) -> Option<String> {
    match act? {
        "replay" if command != Command::DeadMessages => {
            Some("--replay only applies to 'dead-messages'.".to_string())
        }
        act @ ("retry" | "dismiss") if command != Command::Message => {
            Some(format!("--{act} only applies to 'message'."))
        }
        "set" if command != Command::Secret => Some("--set only applies to 'secret'.".to_string()),
        _ => None,
    }
}

/// The act an Event subscription takes: pause, resume or remove.
#[must_use]
pub fn event_subscription_act(word: Option<&str>) -> Option<Act> {
    word.and_then(Act::named)
}

/// The act a Subscription takes, or the refusal of one it does not.
///
/// # Errors
///
/// `--remove`, which no operator takes on a Subscription.
pub fn subscription_act(word: Option<&str>) -> Result<Option<Act>, String> {
    match word {
        None => Ok(None),
        Some("pause") => Ok(Some(Act::Pause)),
        Some("resume") => Ok(Some(Act::Resume)),
        Some(other) => Err(format!("REFUSED: --{other}: {CONFIGURED}")),
    }
}

/// Where a Subscription is added and removed.
pub const CONFIGURED: &str = "A Subscription is added and removed in the TOML configuration of \
    the Xmip Application that draws it; an operator pauses and resumes it.";

/// The act a Message in a Dead Message Queue takes, or the refusal.
///
/// # Errors
///
/// Any act but Replay.
pub fn dead_message_act(word: Option<&str>) -> Result<Option<Act>, String> {
    match word {
        None => Ok(None),
        Some("replay") => Ok(Some(Act::Replay)),
        Some(other) => Err(format!(
            "REFUSED: --{other}: a Message in a Dead Message Queue is replayed, and --replay is \
             its one act."
        )),
    }
}

/// The act an Instance takes: none. A Message that failed it is retried
/// or dismissed as every failed Message is.
///
/// # Errors
///
/// Any act.
pub fn instance_act(word: Option<&str>) -> Result<Option<Act>, String> {
    match word {
        None => Ok(None),
        Some(other) => Err(format!(
            "REFUSED: --{other}: an Instance takes no act; retry or dismiss the Message that \
             failed it: 'message <journey> --retry --location <node>'."
        )),
    }
}

/// The act a failed Message takes, where its Journey is `named`.
///
/// # Errors
///
/// A Journey named with no act, an act with none named, or an act no
/// failed Message takes.
pub fn message_act(word: Option<&str>, named: bool) -> Result<Option<Act>, String> {
    match word {
        None if named => Err("'message <id>' needs an act: --retry or --dismiss.".to_string()),
        None => Ok(None),
        Some(word @ ("retry" | "dismiss")) if !named => Err(format!(
            "REFUSED: to {word} a Message, name its Journey: 'message <id> --{word}'."
        )),
        Some("retry") => Ok(Some(Act::Retry)),
        Some("dismiss") => Ok(Some(Act::Dismiss)),
        Some(other) => Err(format!(
            "REFUSED: --{other}: a Message that failed is retried or dismissed, and --retry and \
             --dismiss are its acts."
        )),
    }
}
