//! `xmip validate <toml>`: the runtime's answer on a node configuration file,
//! without starting it. The file's text is validated, not its path — the
//! runtime validates a proposed document and publishes nothing (ADR-0027
//! clause 9). The problems are `runtime::start::validate`'s and the verdict
//! `operate::surface::configuration_verdict`'s, the same record and
//! sentence the desktop shows (ADR-0052 clause 4); the exit is decided by
//! the record, never the sentence.

use std::io::Write;
use std::path::Path;

use abi::status_meaning;
use operate::surface::configuration_verdict::ConfigurationVerdict;

use crate::json::Json;
use crate::say;

/// Validate the file at `path`: exit 2 where there is no file, 1 where it
/// cannot be read or is not valid.
pub fn run(path: &str, json: bool, output: &mut dyn Write, error: &mut dyn Write) -> i32 {
    if !Path::new(path).is_file() {
        say(error, &format!("No file at {path}."));
        return 2;
    }

    match std::fs::read_to_string(path) {
        Ok(text) => verdict(
            &ConfigurationVerdict::of(path, runtime::start::validate(&text)),
            json,
            output,
            error,
        ),
        Err(failure) => {
            say(error, &format!("{path}: {failure}"));
            1
        }
    }
}

/// Render one verdict: text for a person, or the record as a document.
/// Exit 0 where the document is valid, 1 where it is not.
pub fn verdict(
    verdict: &ConfigurationVerdict,
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    let exit = i32::from(!verdict.ok());

    if json {
        say(
            output,
            &Json::document(|json| {
                json.string("path", &verdict.path);
                json.boolean("valid", verdict.ok());
                json.string("status", &named(verdict.status));
                json.string("meaning", status_meaning::explain(verdict.status));
                json.array("problems", |json| {
                    for problem in &verdict.problems {
                        json.string_value(problem);
                    }
                });
            }),
        );
        return exit;
    }

    if verdict.ok() {
        say(output, &verdict.said);
    } else {
        say(error, &verdict.said);
    }
    exit
}

// A status by its header name, or its number where the header names none.
fn named(code: i32) -> String {
    status_meaning::name(code).map_or_else(|| code.to_string(), str::to_string)
}
