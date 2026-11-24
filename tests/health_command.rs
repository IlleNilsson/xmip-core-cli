//! `xmip health` over a fake surface: worst first, Holding says why beside
//! the word, one document with `--json`, and JSON Lines only when something
//! changed with `--follow`.

#[path = "support/fake.rs"]
mod fake;

use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};

use configure::fixture::test_cluster;
use fake::{FakeSurface, SEEN, leaf};
use observe::{Health, Run};
use operate::surface::run_header::RunHeader;
use operate::surface::scope_selection::ScopeSelection;
use operate::surface::scope_tree::ROOT;
use serde_json::Value;
use xmip_core_cli::command::{follow, health};
use xmip_core_cli::time;

fn here() -> String {
    test_cluster().node_scope(0)
}

fn there() -> String {
    test_cluster().node_scope(1)
}

fn estate() -> FakeSurface {
    FakeSurface::new(vec![
        leaf(&format!("{}/receive/orders", here()), Health::Fine, 0, ""),
        leaf(
            &format!("{}/send/invoices", here()),
            Health::Done,
            90,
            "certificate expired",
        ),
        leaf(
            &format!("{}/receive/orders", there()),
            Health::Working,
            30,
            "",
        ),
    ])
}

fn run(surface: &FakeSurface, scope: &str, json: bool) -> (i32, String, String) {
    let (mut output, mut error) = (Vec::new(), Vec::new());
    let exit = health::run(surface, scope, json, &mut output, &mut error);
    (
        exit,
        String::from_utf8(output).expect("UTF-8"),
        String::from_utf8(error).expect("UTF-8"),
    )
}

#[test]
fn text_says_why_a_holding_scope_is_holding() {
    let (exit, output, _) = run(&estate(), ROOT, false);

    assert_eq!(exit, 0);
    let lines: Vec<&str> = output.split('\n').collect();
    assert_eq!(lines[0], "holding        xmip:///");
    assert_eq!(
        lines[1],
        format!("               done at {}/send/invoices", here())
    );
    assert_eq!(lines[2], "               certificate expired");
}

#[test]
fn text_lists_leaves_worst_first_in_aligned_columns() {
    let (_, output, _) = run(&estate(), ROOT, false);

    let lines: Vec<&str> = output.split('\n').collect();
    let first = lines
        .iter()
        .position(|line| line.is_empty())
        .expect("a blank line")
        + 1;
    assert_eq!(
        lines[first],
        format!("done       90  {}/send/invoices", here())
    );
    assert_eq!(lines[first + 1], "               certificate expired");
    assert_eq!(
        lines[first + 2],
        format!("               observed {}", time::round_trip(SEEN))
    );
    assert_eq!(
        lines[first + 3],
        format!("working    30  {}/receive/orders", there())
    );
}

/// The run reaches the command line too (ADR-0014, amendment 2026-09-19):
/// the same line the GUI puts at the top of every view, naming each node
/// with what it declared it can do (ADR-0056). A surface with no run says
/// nothing of one, in text and in JSON alike.
#[test]
fn health_says_what_the_run_was_started_with_where_a_surface_says_one() {
    let cluster = test_cluster();
    let receiver = cluster.with_role("receiving").name.clone();
    let processor = cluster.with_role("processing").name.clone();
    let mut surface = estate();
    surface.started = RunHeader::from(Some(&Run {
        cluster: cluster.name.clone(),
        tests: vec!["RoundTrip".into()],
        nodes: vec![receiver.clone(), processor.clone()],
        roles: vec![
            format!("{receiver}=receiving"),
            format!("{processor}=processing+sending"),
        ],
        capabilities: vec![format!("{receiver}=online")],
        stress: "calm".into(),
        ..Run::default()
    }));
    let line = format!(
        "RoundTrip · {} · nodes {receiver}=receiving {processor}=processing+sending · Node \
         capabilities {receiver}=online · calm",
        cluster.name
    );

    let (_, text, _) = run(&surface, ROOT, false);
    assert!(
        text.contains(&format!("               run {line}")),
        "{text}"
    );

    let (_, json, _) = run(&surface, ROOT, true);
    let document: Value = serde_json::from_str(&json).expect("JSON");
    assert_eq!(document["run"], line.as_str());

    let (_, silent, _) = run(&estate(), ROOT, true);
    let nothing: Value = serde_json::from_str(&silent).expect("JSON");
    assert!(nothing.get("run").is_none());
}

#[test]
fn a_fine_scope_says_nothing_more() {
    let (_, output, _) = run(&estate(), &format!("{}/receive", here()), false);

    let lines: Vec<&str> = output.split('\n').collect();
    assert_eq!(lines[0], format!("fine           {}/receive", here()));
    assert!(
        lines[1].starts_with("               source "),
        "{}",
        lines[1]
    );
}

#[test]
fn json_is_one_document_with_the_rollup_the_worst_and_every_leaf() {
    let (exit, text, _) = run(&estate(), ROOT, true);

    assert_eq!(exit, 0);
    assert_eq!(text.matches('\n').count(), 1);
    let root: Value = serde_json::from_str(&text).expect("JSON");
    assert_eq!(root["state"], "holding");
    assert_eq!(root["worst"]["evidence"], "certificate expired");
    assert_eq!(root["records"].as_array().map(Vec::len), Some(3));
    assert_eq!(root["records"][0]["state"], "done");
}

#[test]
fn nothing_beneath_a_scope_is_a_complaint_on_stderr() {
    let scope = test_cluster().node_scope(2);

    let (exit, _, error) = run(&estate(), &scope, false);

    assert_eq!(exit, 1);
    assert!(error.contains(&scope), "{error}");
}

#[test]
fn follow_emits_a_line_only_when_health_changes() {
    // Five synthetic publication notices: the first emits, the second and
    // third repeat and emit nothing, the fourth sees the change, and the
    // fifth stops the follow.
    let stop = Arc::new(AtomicBool::new(false));
    let stopping = Arc::clone(&stop);
    let calm = leaf(&format!("{}/receive/orders", here()), Health::Fine, 0, "");
    let mut surface = estate();
    surface.on_read = Some(Box::new(move |reads, records| {
        if reads == 4 {
            *records.lock().expect("records") = vec![calm.clone()];
        }
        if reads == 5 {
            stopping.store(true, Ordering::Release);
        }
    }));
    let mut output = Vec::new();

    let exit = follow::run(
        &surface,
        &ScopeSelection::exactly(ROOT),
        &health::answer,
        &mut output,
        &stop,
    );

    assert_eq!(exit, 0);
    let text = String::from_utf8(output).expect("UTF-8");
    let lines: Vec<&str> = text.split('\n').filter(|line| !line.is_empty()).collect();
    assert_eq!(lines.len(), 2, "{text}");
    assert!(lines[0].contains("\"state\":\"holding\""), "{}", lines[0]);
    assert!(lines[1].contains("\"state\":\"fine\""), "{}", lines[1]);
}
