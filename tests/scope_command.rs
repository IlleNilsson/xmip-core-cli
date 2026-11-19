//! `xmip list`, `show`, `pause` and `resume` over a fake surface: a row per
//! child with its rolled mood and figures, the evidence beneath a row and
//! the leaf to drill to, what a node is for, and a refused pause on stderr.

#[path = "support/fake.rs"]
mod fake;

use configure::fixture::test_cluster;
use fake::{FakeSurface, leaf};
use observe::Health;
use operate::surface::acted::ScopeAction;
use operate::surface::node_capability;
use serde_json::Value;
use xmip_core_cli::command::scope;

fn text(output: Vec<u8>) -> String {
    String::from_utf8(output).expect("UTF-8")
}

fn listed(surface: &FakeSurface, at: &str, json: bool) -> (i32, String) {
    let mut output = Vec::new();
    let exit = scope::list_scope(surface, at, json, &mut output, &mut std::io::sink());
    (exit, text(output))
}

fn shown(surface: &FakeSurface, at: &str, json: bool) -> (i32, String) {
    let mut output = Vec::new();
    let exit = scope::show_scope(surface, at, json, &mut output, &mut std::io::sink());
    (exit, text(output))
}

#[test]
fn list_shows_one_row_per_child_with_its_rolled_mood_and_figures() {
    let cluster = test_cluster();
    let surface = FakeSurface::new(vec![
        leaf(
            &format!("{}/receive/orders", cluster.node_scope(0)),
            Health::Fine,
            0,
            "",
        ),
        leaf(
            &format!("{}/send/billing", cluster.node_scope(1)),
            Health::Working,
            0,
            "",
        ),
    ]);

    let (exit, output) = listed(&surface, &format!("{}/node", cluster.scope()), false);

    assert_eq!(exit, 0);
    assert!(
        output.contains(&format!("fine       {}", cluster.node_scope(0))),
        "{output}"
    );
    // A node is a container: a Working leaf beneath it rolls up as Holding
    // (ADR-0041), and the row says so.
    assert!(
        output.contains(&format!("holding    {}", cluster.node_scope(1))),
        "{output}"
    );
    assert!(output.contains("Retrying –"), "{output}");
}

#[test]
fn show_says_the_evidence_beneath_the_row() {
    let cluster = test_cluster();
    let at = format!("{}/receive/orders", cluster.node_scope(0));
    let surface = FakeSurface::new(vec![leaf(&at, Health::Holding, 70, "disk full")]);

    let (exit, output) = shown(&surface, &at, false);

    assert_eq!(exit, 0);
    assert!(output.contains(&format!("holding    {at}")), "{output}");
    assert!(output.contains("  disk full"), "{output}");
}

/// A row that is not fine says which leaf explains it, so the next scope to
/// type is on the screen. Until 2026-09-26 `list` said "holding" and nothing
/// else, and `show` said the evidence and never whose it was.
#[test]
fn a_row_that_is_not_fine_names_the_leaf_to_drill_to() {
    let cluster = test_cluster();
    let node = cluster.node_scope(0);
    let surface = FakeSurface::new(vec![
        leaf(&format!("{node}/receive/http/json"), Health::Fine, 0, ""),
        leaf(
            &format!("{node}/receive/sftp/xml"),
            Health::Done,
            90,
            "refused: key",
        ),
    ]);
    let worst = format!("  worst {node}/receive/sftp/xml: refused: key");

    let (_, list) = listed(&surface, &format!("{}/node", cluster.scope()), false);
    let (_, show) = shown(&surface, &cluster.scope(), false);

    assert!(list.contains(&worst), "{list}");
    assert!(show.contains(&worst), "{show}");
}

/// A node's row says what the node is for, a role a line beneath it, from
/// what it published (the owner, 2026-11-12: *what are nodes purpose*), and
/// in `--json` as its `purpose`; a row that is not a node's has none.
#[test]
fn a_node_s_row_says_what_the_node_is_for() {
    let cluster = test_cluster();
    let node = cluster.with_role("receiving").name.clone();
    let at = format!("{}/node/{node}", cluster.scope());
    let surface = FakeSurface::new(vec![
        leaf(
            &format!("{at}/capability"),
            Health::Fine,
            0,
            "declares receiving; online; x",
        ),
        leaf(&format!("{at}/receive/orders"), Health::Fine, 0, ""),
    ]);
    let purpose = node_capability::purpose(&["receiving"]);

    let (_, list) = listed(&surface, &format!("{}/node", cluster.scope()), false);
    let (_, show) = shown(&surface, &at, false);
    let (_, json) = shown(&surface, &at, true);
    let (_, stage) = shown(&surface, &format!("{at}/receive"), true);

    let lines: Vec<&str> = list.split('\n').collect();
    let row = lines
        .iter()
        .position(|line| line.contains(&at))
        .expect("the node's row");
    assert_eq!(lines[row + 1], format!("  {purpose}"));
    assert!(show.contains(&format!("  {purpose}")), "{show}");
    let said: Value = serde_json::from_str(&json).expect("JSON");
    assert_eq!(said["purpose"], purpose.as_str());
    let beneath: Value = serde_json::from_str(&stage).expect("JSON");
    assert!(beneath.get("purpose").is_none());
}

#[test]
fn a_refused_pause_goes_to_stderr_with_exit_one() {
    let surface = FakeSurface::new(Vec::new());
    let mut error = Vec::new();

    let exit = scope::apply_scope(
        &surface,
        &test_cluster().scope(),
        (ScopeAction::Pause, "test"),
        false,
        &mut std::io::sink(),
        &mut error,
    );

    assert_eq!(exit, 1);
    assert!(text(error).contains("a fake cannot be paused"));
}
