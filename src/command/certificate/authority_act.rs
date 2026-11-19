//! What each act on the cluster's authority says (ADR-0034, amendment
//! 2026-10-22): the authority set up or inspected with everything it
//! signed, a certificate's standing, a revocation, an export or a backup,
//! and a node's certificate carried across an air gap — requested, signed
//! and imported. Each act is `operate::authority`'s; this renders its
//! answer.

use std::io::Write;

use serde_json::Value;

use super::answer::{Answer, day, document_moment, moment, names, text, texts};
use crate::json::Json;
use crate::text_table;

/// The authority, set up or inspected: `done` says which.
pub fn inspected(asked: &Result<Value, String>, answer: &mut Answer<'_>, done: &str) -> i32 {
    let empty = Value::Null;
    let it = asked.as_ref().unwrap_or(&empty);
    let list = &it["list"];
    let issued: Vec<Value> = it["issued"].as_array().cloned().unwrap_or_default();
    let revoked = list["revoked"].as_i64().unwrap_or(0);
    let published = text(it, "published");
    let sentence = format!(
        "{done} {}: {}, valid until {}, SHA-256 {}. {}{} certificate(s) signed, {revoked} \
         revoked.",
        text(it, "authority"),
        text(it, "subject"),
        day(moment(it, "notAfter")),
        text(it, "fingerprint"),
        if published.is_empty() {
            "It publishes at no address; give one with --publish http://host:port. ".to_string()
        } else {
            format!("Its list and certificate are published at {published}. ")
        },
        issued.len(),
    );

    answer.said(
        asked.as_ref().err().map(String::as_str),
        |json| {
            json.string("authority", text(it, "authority"));
            json.string("subject", text(it, "subject"));
            json.moment("notAfter", &document_moment(moment(it, "notAfter")));
            json.string("fingerprint", text(it, "fingerprint"));
            json.string("published", published);
            json.number("listNumber", list["number"].as_i64().unwrap_or(0));
            json.moment(
                "listNextUpdate",
                &document_moment(moment(list, "nextUpdate")),
            );
            json.number("revoked", revoked);
            json.array("issued", |json| {
                for record in &issued {
                    json.object_value(|json| fields(json, record));
                }
            });
        },
        &sentence,
        |output: &mut dyn Write| {
            let mut rows = vec![
                ["SERIAL", "SUBJECT", "PURPOSE", "UNTIL", "STANDING"]
                    .map(String::from)
                    .to_vec(),
            ];
            rows.extend(issued.iter().map(|record| {
                vec![
                    text(record, "serial").to_string(),
                    text(record, "subject").to_string(),
                    text(record, "purpose").to_string(),
                    day(moment(record, "notAfter")),
                    standing(record),
                ]
            }));
            text_table::write(output, "  ", &rows);
        },
    )
}

/// A certificate file's leaf and its standing.
pub fn read(asked: &Result<Value, String>, answer: &mut Answer<'_>) -> i32 {
    let empty = Value::Null;
    let it = asked.as_ref().unwrap_or(&empty);
    let named = texts(it, "names");
    let sentence = format!(
        "{}: {} ({}), serial {}, valid until {}, renewal due {}: {}.",
        text(it, "certificate"),
        text(it, "subject"),
        named.join(", "),
        text(it, "serial"),
        day(moment(it, "notAfter")),
        day(moment(it, "renewAt")),
        text(it, "standing").to_uppercase(),
    );

    answer.said(
        asked.as_ref().err().map(String::as_str),
        |json| {
            json.string("certificate", text(it, "certificate"));
            json.string("serial", text(it, "serial"));
            json.string("subject", text(it, "subject"));
            names(json, &named);
            json.moment("notAfter", &document_moment(moment(it, "notAfter")));
            json.moment("renewAt", &document_moment(moment(it, "renewAt")));
            json.string("standing", text(it, "standing"));
        },
        &sentence,
        |_: &mut dyn Write| {},
    )
}

/// A certificate revoked.
pub fn revoked(asked: &Result<Value, String>, answer: &mut Answer<'_>) -> i32 {
    let empty = Value::Null;
    let it = asked.as_ref().unwrap_or(&empty);
    let sentence = format!(
        "Revoked {} ({}) for {}; the authority's list names it from now.",
        text(it, "serial"),
        text(it, "subject"),
        text(&it["revoked"], "reason"),
    );

    answer.said(
        asked.as_ref().err().map(String::as_str),
        |json| fields(json, it),
        &sentence,
        |_: &mut dyn Write| {},
    )
}

/// The authority's certificate or list written out, or the authority
/// backed up: `written` and `format` as asked, where the runtime said none.
pub fn written(
    asked: &Result<Value, String>,
    (written, format): (&str, &str),
    answer: &mut Answer<'_>,
) -> i32 {
    let empty = Value::Null;
    let it = asked.as_ref().unwrap_or(&empty);
    let backup = format == "backup";
    let (written, format) = if backup {
        (written.to_string(), format.to_string())
    } else {
        (
            text(it, "written").to_string(),
            text(it, "format").to_string(),
        )
    };
    let copied = texts(it, "copied");
    let sentence = if backup {
        format!(
            "Backed up the authority into {written}: {} file(s), its key among them. Keep the \
             copy as the key is kept.",
            copied.len()
        )
    } else {
        format!("Wrote {written} ({format}).")
    };

    answer.said(
        asked.as_ref().err().map(String::as_str),
        |json| {
            json.string("written", &written);
            json.string("format", &format);
            json.array("copied", |json| {
                for one in &copied {
                    json.string_value(one);
                }
            });
        },
        &sentence,
        |_: &mut dyn Write| {},
    )
}

/// A node's request made for its certificate, to carry to the authority.
pub fn requested(asked: &Result<Value, String>, node: &str, answer: &mut Answer<'_>) -> i32 {
    let empty = Value::Null;
    let it = asked.as_ref().unwrap_or(&empty);
    let named = texts(it, "names");
    let sentence = format!(
        "Made {} for {}. Carry it to the machine that keeps the cluster's authority; {} stays \
         here.",
        text(it, "request"),
        named.join(", "),
        text(it, "key"),
    );

    answer.said(
        asked.as_ref().err().map(String::as_str),
        |json| {
            json.string("node", node);
            json.string("request", text(it, "request"));
            json.string("key", text(it, "key"));
            names(json, &named);
        },
        &sentence,
        |_: &mut dyn Write| {},
    )
}

/// A node's carried `request` signed, its chain written to `written`
/// where one was named.
pub fn signed(
    asked: &Result<Value, String>,
    (request, written): (&str, Option<&str>),
    answer: &mut Answer<'_>,
) -> i32 {
    let empty = Value::Null;
    let it = asked.as_ref().unwrap_or(&empty);
    let named = texts(it, "names");
    let chain = text(it, "chain");
    let tail = written.map_or_else(
        || format!(":\n{}", chain.trim_end()),
        |path| format!(": carry {path} back to the node."),
    );
    let sentence = format!(
        "Signed {request} for {}, serial {}, valid until {}{tail}",
        named.join(", "),
        text(it, "serial"),
        day(moment(it, "notAfter")),
    );

    answer.said(
        asked.as_ref().err().map(String::as_str),
        |json| {
            json.string("request", request);
            json.string("serial", text(it, "serial"));
            names(json, &named);
            json.moment("notAfter", &document_moment(moment(it, "notAfter")));
            json.string_or_null("written", written);
            json.string("chain", chain);
        },
        &sentence,
        |_: &mut dyn Write| {},
    )
}

/// A carried certificate imported on its node.
pub fn imported(asked: &Result<Value, String>, answer: &mut Answer<'_>) -> i32 {
    let empty = Value::Null;
    let it = asked.as_ref().unwrap_or(&empty);
    let taken = it["anchorTaken"].as_bool().unwrap_or(false);
    let anchor = if taken {
        format!(
            " The node held no anchor: {} is the authority with SHA-256 {}; compare it by \
             another way.",
            text(it, "anchors"),
            text(it, "anchorFingerprint")
        )
    } else {
        String::new()
    };
    let sentence = format!(
        "Imported {} as {} and {}, valid until {}; its renewal is due {}.{anchor}",
        text(it, "serial"),
        text(it, "certificate"),
        text(it, "key"),
        day(moment(it, "notAfter")),
        day(moment(it, "renewAt")),
    );

    answer.said(
        asked.as_ref().err().map(String::as_str),
        |json| {
            for name in ["certificate", "key", "anchors", "serial"] {
                json.string(name, text(it, name));
            }
            json.moment("notAfter", &document_moment(moment(it, "notAfter")));
            json.moment("renewAt", &document_moment(moment(it, "renewAt")));
            json.string("anchorFingerprint", text(it, "anchorFingerprint"));
            json.boolean("anchorTaken", taken);
        },
        &sentence,
        |_: &mut dyn Write| {},
    )
}

// One signed certificate's record: its standing where the authority said
// one, revoked where it is, else none.
fn standing(record: &Value) -> String {
    record["standing"].as_str().map_or_else(
        || {
            if record["revoked"].is_object() {
                "revoked".to_string()
            } else {
                String::new()
            }
        },
        str::to_string,
    )
}

fn fields(json: &mut Json, record: &Value) {
    json.string("serial", text(record, "serial"));
    json.string("subject", text(record, "subject"));
    names(json, &texts(record, "names"));
    json.string("purpose", text(record, "purpose"));
    for name in ["notBefore", "notAfter", "renewAt"] {
        json.moment(name, &document_moment(moment(record, name)));
    }
    json.string("standing", &standing(record));
    let revoked = &record["revoked"];
    if revoked.is_object() {
        json.moment("revokedAt", &document_moment(moment(revoked, "at")));
        json.string("reason", text(revoked, "reason"));
    }
}
