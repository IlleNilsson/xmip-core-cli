//! What a line states about the surface to read, carried as the operator
//! typed it, over the executable's own document.

use configure::fixture::test_cluster;
use operate::surface::choice::SurfaceLine;
use xmip_core_cli::invocation::Invocation;
use xmip_core_cli::surface_open;

fn parsed(words: &[&str]) -> Result<Invocation, String> {
    Invocation::parse(&words.iter().map(ToString::to_string).collect::<Vec<_>>())
}

#[test]
fn a_line_with_no_surface_option_states_nothing() {
    assert_eq!(
        parsed(&["measure"]).expect("parsed").line(),
        SurfaceLine::default()
    );
}

#[test]
fn each_surface_option_is_carried_as_the_operator_typed_it() {
    let cluster = test_cluster();
    let snapshot = format!("{}-snapshot.xmip", cluster.name);
    let scope = cluster.scope();
    let stated = parsed(&[
        "show",
        &scope,
        "--remote",
        "elsewhere:7401",
        "--snapshot",
        &snapshot,
        "--local",
        "xmip-node1",
    ])
    .expect("parsed")
    .line();

    assert_eq!(
        stated,
        SurfaceLine {
            remote: Some("elsewhere:7401".into()),
            local: Some("xmip-node1".into()),
            snapshot: Some(snapshot.clone()),
        }
    );
}

#[test]
fn a_snapshot_with_no_path_is_refused_at_the_line() {
    assert_eq!(
        parsed(&["measure", "--snapshot"]),
        Err("--snapshot needs a path.".to_string())
    );
}

#[test]
fn the_document_is_the_executable_s_own() {
    assert_eq!(surface_open::CONFIGURATION_FILE, "cli.toml");
}
