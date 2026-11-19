//! `xmip message`: the Journey is the argument, its node the location and
//! the act an option; no Journey and no act lists the Messages that failed,
//! paged; a list says plainly whether none failed or nothing is known; an
//! act without its node is refused before any node is asked; a snapshot
//! declines an act as text and as a document.

#[path = "support/fake.rs"]
mod fake;
#[path = "support/noun.rs"]
mod noun_support;

use configure::fixture::test_cluster;
use observe::Act;
use operate::surface::failed_message_list::FailedMessageList;
use operate::surface::snapshot::NOT_ACTED;
use xmip_core_cli::command::Command;

use fake::{FakeSurface, SOURCE};
use noun_support::{Published, document, line, refused, run};

struct Sending {
    node: String,
    port: String,
    published: Published,
}

// The test cluster's sending node, by what it declares, and a Send Port on
// it.
fn published() -> Sending {
    let cluster = test_cluster();
    let node = format!(
        "{}/node/{}",
        cluster.scope(),
        cluster.with_role("sending").name
    );
    let port = format!("{node}/send/invoices");
    let text = format!(
        "node = \"{}\"\n\n[[failed_messages]]\nnode = \"{node}\"\nsend_port = \"invoices\"\n\
         count = 2\nblocked = true\n\n[[failed_messages.messages]]\njourney = \"j-1\"\n\
         sequence = 3\nreason = \"invoices: the far end refused it\"\n\n\
         [[failed_messages.messages]]\njourney = \"j-2\"\nsequence = 7\n\
         reason = \"invoices: the far end refused it again\"\n",
        cluster.scope()
    );
    let published = Published::new("message", &cluster.name, &text);

    Sending {
        node,
        port,
        published,
    }
}

#[test]
fn the_journey_is_the_argument_its_node_the_location_and_the_act_an_option() {
    let sending = published();
    let parsed = line(&[
        "message",
        "j-1",
        "--location",
        &sending.port,
        "--dismiss",
        "--who",
        "ilian",
    ]);

    assert_eq!(parsed.command, Command::Message);
    assert_eq!(parsed.argument, "j-1");
    assert_eq!(parsed.location.as_deref(), Some(sending.port.as_str()));
    assert_eq!(parsed.act, Some(Act::Dismiss));
    assert_eq!(parsed.who.as_deref(), Some("ilian"));
    assert!(parsed.noun.is_none());
    assert_eq!(line(&["message", "j-1", "--retry"]).act, Some(Act::Retry));
}

#[test]
fn a_line_that_cannot_be_obeyed_is_said_so() {
    for (said, expected) in [
        ("message j-1", "needs an act: --retry or --dismiss"),
        ("message --retry", "to retry a Message, name its Journey"),
        ("message j-1 j-2 --retry", "takes at most one argument"),
        (
            "message j-1 --replay",
            "--replay only applies to 'dead-messages'",
        ),
        ("message j-1 --pause", "--retry and --dismiss are its acts"),
        ("message j-1 --retry --dismiss", "one act"),
        (
            "message j-1 --retry --sort node",
            "--sort only applies to 'audit'",
        ),
        (
            "message j-1 --retry --message m-1",
            "--message only applies to 'dead-messages'",
        ),
        ("dead-messages --retry", "--retry only applies to 'message'"),
        (
            "subscriptions --dismiss",
            "--dismiss only applies to 'message'",
        ),
        (
            "event-subscriptions --retry",
            "--retry only applies to 'message'",
        ),
        ("health * --dismiss", "--dismiss only applies to 'message'"),
    ] {
        let words: Vec<&str> = said.split(' ').collect();
        let problem = refused(&words);
        assert!(problem.contains(expected), "{said}: {problem}");
    }
}

#[test]
fn no_message_and_no_act_lists_the_messages_that_failed_paged_by_offset_and_limit() {
    let sending = published();
    let parsed = line(&[
        "message",
        "--location",
        &sending.port,
        "--offset",
        "4",
        "--limit",
        "1",
    ]);
    assert_eq!(parsed.command, Command::Message);
    assert_eq!(parsed.act, None);
    assert_eq!((parsed.from, parsed.most), (4, 1));

    let surface = sending.published.surface();
    let (exit, said, _) = run(
        &surface,
        &line(&["message", "--location", &sending.port]),
        "",
    );
    assert_eq!(exit, 0);
    assert!(
        said.contains("Send Port invoices: 2 failed in its queue"),
        "{said}"
    );
    assert!(
        said.contains("j-1  place 3  invoices: the far end refused it"),
        "{said}"
    );
    assert!(said.contains("j-2"), "{said}");
    assert!(
        said.contains("BLOCKED: every Message of its key waits"),
        "{said}"
    );

    let (_, paged, _) = run(
        &surface,
        &line(&[
            "message",
            "--location",
            &sending.node,
            "--offset",
            "4",
            "--limit",
            "1",
            "--json",
        ]),
        "",
    );
    let read = document(&paged);
    let port = &read["failed_messages"][0];
    assert_eq!(port["send_port"], "invoices");
    assert_eq!(port["count"], 2);
    assert_eq!(port["blocked"], true, "a blocked key is loud");
    let journeys = port["messages"].as_array().expect("messages");
    assert_eq!(journeys.len(), 1);
    assert_eq!(journeys[0]["journey"], "j-2");
}

#[test]
fn a_list_says_plainly_whether_none_failed_or_nothing_is_known() {
    let sending = published();
    let port = sending.port.clone();
    let listed = |surface: &dyn operate::surface::OperatorSurface, json: bool| {
        let mut words = vec!["message", "--location", port.as_str()];
        if json {
            words.push("--json");
        }
        run(surface, &line(&words), "")
    };

    // Answered, and none failing.
    let cluster = test_cluster();
    let quiet = sending.published.beside(
        "quiet-snapshot.xmip",
        &format!("node = \"{}\"\n", cluster.scope()),
    );
    let quiet = operate::surface::snapshot::SnapshotSurface::new(
        quiet,
        std::sync::Arc::new(xcore::SystemClock),
    );
    let (exit, said, error) = listed(&quiet, false);
    assert_eq!((exit, error.as_str()), (0, ""));
    assert_eq!(
        said.trim(),
        format!("No Message that failed waits at or beneath {port}.")
    );

    // A surface that cannot list them: not known, and not "none".
    let unlisted = FakeSurface::new(Vec::new());
    let (exit, said, error) = listed(&unlisted, false);
    assert_eq!((exit, said.as_str()), (1, ""));
    assert_eq!(
        error.trim(),
        format!(
            "NOT LISTED: {SOURCE} cannot list the Messages that failed, so whether any wait at \
             or beneath {port} is not known — not that none do."
        )
    );

    // Asked, and Xmip Storage did not answer: what it was told.
    let mut unanswered = FakeSurface::new(Vec::new());
    unanswered.failed = FailedMessageList::unanswered("Xmip Storage is not open");
    let (exit, _, error) = listed(&unanswered, false);
    assert_eq!(
        (exit, error.trim()),
        (1, "FAILED: Xmip Storage is not open")
    );
    let (exit, said, _) = listed(&unanswered, true);
    let read = document(&said);
    assert_eq!(exit, 1);
    assert_eq!(read["listed"], false);
    assert_eq!(read["failure"], "FAILED: Xmip Storage is not open");
}

#[test]
fn an_act_without_its_node_is_refused_before_any_node_is_asked() {
    let sending = published();
    let surface = sending.published.surface();
    let cluster = test_cluster();

    let (exit, _, error) = run(&surface, &line(&["message", "j-1", "--retry"]), "");
    assert_eq!(exit, 2);
    assert!(error.starts_with("REFUSED"), "{error}");

    let scope = cluster.scope();
    let (exit, _, error) = run(
        &surface,
        &line(&["message", "j-1", "--location", &scope, "--dismiss"]),
        "",
    );
    assert_eq!(exit, 2);
    assert!(error.starts_with("REFUSED"), "{error}");
}

#[test]
fn a_snapshot_declines_an_act_as_text_and_as_a_document() {
    let sending = published();
    let surface = sending.published.surface();

    let (exit, _, error) = run(
        &surface,
        &line(&[
            "message",
            "j-1",
            "--location",
            &sending.port,
            "--retry",
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

    let (exit, json, _) = run(
        &surface,
        &line(&[
            "message",
            "j-1",
            "--location",
            &sending.node,
            "--dismiss",
            "--json",
        ]),
        "",
    );
    assert_eq!(exit, 1);
    let read = document(&json);
    assert_eq!(read["node"], sending.node.as_str());
    assert_eq!(read["journey"], "j-1");
    assert_eq!(read["act"], "dismiss");
    assert_eq!(read["applied"], false);
    assert_eq!(read["outcome"], "refused");
}
