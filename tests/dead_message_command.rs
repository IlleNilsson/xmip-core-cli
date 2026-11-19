//! `xmip dead-messages`: the line is the query and Replay an option; the
//! list says message, cluster, node and declines as text and as a
//! document; one Message named is opened; a Replay names one or is refused
//! before any node is asked.

#[path = "support/noun.rs"]
mod noun_support;

use configure::fixture::{TestCluster, test_cluster};
use observe::Act;
use operate::surface::snapshot::NOT_ACTED;
use xmip_core_cli::command::Command;

use noun_support::{Published, document, in_order, line, refused, run};

fn entry(
    cluster: &TestCluster,
    node: &str,
    (message, sequence): (&str, u64),
    rest: &str,
) -> String {
    format!(
        "[[dead_messages]]\nnode = \"{}/node/{node}\"\nmessage = \"{message}\"\n\
         sequence = {sequence}\nlocation = \"orders\"\nreceived_unix_nanos = {}\n{rest}",
        cluster.scope(),
        sequence * 1_000_000_000
    )
}

// The test cluster's first two nodes, by place: the first's queue keeps
// two Messages, the second's one.
fn published() -> (TestCluster, Published) {
    let cluster = test_cluster();
    let (first, second) = (cluster.node(0).name.clone(), cluster.node(1).name.clone());
    let text = format!(
        "node = \"{}\"\n{}{}{}",
        cluster.scope(),
        entry(
            &cluster,
            &first,
            ("m-1", 1),
            "declines = [[\"structured\", \"no MessageType\"]]\n"
        ),
        entry(
            &cluster,
            &first,
            ("m-2", 2),
            "validation = [[\"schema\", \"valid\"]]\n\
             promoted = [[\"MessageType\", \"Invoice\"]]\n\
             declines = [[\"structured\", \"MessageType is Invoice\"], [\"edi\", \"paused\"]]\n"
        ),
        entry(&cluster, &second, ("m-3", 1), ""),
    );
    let published = Published::new("dead-messages", &cluster.name, &text);
    (cluster, published)
}

#[test]
fn the_line_is_the_query_and_replay_an_option() {
    let cluster = test_cluster();
    let pattern = format!("*/{}", cluster.node(0).name);
    let location = cluster.node_scope(0);
    let parsed = line(&[
        "dead-messages",
        &pattern,
        "--location",
        &location,
        "--message",
        "m-2",
        "--sort",
        "declines",
        "--order",
        "descending",
        "--replay",
        "--who",
        "ilian",
    ]);

    assert_eq!(parsed.command, Command::DeadMessages);
    let asked =
        xmip_core_cli::command::noun::dead_message::query(&parsed.noun.clone().expect("a query"));
    assert_eq!(asked.pattern.as_deref(), Some(pattern.as_str()));
    assert_eq!(asked.location.as_deref(), Some(location.as_str()));
    assert_eq!(asked.message.as_deref(), Some("m-2"));
    assert_eq!(asked.sort.as_deref(), Some("declines"));
    assert_eq!(asked.order.as_deref(), Some("descending"));
    assert_eq!(parsed.act, Some(Act::Replay));
    assert_eq!(parsed.who.as_deref(), Some("ilian"));
}

#[test]
fn a_line_that_cannot_be_obeyed_is_said_so() {
    for (said, expected) in [
        ("dead-messages --pause", "--replay is its one act"),
        ("dead-messages --remove", "--replay is its one act"),
        ("dead-messages --replay --pause", "one act"),
        ("dead-messages --message", "--message needs"),
        (
            "subscriptions --replay",
            "--replay only applies to 'dead-messages'",
        ),
        (
            "event-subscriptions --replay",
            "--replay only applies to 'dead-messages'",
        ),
        (
            "subscriptions --message m-1",
            "--message only applies to 'dead-messages'",
        ),
        (
            "dead-messages --name edi",
            "--name only applies to 'subscriptions'",
        ),
        (
            "dead-messages --program xmip",
            "--program only applies to 'audit'",
        ),
        ("dead-messages --who ilian", "--who only applies"),
    ] {
        let words: Vec<&str> = said.split(' ').collect();
        let problem = refused(&words);
        assert!(problem.contains(expected), "{said}: {problem}");
    }
}

#[test]
fn the_list_says_message_cluster_node_and_declines_as_text_and_as_a_document() {
    let (cluster, published) = published();
    let surface = published.surface();
    let first = &cluster.node(0).name;

    let (exit, text, _) = run(&surface, &line(&["dead-messages"]), "");
    assert_eq!(exit, 0);
    assert!(
        text.contains("3 Message(s) in the Dead Message Queue"),
        "{text}"
    );
    assert!(
        in_order(&text, &["m-2", &cluster.name, first, "orders", "2"]),
        "{text}"
    );

    let location = cluster.node_scope(0);
    let (_, json, _) = run(
        &surface,
        &line(&["dead-messages", "--location", &location, "--json"]),
        "",
    );
    let read = document(&json);
    let listed = read["dead_messages"].as_array().expect("a list");
    assert_eq!(listed.len(), 2);
    assert_eq!(listed[0]["message"], "m-1");
    let declines = listed[1]["declines"].as_array().expect("declines");
    assert_eq!(declines.len(), 2);
    assert_eq!(declines[1]["name"], "edi");
    assert_eq!(listed[1]["promoted"][0]["value"], "Invoice");
}

#[test]
fn one_message_named_is_opened_with_its_verdicts_properties_and_declines() {
    let (cluster, published) = published();
    let location = cluster.node_scope(0);

    let (exit, text, _) = run(
        &published.surface(),
        &line(&["dead-messages", "--location", &location, "--message", "m-2"]),
        "",
    );
    assert_eq!(exit, 0);
    assert!(
        text.contains(&format!(
            "Message m-2 in the Dead Message Queue of {location}"
        )),
        "{text}"
    );
    assert!(
        in_order(&text, &["gate verdicts:", "schema", "valid"]),
        "{text}"
    );
    assert!(
        in_order(&text, &["promoted properties:", "MessageType", "Invoice"]),
        "{text}"
    );
    assert!(
        in_order(
            &text,
            &[
                "declines:",
                "structured",
                "MessageType is Invoice",
                "edi",
                "paused"
            ]
        ),
        "{text}"
    );
}

#[test]
fn the_pattern_drills_and_the_sort_orders() {
    let (_, published) = published();
    let surface = published.surface();

    let (_, named, _) = run(
        &surface,
        &line(&["dead-messages", "*/dead-message/m-3", "--json"]),
        "",
    );
    let one = document(&named);
    let listed = one["dead_messages"].as_array().expect("a list");
    assert_eq!(listed.len(), 1);
    assert_eq!(listed[0]["message"], "m-3");

    let (_, sorted, _) = run(
        &surface,
        &line(&[
            "dead-messages",
            "--sort",
            "declines",
            "--order",
            "descending",
            "--json",
        ]),
        "",
    );
    let all = document(&sorted);
    let messages: Vec<&str> = all["dead_messages"]
        .as_array()
        .expect("a list")
        .iter()
        .map(|entry| entry["message"].as_str().expect("a message"))
        .collect();
    assert_eq!(messages, ["m-2", "m-1", "m-3"]);

    let (exit, _, error) = run(&surface, &line(&["dead-messages", "--sort", "held"]), "");
    assert_eq!(exit, 2);
    assert!(error.starts_with("REFUSED"), "{error}");
}

#[test]
fn a_replay_names_one_message_or_is_refused_before_any_node_is_asked() {
    let (cluster, published) = published();
    let surface = published.surface();

    let (exit, _, error) = run(&surface, &line(&["dead-messages", "--replay"]), "");
    assert_eq!(exit, 2);
    assert!(error.starts_with("REFUSED"), "{error}");

    let second = cluster.node_scope(1);
    let (exit, _, error) = run(
        &surface,
        &line(&[
            "dead-messages",
            "--location",
            &second,
            "--message",
            "m-2",
            "--replay",
        ]),
        "",
    );
    assert_eq!(exit, 1);
    assert!(error.contains("no Message m-2"), "{error}");

    let first = cluster.node_scope(0);
    let (exit, _, error) = run(
        &surface,
        &line(&[
            "dead-messages",
            "--location",
            &first,
            "--message",
            "m-2",
            "--replay",
            "--who",
            "ilian",
        ]),
        "",
    );
    assert_eq!(exit, 1);
    assert!(
        error.starts_with(&format!("REFUSED: {NOT_ACTED}")),
        "{error}"
    );
}
