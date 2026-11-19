//! `xmip subscriptions`: the line is the query and the act an option; the
//! list says each Subscription as text and as a document; an act names one
//! or is refused before any node is asked; an act its node has not taken
//! yet is said ASKED and is no failure.

#[path = "support/fake.rs"]
mod fake;
#[path = "support/noun.rs"]
mod noun_support;

use configure::fixture::{TestCluster, test_cluster};
use observe::{Act, PauseState, Subscription};
use operate::surface::acted::ActOutcome;
use operate::surface::snapshot::NOT_ACTED;
use xmip_core_cli::command::Command;
use xmip_core_cli::invocation::noun_arguments::CONFIGURED;

use fake::FakeSurface;
use noun_support::{Published, document, in_order, line, refused, run};

fn entry(
    cluster: &TestCluster,
    node: &str,
    (name, state): (&str, &str),
    counted: (u32, u32),
) -> String {
    format!(
        "[[subscriptions]]\nnode = \"{}/node/{node}\"\nname = \"{name}\"\n\
         application = \"RoundTrip\"\nfilter = \"MessageType = '{name}'\"\n\
         destination = \"the Send Port 'RoundTripOut'\"\nstate = \"{state}\"\n\
         picked_up = {}\nheld = {}\n",
        cluster.scope(),
        counted.0,
        counted.1
    )
}

// The test cluster's first two nodes, by place: the first routes by two
// Subscriptions, the second by one.
fn published() -> (TestCluster, Published) {
    let cluster = test_cluster();
    let (first, second) = (cluster.node(0).name.clone(), cluster.node(1).name.clone());
    let text = format!(
        "node = \"{}\"\n{}{}{}",
        cluster.scope(),
        entry(&cluster, &first, ("structured", "active"), (12, 0)),
        entry(&cluster, &first, ("edi", "paused"), (3, 9)),
        entry(&cluster, &second, ("flat", "active"), (40, 0)),
    );
    let published = Published::new("subscriptions", &cluster.name, &text);
    (cluster, published)
}

#[test]
fn the_line_is_the_query_and_the_act_an_option() {
    let cluster = test_cluster();
    let first = &cluster.node(0).name;
    let pattern = format!("*/{first}");
    let location = cluster.node_scope(0);
    let parsed = line(&[
        "subscriptions",
        &pattern,
        "--location",
        &location,
        "--name",
        "edi",
        "--sort",
        "held",
        "--order",
        "descending",
        "--resume",
        "--who",
        "ilian",
    ]);

    assert_eq!(parsed.command, Command::Subscriptions);
    let asked =
        xmip_core_cli::command::noun::subscription::query(&parsed.noun.clone().expect("a query"));
    assert_eq!(asked.pattern.as_deref(), Some(pattern.as_str()));
    assert_eq!(asked.location.as_deref(), Some(location.as_str()));
    assert_eq!(asked.name.as_deref(), Some("edi"));
    assert_eq!(asked.sort.as_deref(), Some("held"));
    assert_eq!(asked.order.as_deref(), Some("descending"));
    assert_eq!(parsed.act, Some(Act::Resume));
    assert_eq!(parsed.who.as_deref(), Some("ilian"));
}

#[test]
fn a_line_that_cannot_be_obeyed_is_said_so() {
    for (said, expected) in [
        ("subscriptions --pause --resume", "one act"),
        ("subscriptions --name", "--name needs"),
        (
            "subscriptions --id 3",
            "--id only applies to 'event-subscriptions'",
        ),
        (
            "event-subscriptions --name edi",
            "--name only applies to 'subscriptions'",
        ),
        (
            "list --pause",
            "--pause only applies to 'event-subscriptions',",
        ),
        (
            "subscriptions --program xmip",
            "--program only applies to 'audit'",
        ),
        ("subscriptions --who ilian", "--who only applies"),
    ] {
        let words: Vec<&str> = said.split(' ').collect();
        let problem = refused(&words);
        assert!(problem.contains(expected), "{said}: {problem}");
    }
}

#[test]
fn remove_is_refused_in_words_because_the_configuration_adds_and_removes_it() {
    let cluster = test_cluster();
    let location = cluster.node_scope(0);
    let problem = refused(&[
        "subscriptions",
        "--location",
        &location,
        "--name",
        "edi",
        "--remove",
    ]);

    assert!(problem.starts_with("REFUSED"), "{problem}");
    assert!(problem.contains(CONFIGURED), "{problem}");
}

#[test]
fn the_list_says_name_cluster_node_filter_and_what_is_held_as_text_and_as_a_document() {
    let (cluster, published) = published();
    let surface = published.surface();
    let first = &cluster.node(0).name;

    let (exit, text, _) = run(&surface, &line(&["subscriptions"]), "");
    assert_eq!(exit, 0);
    assert!(
        text.contains("3 Subscription(s), 1 paused, 9 held"),
        "{text}"
    );
    assert!(
        in_order(
            &text,
            &[
                "edi",
                &cluster.name,
                first,
                "MessageType = 'edi'",
                "the Send Port"
            ]
        ),
        "{text}"
    );

    let location = cluster.node_scope(0);
    let (_, json, _) = run(
        &surface,
        &line(&["subscriptions", "--location", &location, "--json"]),
        "",
    );
    let read = document(&json);
    let listed = read["subscriptions"].as_array().expect("a list");
    assert_eq!(listed.len(), 2);
    assert_eq!(listed[0]["name"], "edi");
    assert_eq!(listed[0]["paused"], true);
    assert_eq!(listed[0]["held"], 9);
    assert_eq!(listed[0]["application"], "RoundTrip");
}

#[test]
fn the_pattern_drills_and_the_sort_orders() {
    let (_, published) = published();
    let surface = published.surface();

    let (_, drilled, _) = run(
        &surface,
        &line(&["subscriptions", "*/subscription/fl*", "--json"]),
        "",
    );
    let one = document(&drilled);
    let listed = one["subscriptions"].as_array().expect("a list");
    assert_eq!(listed.len(), 1);
    assert_eq!(listed[0]["name"], "flat");

    let (_, sorted, _) = run(
        &surface,
        &line(&[
            "subscriptions",
            "--sort",
            "picked-up",
            "--order",
            "descending",
            "--json",
        ]),
        "",
    );
    let all = document(&sorted);
    let names: Vec<&str> = all["subscriptions"]
        .as_array()
        .expect("a list")
        .iter()
        .map(|entry| entry["name"].as_str().expect("a name"))
        .collect();
    assert_eq!(names, ["flat", "structured", "edi"]);

    let (exit, _, error) = run(&surface, &line(&["subscriptions", "--sort", "queued"]), "");
    assert_eq!(exit, 2);
    assert!(error.starts_with("REFUSED"), "{error}");
}

#[test]
fn an_act_names_one_subscription_or_is_refused_before_any_node_is_asked() {
    let (cluster, published) = published();
    let surface = published.surface();

    let (exit, _, error) = run(&surface, &line(&["subscriptions", "--pause"]), "");
    assert_eq!(exit, 2);
    assert!(error.starts_with("REFUSED"), "{error}");

    let second = cluster.node_scope(1);
    let (exit, _, error) = run(
        &surface,
        &line(&[
            "subscriptions",
            "--location",
            &second,
            "--name",
            "edi",
            "--pause",
        ]),
        "",
    );
    assert_eq!(exit, 1);
    assert!(error.contains("no Subscription 'edi'"), "{error}");

    let first = cluster.node_scope(0);
    let (exit, _, error) = run(
        &surface,
        &line(&[
            "subscriptions",
            "--location",
            &first,
            "--name",
            "edi",
            "--resume",
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

#[test]
fn an_act_its_node_did_not_take_yet_is_said_asked_and_is_no_failure() {
    // ADR-0014, amendment 2026-11-13: the door wrote it to Xmip Storage and
    // its node takes it as soon as it runs.
    let cluster = test_cluster();
    let node = cluster.node_scope(0);
    let first = &cluster.node(0).name;
    let sentence = format!("asked of {first}; not taken yet; {first} takes it as soon as it runs");
    let orders = Subscription {
        node: node.clone(),
        name: "edi".into(),
        application: "RoundTrip".into(),
        filter: "true".into(),
        destination: "the Send Port 'Out'".into(),
        state: PauseState::Active,
        ..Subscription::default()
    };
    let mut surface = FakeSurface::new(Vec::new());
    surface.listed = vec![orders];
    surface.acted = (ActOutcome::Asked, sentence.clone());

    let (exit, said, error) = run(
        &surface,
        &line(&[
            "subscriptions",
            "--location",
            &node,
            "--name",
            "edi",
            "--pause",
        ]),
        "",
    );
    assert_eq!(exit, 0);
    assert_eq!(error, "");
    assert!(said.starts_with(&format!("ASKED. {sentence}")), "{said}");

    let (exit, said, _) = run(
        &surface,
        &line(&[
            "subscriptions",
            "--location",
            &node,
            "--name",
            "edi",
            "--pause",
            "--json",
        ]),
        "",
    );
    assert_eq!(exit, 0);
    let read = document(&said);
    assert_eq!(read["outcome"], "asked");
    assert_eq!(read["applied"], false);
}
