//! One command line, parsed: the command, its argument and its options,
//! with nothing but a list of words — an audit's query among them.

use configure::fixture::test_cluster;
use operate::surface::acted;
use operate::surface::listener::{local_refusal, remote_refusal};
use xmip_core_cli::command::Command;
use xmip_core_cli::invocation::Invocation;
use xmip_core_cli::invocation::audit_arguments::AuditAsk;
use xmip_core_cli::system::user;

fn parsed(words: &[&str]) -> Result<Invocation, String> {
    Invocation::parse(&words.iter().map(ToString::to_string).collect::<Vec<_>>())
}

#[test]
fn no_arguments_is_help() {
    let parsed = parsed(&[]).expect("help");

    assert_eq!(parsed.command, Command::Help);
}

#[test]
fn each_spelling_of_help_is_help() {
    for word in ["help", "--help", "-h"] {
        assert_eq!(
            parsed(&[word]).expect(word).command,
            Command::Help,
            "{word}"
        );
    }
}

#[test]
fn a_command_with_its_argument_parses() {
    let parsed = parsed(&["health", "xmip:///"]).expect("parsed");

    assert_eq!(parsed.command, Command::Health);
    assert_eq!(parsed.argument, "xmip:///");
    assert!(!parsed.json);
    assert!(!parsed.follow);
}

#[test]
fn options_sit_anywhere() {
    let parsed = parsed(&["--json", "validate", "node.xmip"]).expect("parsed");

    assert_eq!(parsed.command, Command::Validate);
    assert_eq!(parsed.argument, "node.xmip");
    assert!(parsed.json);
}

#[test]
fn follow_implies_json() {
    let parsed = parsed(&["health", "xmip:///", "--follow"]).expect("parsed");

    assert!(parsed.follow);
    assert!(parsed.json);
}

#[test]
fn follow_applies_to_live_commands_only() {
    let problem = parsed(&["abi", "--follow"]).expect_err("refused");

    assert!(problem.contains("--follow"), "{problem}");
}

#[test]
fn measure_defaults_to_the_cluster_and_may_follow() {
    let parsed = parsed(&["measure", "--follow"]).expect("parsed");

    assert_eq!(parsed.command, Command::Measure);
    assert_eq!(parsed.argument, "");
    assert!(parsed.follow);
}

#[test]
fn measure_accepts_one_scope() {
    let scope = test_cluster().scope();

    assert_eq!(
        parsed(&["measure", &scope]).expect("parsed").argument,
        scope
    );
}

#[test]
fn list_defaults_to_the_cluster() {
    let parsed = parsed(&["list"]).expect("parsed");

    assert_eq!(parsed.command, Command::List);
    assert_eq!(parsed.argument, "");
}

#[test]
fn remote_names_a_node_s_operate_address() {
    let parsed =
        parsed(&["health", "xmip:///", "--remote", "node1:7401", "--follow"]).expect("parsed");

    assert_eq!(parsed.remote.as_deref(), Some("node1:7401"));
    assert!(parsed.follow);
}

#[test]
fn remote_needs_an_address() {
    // The sentence is the one every surface says; what an address is, the
    // runtime judges as it opens it.
    let bare = parsed(&["health", "xmip:///", "--remote"]).expect_err("refused");

    assert_eq!(Some(bare.as_str()), remote_refusal(None));
    assert!(bare.contains("node1:7401"), "{bare}");
}

#[test]
fn local_names_a_node_s_local_endpoint_and_needs_one() {
    // ADR-0014, amendment 2026-10-13: a node on this machine is followed over
    // gRPC at its local endpoint, and the line states it like a remote.
    let parsed_local = parsed(&["health", "xmip:///", "--local", "xmip-node1"]).expect("parsed");

    assert_eq!(parsed_local.local.as_deref(), Some("xmip-node1"));
    assert_eq!(parsed_local.line().local.as_deref(), Some("xmip-node1"));
    assert_eq!(parsed_local.remote, None);

    let bare = parsed(&["health", "xmip:///", "--local"]).expect_err("refused");
    assert_eq!(Some(bare.as_str()), local_refusal(None));
}

#[test]
fn who_names_the_pauser_and_only_for_pause() {
    let scope = test_cluster().scope();
    let parsed_pause = parsed(&["pause", &scope, "--who", "ilian"]).expect("parsed");

    assert_eq!(parsed_pause.who.as_deref(), Some("ilian"));
    assert_eq!(
        acted::who(parsed_pause.who.as_deref(), &user::name()),
        "ilian"
    );
    assert_eq!(acted::who(None, &user::name()), user::name());

    let not = parsed(&["resume", &scope, "--who", "ilian"]).expect_err("refused");
    assert!(not.contains("--who"), "{not}");
    assert!(parsed(&["pause", &scope, "--who"]).is_err());
}

#[test]
fn runtime_is_no_option_of_a_command_that_links_the_runtime() {
    // The runtime is linked (ADR-0014, amendment 2026-11-17): there is no
    // library to name.
    let problem = parsed(&["health", "xmip:///", "--runtime", "lib.dll"]).expect_err("refused");

    assert_eq!(
        problem,
        "'--runtime' is not an xmip option. Try 'xmip help'."
    );
}

#[test]
fn a_negative_status_code_is_an_argument_not_an_option() {
    // Every status but Ok is negative; a parser that read '-22' as an option
    // would refuse the one argument 'status' exists to take.
    let parsed = parsed(&["status", "-22"]).expect("parsed");

    assert_eq!(parsed.command, Command::Status);
    assert_eq!(parsed.argument, "-22");
}

#[test]
fn an_unknown_command_is_refused_by_name() {
    let problem = parsed(&["frobnicate"]).expect_err("refused");

    assert!(problem.contains("'frobnicate'"), "{problem}");
}

#[test]
fn an_unknown_option_is_refused_by_name() {
    let problem = parsed(&["abi", "--verbose"]).expect_err("refused");

    assert!(problem.contains("'--verbose'"), "{problem}");
}

#[test]
fn a_command_that_takes_an_argument_refuses_to_go_without() {
    for word in [
        "status", "probe", "health", "validate", "show", "pause", "resume",
    ] {
        let problem = parsed(&[word]).expect_err(word);
        assert!(
            problem.contains("exactly one argument"),
            "{word}: {problem}"
        );
    }
}

#[test]
fn a_command_without_an_argument_refuses_one() {
    for word in ["abi", "help"] {
        let problem = parsed(&[word, "extra"]).expect_err(word);
        assert!(problem.contains("no argument"), "{word}: {problem}");
    }
}

#[test]
fn every_option_is_a_word_of_the_query_and_the_argument_its_pattern() {
    let cluster = test_cluster();
    let pattern = format!("{}/*", cluster.scope());
    let scope = cluster.scope();
    let line = [
        "audit",
        &pattern,
        "--location",
        &scope,
        "--host",
        "H1",
        "--program",
        "xmip",
        "--record",
        "r1",
        "--severity",
        "Error",
        "--action",
        "pause",
        "--from",
        "2026-09-29",
        "--to",
        "2026-09-30T12:00",
        "--sort",
        "node",
        "--order",
        "ascending",
        "--offset",
        "5",
        "--limit",
        "7",
        "--json",
    ];
    let parsed = parsed(&line).expect("parsed");

    assert_eq!(parsed.command, Command::Audit);
    assert!(parsed.json);
    assert_eq!(
        parsed.audit,
        Some(AuditAsk {
            pattern: Some(pattern.clone()),
            location: Some(scope.clone()),
            host: Some("H1".into()),
            program: Some("xmip".into()),
            record: Some("r1".into()),
            severity: Some("Error".into()),
            action: Some("pause".into()),
            from: Some("2026-09-29".into()),
            to: Some("2026-09-30T12:00".into()),
            sort: Some("node".into()),
            order: Some("ascending".into()),
            offset: 5,
            limit: 7,
            ..AuditAsk::default()
        })
    );
}

#[test]
fn audit_with_nothing_asked_is_the_empty_query() {
    let parsed = parsed(&["audit"]).expect("parsed");
    let asked = parsed.audit.expect("a query");

    assert_eq!(asked, AuditAsk::default());
    assert_eq!(asked.pairs(), Vec::<(String, String)>::new());
}

#[test]
fn a_line_the_audit_cannot_obey_is_refused_as_any_line() {
    for (line, expected) in [
        (
            &["health", "xmip:///", "--severity", "Error"][..],
            "--severity only applies to 'audit'.",
        ),
        (
            &["audit", "--limit", "many"][..],
            "--limit needs a whole number, not 'many'.",
        ),
        (&["audit", "--location"][..], "--location needs a value."),
        (
            &["health", "xmip:///", "--include-hidden"][..],
            "--include-hidden only applies to 'audit'.",
        ),
    ] {
        assert_eq!(parsed(line), Err(expected.to_string()), "{line:?}");
    }
}
