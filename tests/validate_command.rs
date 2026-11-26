//! `xmip validate <toml>`: a verdict rendered — valid on stdout, invalid on
//! stderr with its problems, the record as a document — and the exit
//! decided by the record, never by the sentence (ADR-0052 clause 4); and a
//! Location held to its technology's declaration, though the command line
//! loads no technology (ADR-0064, amendment 2026-11-26).

use abi::ffi::status;
use configure::fixture::test_cluster;
use file::temporary::Folder;
use operate::surface::configuration_verdict::ConfigurationVerdict;
use xmip_core_cli::command::validate::{run, verdict};

const TOML: &str = "node.xmip";

fn rendered(said: &ConfigurationVerdict, json: bool) -> (i32, String, String) {
    let (mut output, mut error) = (Vec::new(), Vec::new());
    let exit = verdict(said, json, &mut output, &mut error);
    (
        exit,
        String::from_utf8(output).expect("UTF-8"),
        String::from_utf8(error).expect("UTF-8"),
    )
}

#[test]
fn a_valid_document_is_said_so_on_stdout() {
    let (exit, said, _) = rendered(
        &ConfigurationVerdict::validated(TOML, status::OK, vec![]),
        false,
    );

    assert_eq!(exit, 0);
    assert_eq!(said, format!("{TOML} is valid\n"));
}

#[test]
fn an_invalid_document_is_said_so_on_stderr_with_its_problems() {
    let problems = vec!["no name".to_string(), "no receive location".to_string()];
    let (exit, _, error) = rendered(
        &ConfigurationVerdict::validated(TOML, status::INVALID, problems),
        false,
    );

    assert_eq!(exit, 1);
    assert_eq!(
        error,
        format!("{TOML} is invalid: no name; no receive location\n")
    );
}

#[test]
fn json_carries_the_record() {
    let answer = ConfigurationVerdict::validated(TOML, status::INVALID, vec!["no name".into()]);
    let (exit, said, _) = rendered(&answer, true);
    let document: serde_json::Value = serde_json::from_str(&said).expect("one document");

    assert_eq!(exit, 1);
    assert_eq!(document["valid"], false);
    assert_eq!(document["status"], "Invalid");
    assert_eq!(document["problems"][0], "no name");
}

#[test]
fn the_exit_is_the_record_s_not_the_sentence_s() {
    let misleading = ConfigurationVerdict {
        path: TOML.to_string(),
        status: status::INVALID,
        problems: Vec::new(),
        said: format!("{TOML} is valid, said nobody"),
    };

    assert_eq!(rendered(&misleading, false).0, 1);
}

#[test]
fn a_locations_setting_its_technology_does_not_declare_is_refused() {
    let cluster = test_cluster();
    let node = &cluster.node(0).name;
    let folder = Folder::new("cli-validate-declared");
    let path = folder.join(TOML);
    std::fs::write(
        &path,
        format!(
            "[service]\nname = \"xmip-{node}\"\ncluster_name = \"{}\"\nnode_name = \"{node}\"\n\n\
             [[send_locations]]\nname = \"billing-out\"\naddress = \"C:/out\"\n\
             [send_locations.settings]\nnamed = \"{{message}}.edi\"\n",
            cluster.name
        ),
    )
    .expect("written");
    let (mut output, mut error) = (Vec::new(), Vec::new());

    let exit = run(
        path.to_str().expect("UTF-8"),
        false,
        &mut output,
        &mut error,
    );

    let said = String::from_utf8(error).expect("UTF-8");
    assert_eq!(exit, 1, "{said}");
    assert!(
        said.contains("xmip-core-transport-file has no setting \"named\""),
        "{said}"
    );
}
