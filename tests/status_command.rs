//! `xmip status <code>`: a code explained as text and as one document, one
//! the header does not define said so, and a word that is no number
//! refused.

use abi::ffi::status;
use abi::status_meaning::explain;
use xmip_core_cli::command::status::run;

fn answered(code: &str, json: bool) -> (i32, String, String) {
    let (mut output, mut error) = (Vec::new(), Vec::new());
    let exit = run(code, json, &mut output, &mut error);
    (
        exit,
        String::from_utf8(output).expect("UTF-8"),
        String::from_utf8(error).expect("UTF-8"),
    )
}

#[test]
fn explains_a_known_code_as_text() {
    let (exit, said, _) = answered("-21", false);
    let lines: Vec<&str> = said.split('\n').collect();

    assert_eq!(exit, 0);
    assert_eq!(lines[0], "  -21  Timeout");
    assert_eq!(lines[1], format!("       {}", explain(status::TIMEOUT)));
    assert_eq!(lines[2], "       retryable: yes   terminal: no");
}

#[test]
fn explains_a_known_code_as_json() {
    let (exit, said, _) = answered("-41", true);
    let document: serde_json::Value = serde_json::from_str(&said).expect("one document");

    assert_eq!(exit, 0);
    assert_eq!(document["code"], -41);
    assert_eq!(document["name"], "Panic");
    assert_eq!(document["known"], true);
    assert_eq!(document["terminal"], true);
    assert_eq!(document["retryable"], false);
}

#[test]
fn an_unknown_code_is_said_so_and_exits_one() {
    let (exit, said, _) = answered("-99", true);
    let document: serde_json::Value = serde_json::from_str(&said).expect("one document");

    assert_eq!(exit, 1);
    assert_eq!(document["known"], false);
    assert_eq!(document["name"], "unknown");
}

#[test]
fn a_non_number_is_a_usage_error_on_stderr() {
    let (exit, _, error) = answered("ok", false);

    assert_eq!(exit, 2);
    assert!(error.contains("'ok' is not a number"), "{error}");
}
