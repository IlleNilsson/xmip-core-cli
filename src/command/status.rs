//! `xmip status <code>`: what a status code means, in the header's name and
//! one line of English — `abi::status_meaning::StatusMeaning`, the answer
//! `ConvertFrom-XmipStatus` emits too; only the rendering is here. A code
//! the header does not define is said to be unknown and exits 1.

use std::io::Write;

use abi::status_meaning::StatusMeaning;

use crate::json::Json;
use crate::say;
use crate::text_table::right;

/// Explain one status code.
pub fn run(code: &str, json: bool, output: &mut dyn Write, error: &mut dyn Write) -> i32 {
    let Ok(value) = code.parse::<i32>() else {
        say(
            error,
            &format!("'{code}' is not a number. Status codes are integers, zero or negative."),
        );
        return 2;
    };

    let meaning = StatusMeaning::of(value);
    let known = i32::from(!meaning.known);

    if json {
        say(
            output,
            &Json::document(|json| {
                json.number("code", meaning.code);
                json.boolean("known", meaning.known);
                json.string("name", meaning.name);
                json.string("meaning", meaning.meaning);
                json.boolean("retryable", meaning.retryable);
                json.boolean("terminal", meaning.terminal);
            }),
        );
        return known;
    }

    say(
        output,
        &format!("{}  {}", right(&meaning.code.to_string(), 5), meaning.name),
    );
    say(output, &format!("       {}", meaning.meaning));

    if !meaning.known {
        return 1;
    }

    say(
        output,
        &format!(
            "       retryable: {}   terminal: {}",
            yes(meaning.retryable),
            yes(meaning.terminal)
        ),
    );
    0
}

const fn yes(value: bool) -> &'static str {
    if value { "yes" } else { "no" }
}
