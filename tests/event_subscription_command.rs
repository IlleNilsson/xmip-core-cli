//! `xmip event-subscriptions`: the line is the query and the act an
//! option; the list says subscriber, cluster, node and action as text and
//! as a document, with the members a node does not hear; an act names one
//! or is refused before any node is asked.

#[path = "support/noun.rs"]
mod noun_support;

use configure::fixture::{TestCluster, test_cluster};
use observe::Act;
use operate::surface::snapshot::NOT_ACTED;
use xmip_core_cli::command::Command;

use noun_support::{Published, document, in_order, line, refused, run};

// The test cluster's first two nodes, by place, each holding an Event
// subscription; the first does not hear the third.
fn published() -> (TestCluster, Published) {
    let cluster = test_cluster();
    let text = format!(
        "node = \"{}\"\n\
         [[event_subscriptions]]\nnode = \"{}\"\nid = 1\nsubscriber = \"operations\"\n\
         party = \"0199a0a0-0000-7000-8000-000000000001\"\naction = \"every Event\"\n\
         state = \"active\"\nqueued = 2\ncapacity = 64\n\
         [[event_subscriptions]]\nnode = \"{}\"\nid = 2\nsubscriber = \"on-call\"\n\
         party = \"0199a0a0-0000-7000-8000-000000000002\"\n\
         action = \"every Event ending failure\"\nstate = \"paused\"\nqueued = 7\n\
         [[unheard]]\nby = \"{}\"\nnode = \"{}\"\nsince_unix_nanos = 1790000000000000000\n\
         why = \"connection refused\"\n",
        cluster.scope(),
        cluster.node_scope(0),
        cluster.node_scope(1),
        cluster.node_scope(0),
        cluster.node_scope(2),
    );
    let published = Published::new("event-subscriptions", &cluster.name, &text);
    (cluster, published)
}

#[test]
fn the_line_is_the_query_and_the_act_an_option() {
    let cluster = test_cluster();
    let pattern = format!("*/{}", cluster.node(0).name);
    let location = cluster.node_scope(0);
    let parsed = line(&[
        "event-subscriptions",
        &pattern,
        "--location",
        &location,
        "--id",
        "3",
        "--sort",
        "queued",
        "--order",
        "descending",
        "--pause",
        "--who",
        "ilian",
    ]);

    assert_eq!(parsed.command, Command::EventSubscriptions);
    let asked = xmip_core_cli::command::noun::event_subscription::query(
        &parsed.noun.clone().expect("a query"),
    );
    assert_eq!(asked.pattern.as_deref(), Some(pattern.as_str()));
    assert_eq!(asked.location.as_deref(), Some(location.as_str()));
    assert_eq!(asked.id, Some(3));
    assert_eq!(asked.sort.as_deref(), Some("queued"));
    assert_eq!(asked.order.as_deref(), Some("descending"));
    assert_eq!(parsed.act, Some(Act::Pause));
    assert_eq!(parsed.who.as_deref(), Some("ilian"));
}

#[test]
fn a_line_that_cannot_be_obeyed_is_said_so() {
    for (said, expected) in [
        ("event-subscriptions --pause --remove", "one act"),
        ("event-subscriptions --id x", "--id needs"),
        (
            "event-subscriptions --severity Error",
            "--severity only applies to 'audit'",
        ),
        ("list --id 3", "--id only applies to 'event-subscriptions'"),
        ("event-subscriptions --who ilian", "--who only applies"),
    ] {
        let words: Vec<&str> = said.split(' ').collect();
        let problem = refused(&words);
        assert!(problem.contains(expected), "{said}: {problem}");
    }
}

#[test]
fn the_list_says_subscriber_cluster_node_and_action_as_text_and_as_a_document() {
    let (cluster, published) = published();
    let surface = published.surface();
    let first = &cluster.node(0).name;
    let second = &cluster.node(1).name;

    let (exit, text, _) = run(&surface, &line(&["event-subscriptions"]), "");
    assert_eq!(exit, 0);
    assert!(text.contains("2 Event subscription(s), 1 paused"), "{text}");
    assert!(
        in_order(
            &text,
            &[
                "1",
                "operations",
                &cluster.name,
                first,
                "every Event",
                "active"
            ]
        ),
        "{text}"
    );
    assert!(!text.contains("0199a0a0"), "{text}");
    let unheard = format!(
        "{first}: not hearing {} since 2026-09-21T14:13:20Z: connection refused",
        cluster.node_scope(2)
    );
    assert!(text.contains(&unheard), "{text}");

    let location = cluster.node_scope(1);
    let (_, json, _) = run(
        &surface,
        &line(&["event-subscriptions", "--location", &location, "--json"]),
        "",
    );
    let read = document(&json);
    let listed = read["event_subscriptions"].as_array().expect("a list");
    assert_eq!(listed.len(), 1);
    let one = &listed[0];
    assert!(
        one["node"]
            .as_str()
            .is_some_and(|node| node.ends_with(&format!("/{second}")))
    );
    assert_eq!(one["paused"], true);
    assert_eq!(one["subscriber"], "on-call");
    assert_eq!(one["party"], "0199a0a0-0000-7000-8000-000000000002");
    assert!(read["unheard"].as_array().is_some_and(Vec::is_empty));
}

#[test]
fn an_act_names_one_subscription_or_is_refused_before_any_node_is_asked() {
    let (cluster, published) = published();
    let surface = published.surface();

    let (exit, _, error) = run(&surface, &line(&["event-subscriptions", "--pause"]), "");
    assert_eq!(exit, 2);
    assert!(error.starts_with("REFUSED"), "{error}");

    let first = cluster.node_scope(0);
    let (exit, _, error) = run(
        &surface,
        &line(&[
            "event-subscriptions",
            "--location",
            &first,
            "--id",
            "9",
            "--pause",
        ]),
        "",
    );
    assert_eq!(exit, 1);
    assert!(error.contains("no Event subscription 9"), "{error}");

    let second = cluster.node_scope(1);
    let (exit, _, error) = run(
        &surface,
        &line(&[
            "event-subscriptions",
            "--location",
            &second,
            "--id",
            "2",
            "--resume",
        ]),
        "",
    );
    assert_eq!(exit, 1);
    assert!(
        error.starts_with(&format!("REFUSED: {NOT_ACTED}")),
        "{error}"
    );
}
