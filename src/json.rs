//! One JSON document as text, written by hand: an object, its members in
//! the order written, on one line, so it is one JSON Lines record when
//! several follow each other (ADR-0014 clause 10). A document for a program
//! escapes what the command line always escaped — a quote, `<`, `>`, `&`,
//! `'`, `+`, `` ` ``, every control character and everything beyond ASCII,
//! as `\u` and four upper-case digits — so what a pipe reads is what it
//! read before; an MCP message escapes only what JSON requires, since the
//! document it carries is itself a string inside it.

use std::fmt::Write as _;

/// How a document escapes its strings.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Escaping {
    /// A program's document: every character a web page would misread too.
    Document,
    /// An MCP message: what JSON requires and no more.
    Relaxed,
}

/// A document being written.
#[derive(Debug)]
pub struct Json {
    out: String,
    escaping: Escaping,
    // Whether the container being written has a member yet, innermost last.
    open: Vec<bool>,
}

impl Json {
    /// One document: `body` writes into the object it is.
    #[must_use]
    pub fn document(body: impl FnOnce(&mut Self)) -> String {
        Self::with(Escaping::Document, body)
    }

    /// One document escaped as `escaping` says.
    #[must_use]
    pub fn with(escaping: Escaping, body: impl FnOnce(&mut Self)) -> String {
        let mut json = Self {
            out: String::from("{"),
            escaping,
            open: vec![false],
        };
        body(&mut json);
        json.out.push('}');
        json.out
    }

    fn next(&mut self) {
        if let Some(any) = self.open.last_mut() {
            if *any {
                self.out.push(',');
            }
            *any = true;
        }
    }

    fn name(&mut self, name: &str) {
        self.next();
        self.quoted(name);
        self.out.push(':');
    }

    fn quoted(&mut self, text: &str) {
        self.out.push('"');
        for character in text.chars() {
            escape(&mut self.out, character, self.escaping);
        }
        self.out.push('"');
    }

    /// `"name":"value"`.
    pub fn string(&mut self, name: &str, value: &str) {
        self.name(name);
        self.quoted(value);
    }

    /// `"name":"moment"`, a moment as `time::document` writes it, quoted and
    /// never escaped, as .NET writes a date and time: its `+` stays a `+`.
    pub fn moment(&mut self, name: &str, moment: &str) {
        self.name(name);
        self.out.push('"');
        self.out.push_str(moment);
        self.out.push('"');
    }

    /// `"name":value`, a whole number.
    pub fn number(&mut self, name: &str, value: impl Into<i128>) {
        self.name(name);
        let _ = write!(self.out, "{}", value.into());
    }

    /// `"name":value`, a number with a fraction, as .NET's shortest
    /// round-trip form writes it.
    pub fn decimal(&mut self, name: &str, value: f64) {
        self.name(name);
        self.raw_decimal(value);
    }

    /// `"name":true` or `false`.
    pub fn boolean(&mut self, name: &str, value: bool) {
        self.name(name);
        self.out.push_str(if value { "true" } else { "false" });
    }

    /// `"name":null`.
    pub fn null(&mut self, name: &str) {
        self.name(name);
        self.out.push_str("null");
    }

    /// `"name":value`, or `null` for none.
    pub fn number_or_null(&mut self, name: &str, value: Option<impl Into<i128>>) {
        match value {
            Some(value) => self.number(name, value),
            None => self.null(name),
        }
    }

    /// `"name":"value"`, or `null` for none.
    pub fn string_or_null(&mut self, name: &str, value: Option<&str>) {
        match value {
            Some(value) => self.string(name, value),
            None => self.null(name),
        }
    }

    /// `"name":` and JSON written as it is: a document within this one.
    pub fn raw(&mut self, name: &str, json: &str) {
        self.name(name);
        self.out.push_str(json);
    }

    /// `"name":{`, the members `body` writes, `}`.
    pub fn object(&mut self, name: &str, body: impl FnOnce(&mut Self)) {
        self.name(name);
        self.inside('{', '}', body);
    }

    /// `"name":[`, the values `body` writes, `]`.
    pub fn array(&mut self, name: &str, body: impl FnOnce(&mut Self)) {
        self.name(name);
        self.inside('[', ']', body);
    }

    /// `{`, the members `body` writes, `}`, as a value of an array.
    pub fn object_value(&mut self, body: impl FnOnce(&mut Self)) {
        self.next();
        self.inside('{', '}', body);
    }

    /// `[`, the values `body` writes, `]`, as a value of an array.
    pub fn array_value(&mut self, body: impl FnOnce(&mut Self)) {
        self.next();
        self.inside('[', ']', body);
    }

    /// A string, as a value of an array.
    pub fn string_value(&mut self, value: &str) {
        self.next();
        self.quoted(value);
    }

    /// A whole number, as a value of an array.
    pub fn number_value(&mut self, value: impl Into<i128>) {
        self.next();
        let _ = write!(self.out, "{}", value.into());
    }

    /// `null`, as a value of an array or after a name.
    pub fn null_value(&mut self) {
        self.next();
        self.out.push_str("null");
    }

    /// JSON written as it is, as a value of an array.
    pub fn raw_value(&mut self, json: &str) {
        self.next();
        self.out.push_str(json);
    }

    fn inside(&mut self, open: char, close: char, body: impl FnOnce(&mut Self)) {
        self.out.push(open);
        self.open.push(false);
        body(self);
        self.open.pop();
        self.out.push(close);
    }

    fn raw_decimal(&mut self, value: f64) {
        if value.is_finite() && value.fract() == 0.0 && value.abs() < 1e15 {
            let _ = write!(self.out, "{value:.0}");
        } else {
            let _ = write!(self.out, "{value}");
        }
    }
}

/// A `serde_json` value written into a document as this module escapes,
/// its members in the order the value holds them.
pub fn value(json: &mut Json, name: &str, value: &serde_json::Value) {
    json.name(name);
    json.any(value);
}

impl Json {
    /// A `serde_json` value, as a value of an array.
    pub fn value_value(&mut self, value: &serde_json::Value) {
        self.next();
        self.any(value);
    }

    fn any(&mut self, given: &serde_json::Value) {
        match given {
            serde_json::Value::Null => self.out.push_str("null"),
            serde_json::Value::Bool(yes) => self.out.push_str(if *yes { "true" } else { "false" }),
            serde_json::Value::Number(number) => {
                let _ = write!(self.out, "{number}");
            }
            serde_json::Value::String(text) => self.quoted(text),
            serde_json::Value::Array(values) => self.inside('[', ']', |json| {
                for each in values {
                    json.value_value(each);
                }
            }),
            serde_json::Value::Object(members) => self.inside('{', '}', |json| {
                for (name, each) in members {
                    value(json, name, each);
                }
            }),
        }
    }
}

// One character, escaped as `escaping` says.
fn escape(out: &mut String, character: char, escaping: Escaping) {
    match character {
        '\n' => out.push_str("\\n"),
        '\r' => out.push_str("\\r"),
        '\t' => out.push_str("\\t"),
        '\u{8}' => out.push_str("\\b"),
        '\u{c}' => out.push_str("\\f"),
        '\\' => out.push_str("\\\\"),
        '"' if escaping == Escaping::Relaxed => out.push_str("\\\""),
        _ if needs_escape(character, escaping) => {
            let mut units = [0u16; 2];
            for unit in character.encode_utf16(&mut units) {
                let _ = write!(out, "\\u{unit:04X}");
            }
        }
        _ => out.push(character),
    }
}

fn needs_escape(character: char, escaping: Escaping) -> bool {
    let control = character < ' ' || ('\u{7f}'..='\u{9f}').contains(&character);
    let separator = matches!(character, '\u{2028}' | '\u{2029}');

    match escaping {
        Escaping::Relaxed => control || separator,
        Escaping::Document => {
            control
                || !character.is_ascii()
                || matches!(character, '"' | '<' | '>' | '&' | '\'' | '+' | '`')
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_document_is_one_line_its_members_in_the_order_written() {
        let scope = configure::fixture::test_cluster().scope();
        let document = Json::document(|json| {
            json.string("scope", &scope);
            json.number("severity", 40u8);
            json.boolean("valid", true);
            json.null("observed");
            json.array("problems", |json| {
                json.string_value("one");
                json.object_value(|json| json.number("n", -2i32));
            });
        });

        assert_eq!(
            document,
            format!(
                r#"{{"scope":"{scope}","severity":40,"valid":true,"observed":null,"problems":["one",{{"n":-2}}]}}"#
            )
        );
    }

    #[test]
    fn a_document_escapes_as_the_command_line_always_did_and_a_message_less() {
        let text = "a \"b\" <c> & d's + `e` → f\\\n";
        let u = |hex: &str| format!("{}u{hex}", '\\');
        let escaped = format!(
            r#"{{"t":"a {q}b{q} {lt}c{gt} {amp} d{ap}s {plus} {bt}e{bt} {arrow} f\\\n"}}"#,
            q = u("0022"),
            lt = u("003C"),
            gt = u("003E"),
            amp = u("0026"),
            ap = u("0027"),
            plus = u("002B"),
            bt = u("0060"),
            arrow = u("2192"),
        );

        assert_eq!(Json::document(|json| json.string("t", text)), escaped);
        assert_eq!(
            Json::with(Escaping::Relaxed, |json| json.string("t", text)),
            "{\"t\":\"a \\\"b\\\" <c> & d's + `e` → f\\\\\\n\"}"
        );
        assert_eq!(
            Json::document(|json| json.string("t", "😀")),
            format!(r#"{{"t":"{}{}"}}"#, u("D83D"), u("DE00"))
        );
    }
}
