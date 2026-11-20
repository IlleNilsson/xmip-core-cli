//! What a line states about the surface to read, carried as the operator
//! typed it, over the executable's own document.

use configure::fixture::test_cluster;
use operate::surface::choice::SurfaceLine;
use xmip_core_cli::invocation::Invocation;

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
    assert_eq!(operate::surface::choice::table::COMMAND_LINE, "cli.toml");
}

/// A link to the program, as a package puts `/usr/bin/xmip` on the path,
/// finds the document beside the program it names, as an install wrote it.
#[cfg(unix)]
#[test]
fn a_link_to_the_program_finds_the_document_beside_the_program() {
    use operate::surface::choice::table::{self, COMMAND_LINE};
    use operate::surface::choice::{Asked, Choice, Document, decided};
    use xmip_core_cli::surface_open;

    let place = file::temporary::Folder::new("cli-link");
    let (bin, path) = (place.join("opt/bin"), place.join("usr/bin"));
    std::fs::create_dir_all(&bin).expect("bin");
    std::fs::create_dir_all(&path).expect("path");
    std::fs::write(bin.join("xmip"), "").expect("the program");
    std::os::unix::fs::symlink(bin.join("xmip"), path.join("xmip")).expect("the link");
    let endpoint = runtime::registration::local_endpoint(&test_cluster().node(0).name, None);
    let written = table::local_document(&endpoint).expect("written");
    std::fs::write(bin.join(COMMAND_LINE), written).expect("the document");

    let folder = surface_open::folder(&path.join("xmip"));
    let document = Document::read(&folder.join(COMMAND_LINE));

    assert_eq!(folder, std::fs::canonicalize(&bin).expect("canonical"));
    assert_eq!(
        decided(&Asked::Stated(SurfaceLine::default()), &document, false),
        Ok(Choice::Local(Some(endpoint)))
    );
}
