//! What every certificate act says: its record as a document, or its
//! sentence for a person and what follows it; a refusal on stderr, as a
//! document where one was asked for; and a moment as an act says it.

use std::io::Write;

use codec::civil::CivilTime;
use serde_json::Value;

use crate::json::Json;
use crate::say;

/// Where an act's answer goes, and in which form.
pub struct Answer<'a> {
    pub json: bool,
    pub output: &'a mut dyn Write,
    pub error: &'a mut dyn Write,
}

impl Answer<'_> {
    /// Said: `refused` on stderr where it is not empty, exit 1; else the
    /// document `fields` writes, or `sentence` and what `more` writes after
    /// it, exit 0.
    pub fn said(
        &mut self,
        refused: Option<&str>,
        fields: impl FnOnce(&mut Json),
        sentence: &str,
        more: impl FnOnce(&mut dyn Write),
    ) -> i32 {
        if let Some(refused) = refused {
            let said = if self.json {
                Json::document(|json| json.string("refused", refused))
            } else {
                refused.to_string()
            };
            say(self.error, &said);
            return 1;
        }

        if self.json {
            say(self.output, &Json::document(fields));
        } else {
            say(self.output, sentence);
            more(self.output);
        }
        0
    }
}

/// A text member of an answer; empty where it is none.
pub fn text<'v>(answer: &'v Value, name: &str) -> &'v str {
    answer[name].as_str().unwrap_or_default()
}

/// A list of texts of an answer; empty where it is none.
pub fn texts(answer: &Value, name: &str) -> Vec<String> {
    answer[name]
        .as_array()
        .map(|all| {
            all.iter()
                .map(|one| one.as_str().unwrap_or_default().to_string())
                .collect()
        })
        .unwrap_or_default()
}

/// A moment of an answer, Unix seconds.
pub fn moment(answer: &Value, name: &str) -> i64 {
    answer[name].as_i64().unwrap_or(0)
}

/// A moment, Unix seconds, as a document writes it.
pub fn document_moment(seconds: i64) -> String {
    crate::time::document(seconds.saturating_mul(1_000_000_000))
}

/// A moment, Unix seconds, as a person reads it: `2026-10-22 12:00 UTC`.
pub fn day(seconds: i64) -> String {
    let at = CivilTime::from_unix(seconds);

    format!(
        "{:04}-{:02}-{:02} {:02}:{:02} UTC",
        at.year, at.month, at.day, at.hour, at.minute
    )
}

/// The names, as a document writes them.
pub fn names(json: &mut Json, names: &[String]) {
    json.array("names", |json| {
        for name in names {
            json.string_value(name);
        }
    });
}
