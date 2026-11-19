//! `xmip secret`: the secret is the argument, its kind and file options and
//! Set the act; a line that would set nothing, or put a value on the line,
//! is refused; the value is read from the file or standard input and never
//! said; a listing says each secret by name, kind and when, never a value.

#[path = "support/fake.rs"]
mod fake;
#[path = "support/noun.rs"]
mod noun_support;

use configure::fixture::test_cluster;
use operate::surface::acted::SecretRecord;
use xmip_core_cli::command::Command;
use xmip_core_cli::command::noun::secret;
use xmip_core_cli::invocation::secret_arguments::SecretAsk;

use fake::FakeSurface;
use noun_support::{Published, line, refused, run};

#[test]
fn the_secret_is_the_argument_its_kind_and_file_options_and_set_the_act() {
    let cluster = test_cluster();
    let node = cluster.node_scope(0);
    let parsed = line(&[
        "secret",
        "partner-x",
        "--set",
        "--kind",
        "key",
        "--file",
        "key.pem",
        "--location",
        &node,
        "--who",
        "ilian",
    ]);

    assert_eq!(parsed.command, Command::Secret);
    assert_eq!(
        parsed.secret,
        Some(SecretAsk {
            name: "partner-x".into(),
            kind: Some("key".into()),
            file: Some("key.pem".into()),
            set: true,
        })
    );
    assert_eq!(parsed.location.as_deref(), Some(node.as_str()));
    assert_eq!(line(&["secret"]).secret, Some(SecretAsk::default()));
}

#[test]
fn a_line_that_would_set_nothing_or_put_a_value_on_the_command_line_is_refused() {
    let said = |words: &[&str], expected: &str| {
        let problem = refused(words);
        assert!(problem.contains(expected), "{words:?}: {problem}");
    };

    said(&["secret", "partner-x"], "needs --set");
    said(&["secret", "--set"], "name it and its kind");
    said(&["secret", "partner-x", "--set"], "name it and its kind");
    said(
        &["message", "j-1", "--set"],
        "--set only applies to 'secret'",
    );
    said(
        &["health", "x", "--kind", "key"],
        "only applies to 'secret'",
    );
    said(&["secret", "partner-x", "--pause"], "--set is its act");
    said(
        &["secret", "partner-x", "--retry"],
        "only applies to 'message'",
    );
    said(
        &["secret", "partner-x", "--value", "x"],
        "not an xmip option",
    );
}

#[test]
fn the_value_is_read_from_the_file_or_standard_input_and_never_said() {
    let drawn = format!("{:x}", std::process::id() ^ 0x5eed_c0de);
    let published = Published::new("secret", "value", "");
    let file = published.beside("value.txt", &drawn);
    let from_file = SecretAsk {
        name: "partner-x".into(),
        kind: Some("password".into()),
        file: Some(file.display().to_string()),
        set: true,
    };
    let from_input = SecretAsk {
        file: None,
        ..from_file.clone()
    };

    assert_eq!(from_file.value(&mut std::io::empty()).expect("read"), drawn);
    assert_eq!(
        from_input
            .value(&mut format!("{drawn}\n").as_bytes())
            .expect("read"),
        drawn
    );

    let surface = FakeSurface::new(Vec::new());
    let (exit, said, error) = run(
        &surface,
        &line(&[
            "secret",
            "partner-x",
            "--set",
            "--kind",
            "password",
            "--who",
            "ilian",
        ]),
        &drawn,
    );
    assert_eq!(exit, 1);
    assert!(error.contains("reaches no node to set a secret"), "{error}");
    assert!(!format!("{error}{said}").contains(&drawn));

    let (exit, _, error) = run(&surface, &line(&["secret", "--json"]), "");
    assert_eq!(exit, 1);
    assert!(error.contains("lists no secrets"), "{error}");
}

#[test]
fn a_listing_says_each_secret_by_name_kind_and_when_never_a_value() {
    let one = SecretRecord {
        name: "partner-x".into(),
        kind: "key".into(),
        set_unix_nanos: 0,
        set_by: "CN=ilian".into(),
    };
    let document = secret::document("FAKE", &[one]);

    assert!(document.contains("\"name\":\"partner-x\""), "{document}");
    assert!(document.contains("\"kind\":\"key\""), "{document}");
    assert!(!document.contains("value"), "{document}");
}
