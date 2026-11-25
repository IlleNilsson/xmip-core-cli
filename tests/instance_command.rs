//! `xmip instances`: the line names a Work Process and where, and opens one
//! Instance with `--instance`; the list says each Instance's state, what it
//! waits for, its keys and values, and the Messages that failed in its
//! queue with the act that retries one, as text and as a document; an
//! Instance takes no act (ADR-0066, amendment 2026-11-25; open problem 192).

#[path = "support/noun.rs"]
mod noun_support;

use configure::fixture::{TestCluster, test_cluster};
use xmip_core_cli::command::Command;

use noun_support::{Published, document, in_order, line, refused, run};

/// The test cluster's first node runs `Order in parts`: one Instance
/// waiting for the rest of its order, one failed by a Message that waits in
/// its queue.
fn published() -> (TestCluster, Published) {
    let cluster = test_cluster();
    let text = format!(
        "node = \"{}\"\n\
         [[work_process_instances]]\n\
         node = \"{}\"\n\
         work_process = \"Order in parts\"\n\
         failed_count = 1\n\
         [[work_process_instances.instances]]\n\
         instance = \"i-1\"\n\
         state = \"waiting\"\n\
         waits_for = \"message or time\"\n\
         until_unix_nanos = 1789128000000000000\n\
         correlations = [[\"Order\", \"A-1001\"]]\n\
         values = [[\"Ordered\", \"3\"], [\"Delivered\", \"1\"]]\n\
         turns = 2\n\
         updated_unix_nanos = 5\n\
         [[work_process_instances.instances.history]]\n\
         at_unix_nanos = 1789127000000000000\n\
         what = \"started\"\n\
         message = \"m-1\"\n\
         journey = \"j-0\"\n\
         [[work_process_instances.instances]]\n\
         instance = \"i-2\"\n\
         state = \"failed\"\n\
         code = \"offline\"\n\
         reason = \"the unit cannot reach its stock\"\n\
         failed_by = \"j-1\"\n\
         turns = 4\n\
         updated_unix_nanos = 4\n\
         [[work_process_instances.failed]]\n\
         journey = \"j-1\"\n\
         sequence = 7\n\
         reason = \"Order in parts threw offline: the unit cannot reach its stock\"\n",
        cluster.scope(),
        cluster.node_scope(0),
    );
    let published = Published::new("instances", &cluster.name, &text);
    (cluster, published)
}

#[test]
fn the_line_names_the_work_process_where_and_the_instance_and_takes_no_act() {
    let cluster = test_cluster();
    let location = cluster.node_scope(0);
    let parsed = line(&[
        "instances",
        "Order in parts",
        "--location",
        &location,
        "--instance",
        "i-1",
        "--json",
    ]);
    assert_eq!(parsed.command, Command::Instances);
    let asked = parsed.noun.expect("a query");
    assert_eq!(asked.pattern.as_deref(), Some("Order in parts"));
    assert_eq!(asked.location.as_deref(), Some(location.as_str()));
    assert_eq!(asked.selectors.instance.as_deref(), Some("i-1"));
    assert!(parsed.json);

    assert_eq!(
        refused(&["instances", "--retry"]),
        "--retry only applies to 'message'."
    );
    assert!(refused(&["instances", "--pause"]).contains("an Instance takes no act"));
    assert_eq!(
        refused(&["message", "--instance", "i-1"]),
        "--instance only applies to 'instances'."
    );
    assert!(refused(&["instances", "--sort", "state"]).contains("only applies to 'audit'"));
}

#[test]
fn the_list_says_each_instance_and_the_failed_messages_with_the_act_that_retries_one() {
    let (cluster, published) = published();
    let surface = published.surface();
    let (exit, output, error) = run(&surface, &line(&["instances"]), "");
    assert_eq!(exit, 0, "{error}");
    let node = cluster.node_scope(0);
    assert!(
        output.contains(&format!(
            "{node} Work Process Order in parts: 2 Instance(s), 1 failed in its queue"
        )),
        "{output}"
    );
    assert!(
        in_order(&output, &["i-1", "waiting", "message or time"]),
        "{output}"
    );
    assert!(
        in_order(&output, &["Order A-1001", "Ordered 3, Delivered 1", "2"]),
        "{output}"
    );
    assert!(in_order(&output, &["FAILED", "j-1", "place 7"]), "{output}");
    assert!(
        output.contains(&format!("xmip message j-1 --retry --location {node}")),
        "{output}"
    );

    let other = cluster.node_scope(1);
    let (_, elsewhere, _) = run(&surface, &line(&["instances", "--location", &other]), "");
    assert!(elsewhere.starts_with("No Work Process runs"), "{elsewhere}");
}

#[test]
fn the_list_as_a_document_and_one_instance_opened_with_its_history() {
    let (_, published) = published();
    let surface = published.surface();
    let (exit, output, _) = run(
        &surface,
        &line(&["instances", "Order in parts", "--json"]),
        "",
    );
    assert_eq!(exit, 0);
    let read = document(&output);
    assert_eq!(read["matched"], 1);
    let list = &read["work_process_instances"][0];
    assert_eq!(list["failed_count"], 1);
    assert_eq!(list["failed"][0]["journey"], "j-1");
    assert_eq!(list["instances"][1]["state"], "failed");
    assert_eq!(list["instances"][0]["values"][1]["value"], "1");

    let (exit, opened, _) = run(&surface, &line(&["instances", "--instance", "i-1"]), "");
    assert_eq!(exit, 0);
    assert!(
        opened.contains("Instance i-1 of Order in parts"),
        "{opened}"
    );
    assert!(
        opened.contains("waits for a message or time until"),
        "{opened}"
    );
    assert!(opened.contains("history:"), "{opened}");
    assert!(in_order(&opened, &["started", "m-1", "j-0"]), "{opened}");

    let (exit, failed, _) = run(&surface, &line(&["instances", "--instance", "i-2"]), "");
    assert_eq!(exit, 0);
    assert!(
        failed.contains("threw offline: the unit cannot reach its stock"),
        "{failed}"
    );
    assert!(failed.contains("xmip message j-1 --retry"), "{failed}");

    let (exit, _, error) = run(&surface, &line(&["instances", "--instance", "none"]), "");
    assert_eq!(exit, 1);
    assert!(error.starts_with("REFUSED: no Instance none"), "{error}");
}
