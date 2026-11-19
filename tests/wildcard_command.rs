//! What each command does with a wildcard. What an argument selects is
//! `ScopeSelection`'s, shared with every surface (ADR-0059 clause 7;
//! ADR-0052 clause 1); what a command says over several scopes, and the
//! refusal as a document, is the executable's own and held here.

#[path = "support/fake.rs"]
mod fake;

use configure::fixture::test_cluster;
use fake::{FakeSurface, leaf};
use observe::{Counted, Health};
use operate::surface::acted::ScopeAction;
use operate::surface::scope_selection::ScopeSelection;
use serde_json::Value;
use xmip_core_cli::command::{health, measure, scope};
use xmip_core_cli::usage;

// The test cluster's first node, and one named as it is with a 2 after: the
// pattern First* selects the two of them and no other.
fn first() -> String {
    test_cluster().node_scope(0)
}

fn second() -> String {
    format!("{}2", first())
}

fn pattern() -> String {
    format!("{}*", first())
}

fn cluster() -> FakeSurface {
    let test = test_cluster();
    FakeSurface::new(vec![
        leaf(&format!("{}/receive/tcp", first()), Health::Fine, 0, ""),
        leaf(
            &format!("{}/receive/file", first()),
            Health::Stressed,
            55,
            "slow",
        ),
        leaf(&format!("{}/receive/tcp", second()), Health::Fine, 0, ""),
        leaf(
            &format!("{}/process/json", test.node_scope(1)),
            Health::Done,
            90,
            "refused",
        ),
        leaf(
            &format!("{}/send/tcp", test.node_scope(2)),
            Health::Fine,
            0,
            "",
        ),
    ])
}

fn chosen(surface: &FakeSurface) -> ScopeSelection {
    ScopeSelection::of(surface, &pattern()).expect("the pattern selects")
}

fn lines(text: &str) -> Vec<&str> {
    text.split('\n').filter(|line| !line.is_empty()).collect()
}

fn text(output: Vec<u8>) -> String {
    String::from_utf8(output).expect("UTF-8")
}

/// With `--json` the refusal is a document too, because a program that is
/// promised structure must not be handed prose.
#[test]
fn the_refusal_is_a_document_when_a_program_is_asking() {
    // Two node names run together: no node is named so.
    let test = test_cluster();
    let none = format!(
        "{}/node/{}{}*",
        test.scope(),
        test.node(0).name,
        test.node(1).name
    );
    let refusal = ScopeSelection::of(&cluster(), &none).expect_err("nothing matches");

    let document: Value = serde_json::from_str(&scope::unmatched(&none, &refusal)).expect("JSON");

    assert_eq!(document["pattern"], none.as_str());
    assert_eq!(document["matched"], 0);
    assert!(
        document["refused"]
            .as_str()
            .is_some_and(|refused| refused.starts_with("REFUSED"))
    );
}

#[test]
fn health_over_a_pattern_says_each_scope_in_turn_and_rolls_up_none_of_them_together() {
    let surface = cluster();
    let mut output = Vec::new();

    let exit = health::selected(
        &surface,
        &chosen(&surface),
        false,
        &mut output,
        &mut Vec::new(),
    );

    let said = text(output);
    let lines = lines(&said);
    assert_eq!(exit, 0);
    assert_eq!(lines[0], format!("holding        {}", first()));
    assert!(
        lines.contains(&format!("fine           {}", second()).as_str()),
        "{said}"
    );
}

#[test]
fn health_over_a_pattern_in_json_is_one_document_with_one_object_per_scope() {
    let surface = cluster();
    let mut output = Vec::new();

    health::selected(
        &surface,
        &chosen(&surface),
        true,
        &mut output,
        &mut Vec::new(),
    );

    let said = text(output);
    assert_eq!(said.matches('\n').count(), 1);
    let root: Value = serde_json::from_str(&said).expect("JSON");
    assert_eq!(root["pattern"], pattern().as_str());
    assert_eq!(root["matched"], 2);
    assert_eq!(root["scopes"][0]["scope"], first().as_str());
    assert_eq!(root["scopes"][0]["state"], "holding");
    assert_eq!(root["scopes"][1]["state"], "fine");
}

#[test]
fn measure_over_a_pattern_says_one_line_per_scope_and_adds_nothing_up() {
    let mut surface = cluster();
    surface.measurements.push((Counted::Streams, 7));
    let mut output = Vec::new();

    let exit = measure::selected(
        &surface,
        &chosen(&surface),
        false,
        &mut output,
        &mut Vec::new(),
    );

    let said = text(output);
    let lines = lines(&said);
    assert_eq!(exit, 0);
    assert_eq!(lines.len(), 2);
    assert!(
        lines[0].starts_with(&format!("{}  Streams 7", first())),
        "{said}"
    );
    assert!(
        lines[1].starts_with(&format!("{}  Streams 7", second())),
        "{said}"
    );
}

#[test]
fn measure_over_a_pattern_in_json_names_each_scope_and_its_figures() {
    let mut surface = cluster();
    surface.measurements.push((Counted::Streams, 7));
    let mut output = Vec::new();

    measure::selected(
        &surface,
        &chosen(&surface),
        true,
        &mut output,
        &mut Vec::new(),
    );

    let root: Value = serde_json::from_str(&text(output)).expect("JSON");
    assert_eq!(root["matched"], 2);
    assert_eq!(root["scopes"][1]["scope"], second().as_str());
    assert_eq!(root["scopes"][0]["streams"], 7);
}

#[test]
fn list_over_a_pattern_is_one_list_of_rows_that_name_their_own_scopes() {
    let surface = cluster();
    let mut output = Vec::new();

    let exit = scope::list(
        &surface,
        &chosen(&surface),
        false,
        &mut output,
        &mut Vec::new(),
    );

    let said = text(output);
    assert_eq!(exit, 0);
    assert!(said.contains(&format!("{}/receive", first())), "{said}");
    assert!(said.contains(&format!("{}/receive", second())), "{said}");
}

#[test]
fn show_over_a_pattern_is_one_row_per_scope_and_in_json_one_entry_each() {
    let surface = cluster();
    let mut output = Vec::new();
    let mut as_json = Vec::new();

    scope::show(
        &surface,
        &chosen(&surface),
        false,
        &mut output,
        &mut Vec::new(),
    );
    scope::show(
        &surface,
        &chosen(&surface),
        true,
        &mut as_json,
        &mut Vec::new(),
    );

    assert!(text(output).contains(&first()));
    let document: Value = serde_json::from_str(&text(as_json)).expect("JSON");
    assert_eq!(document["scopes"].as_array().map(Vec::len), Some(2));
    assert_eq!(document["scopes"][1]["scope"], second().as_str());
}

/// A pattern adds no reach: one act already reaches everything beneath the
/// scope it names (ADR-0027). What it adds is several subtrees at once, and
/// the exit says whether every one of them was applied.
#[test]
fn pause_over_a_pattern_acts_on_each_and_says_so_when_one_was_not_applied() {
    let surface = cluster();
    let mut error = Vec::new();

    let exit = scope::apply(
        &surface,
        &chosen(&surface),
        (ScopeAction::Pause, "ilian"),
        false,
        &mut Vec::new(),
        &mut error,
    );

    assert_eq!(exit, 1);
    assert_eq!(lines(&text(error)).len(), 2);
}

#[test]
fn pause_over_a_pattern_in_json_is_one_document_and_not_one_line_per_scope() {
    let surface = cluster();
    let mut output = Vec::new();

    scope::apply(
        &surface,
        &chosen(&surface),
        (ScopeAction::Pause, "ilian"),
        true,
        &mut output,
        &mut Vec::new(),
    );

    let said = text(output);
    assert_eq!(said.matches('\n').count(), 1);
    let document: Value = serde_json::from_str(&said).expect("JSON");
    assert_eq!(document["action"], "pause");
    assert_eq!(document["scopes"].as_array().map(Vec::len), Some(2));
    assert_eq!(document["scopes"][0]["applied"], false);
}

/// The help says the rule, because a convention nobody is told about is a
/// convention nobody uses.
#[test]
fn the_usage_says_that_a_scope_may_be_a_wildcard() {
    let text = usage::text();

    assert!(text.contains("wildcard"));
    assert!(text.contains("REFUSED"));
    let example = text.split("xmip health \"xmip:///").skip(1).any(|after| {
        after
            .split('"')
            .next()
            .is_some_and(|quoted| quoted.ends_with('*'))
    });
    assert!(example, "an example of a wildcard scope");
}
