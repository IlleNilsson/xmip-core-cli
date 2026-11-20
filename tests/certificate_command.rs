//! `xmip certificate`: one act a line and its options no other command's; a
//! node issued its key, certificate and anchors from the cluster's
//! authority; a person's request made and signed by the authority a node's
//! configuration names; and a node's certificate carried across an air gap
//! — requested, signed, imported, inspected and revoked. The node and the
//! cluster are the test cluster's, never a literal (ADR-0056, amendment
//! 2026-10-03).

use std::path::Path;

use file::temporary::Folder;
use tls::certificate::Certificate;
use xmip_core_cli::command::{Command, certificate};
use xmip_core_cli::invocation::Invocation;

const PERSON: &str = "alice@example.org";

fn line(words: &[&str]) -> Vec<String> {
    words.iter().map(ToString::to_string).collect()
}

fn parse(words: &[&str]) -> Invocation {
    Invocation::parse(&line(words)).expect("a line that can be obeyed")
}

fn refused(words: &[&str]) -> String {
    Invocation::parse(&line(words)).expect_err("a line that cannot be obeyed")
}

// The line answered: its exit, what it said and what it complained of.
fn answered(words: &[&str]) -> (i32, String, String) {
    let (mut output, mut error) = (Vec::new(), Vec::new());
    let exit = certificate::run(&parse(words), &mut output, &mut error);
    (
        exit,
        String::from_utf8(output).expect("UTF-8"),
        String::from_utf8(error).expect("UTF-8"),
    )
}

// The line answered, which must exit 0: what it said.
fn answer(words: &[&str]) -> String {
    let (exit, said, error) = answered(words);
    assert_eq!(exit, 0, "{error}");
    said
}

// A folder of the test's own, gone when it is dropped, and the paths in it
// as the line writes them.
struct Place(Folder);

impl Place {
    fn new(name: &str) -> Self {
        Self(Folder::new(&format!("cli-certificate-{name}")))
    }

    fn at(&self, name: &str) -> String {
        self.0.join(name).display().to_string()
    }
}

fn subject(pem: &str) -> String {
    Certificate::first(pem)
        .expect("a certificate")
        .common_name()
        .expect("a subject")
        .unwrap_or_default()
}

#[test]
fn the_line_names_one_act_and_its_options_apply_to_no_other_command() {
    let cluster = configure::fixture::test_cluster();
    let node = cluster.node(0).name.clone();
    let names = format!("{node}.example");

    let made = parse(&["certificate", "--operator", PERSON, "--out", "phone"]);
    assert_eq!(made.command, Command::Certificate);
    let asked = made.certificate.clone().expect("asked");
    assert_eq!(
        (asked.operator.as_deref(), asked.out.as_deref()),
        (Some(PERSON), Some("phone"))
    );

    let signing = parse(&["certificate", "operator.csr", "--sign", "node.xmip"]);
    assert_eq!(signing.argument, "operator.csr");
    assert_eq!(
        signing.certificate.and_then(|ask| ask.sign).as_deref(),
        Some("node.xmip")
    );

    assert!(refused(&["certificate"]).contains("needs an act"));
    assert!(
        refused(&[
            "certificate",
            "x.csr",
            "--operator",
            PERSON,
            "--sign",
            "n.xmip"
        ])
        .contains("one act a line")
    );
    assert!(refused(&["certificate", "x.csr", "--operator", PERSON]).contains("takes no argument"));
    assert!(refused(&["certificate", "--sign", "n.xmip"]).contains("needs the request"));
    assert!(refused(&["certificate", "--out"]).contains("needs a path"));
    assert_eq!(
        refused(&["validate", "node.xmip", "--operator", PERSON]),
        "The options of 'certificate' only apply to 'certificate', --out to 'certificate' and \
         'database', and --node to 'certificate' and 'report'."
    );

    let issuing = parse(&[
        "certificate",
        "--node",
        &node,
        "--cluster",
        &cluster.name,
        "--authority",
        "ca",
        "--names",
        &names,
    ]);
    let asked = issuing.certificate.expect("asked");
    assert_eq!(
        (asked.node.as_deref(), asked.names.as_deref()),
        (Some(node.as_str()), Some(names.as_str()))
    );

    assert!(
        refused(&["certificate", "--node", &node, "--authority", "ca"]).contains("needs --cluster")
    );
    assert!(
        refused(&[
            "certificate",
            "--node",
            &node,
            "--cluster",
            &cluster.name,
            "--authority",
            "ca",
            "--operator",
            PERSON,
        ])
        .contains("one act a line")
    );
    assert!(refused(&["certificate", "--revoke", "0A"]).contains("needs --authority"));
    assert!(refused(&["certificate", "--import", "tls"]).contains("needs the chain"));
    assert!(
        refused(&[
            "certificate",
            "r.csr",
            "--authority",
            "ca",
            "--cluster",
            &cluster.name
        ])
        .contains("takes no --cluster")
    );

    let carried = parse(&["certificate", "--node", &node, "--out", "tls"]);
    assert_eq!(carried.certificate.and_then(|ask| ask.authority), None);
}

#[test]
fn a_node_is_issued_its_key_certificate_and_anchors_from_the_cluster_s_authority() {
    let cluster = configure::fixture::test_cluster();
    let node = cluster.node(0).name.clone();
    let place = Place::new("node");
    let folder = place.at("node");

    let said = answer(&[
        "certificate",
        "--node",
        &node,
        "--cluster",
        &cluster.name,
        "--authority",
        &place.at("authority"),
        "--names",
        &format!("{node}.example"),
        "--out",
        &folder,
        "--json",
    ]);
    let document: serde_json::Value = serde_json::from_str(&said).expect("one document");
    let certificate = document["certificate"].as_str().expect("a path");

    assert_eq!(Path::new(certificate), Path::new(&folder).join("node.pem"));
    assert!(Path::new(&folder).join("anchors.pem").is_file());
    assert!(Path::new(&folder).join("node.key").is_file());
    assert_eq!(
        subject(&std::fs::read_to_string(certificate).expect("read")),
        node
    );
}

#[test]
fn a_request_is_made_and_signed_by_the_authority_the_configuration_names() {
    let place = Place::new("certificate");
    let configuration = authority(&place);
    let phone = place.at("phone");

    assert!(
        answer(&["certificate", "--operator", PERSON, "--out", &phone]).contains("operator.csr")
    );

    let request = Path::new(&phone).join("operator.csr").display().to_string();
    let chain = answer(&["certificate", &request, "--sign", &configuration]);
    assert_eq!(subject(&chain), PERSON);

    let kept = Path::new(&phone).join("operator.pem").display().to_string();
    let said = answer(&[
        "certificate",
        &request,
        "--sign",
        &configuration,
        "--out",
        &kept,
        "--json",
    ]);
    let document: serde_json::Value = serde_json::from_str(&said).expect("one document");
    assert_eq!(document["written"], kept.as_str());

    let (exit, _, error) = answered(&["certificate", "--operator", PERSON, "--out", &phone]);
    assert_eq!(exit, 1);
    assert!(error.contains("nothing is overwritten"), "{error}");
}

#[test]
fn a_node_certificate_carried_across_an_air_gap_is_requested_signed_imported_and_inspected() {
    let cluster = configure::fixture::test_cluster();
    let node = cluster.node(0).name.clone();
    let place = Place::new("carried");
    let (authority, tls, chain) = (
        place.at("authority"),
        place.at("tls"),
        place.at("carried.pem"),
    );

    let said = answer(&[
        "certificate",
        "--authority",
        &authority,
        "--cluster",
        &cluster.name,
        "--publish",
        "http://ca.example:8080",
    ]);
    assert!(
        said.contains("published at http://ca.example:8080"),
        "{said}"
    );

    answer(&[
        "certificate",
        "--node",
        &node,
        "--names",
        &format!("{node}.example"),
        "--out",
        &tls,
    ]);
    let request = Path::new(&tls).join("node.csr").display().to_string();
    answer(&[
        "certificate",
        &request,
        "--authority",
        &authority,
        "--names",
        &format!("{node},{node}.example"),
        "--out",
        &chain,
    ]);

    let said = answer(&["certificate", &chain, "--import", &tls]);
    assert!(said.contains("compare it by another way"), "{said}");

    let leaf = Path::new(&tls).join("node.pem").display().to_string();
    let read: serde_json::Value =
        serde_json::from_str(&answer(&["certificate", "--inspect", &leaf, "--json"]))
            .expect("one document");
    assert_eq!(read["standing"], "current");
    let serial = read["serial"].as_str().expect("a serial").to_string();

    answer(&[
        "certificate",
        "--revoke",
        &serial,
        "--authority",
        &authority,
        "--reason",
        "keyCompromise",
    ]);
    let said = answer(&["certificate", "--inspect", &authority]);
    assert!(said.contains("1 revoked"), "{said}");
    let lines: Vec<&str> = said.split('\n').collect();
    assert!(lines[lines.len() - 2].contains("revoked"), "{said}");

    let (exit, _, error) = answered(&["certificate", &chain, "--import", &tls]);
    assert_eq!(exit, 1);
    assert!(error.contains("no request is waiting"), "{error}");
}

// An authority kept as the capability keeps one, and a node's slice
// declaring it in `[provision.operator]`, signed by it in a configuration
// folder of its own (ADR-0031, amendment 2026-11-19): the slice.
fn authority(place: &Place) -> String {
    let folder = place.0.join("config");
    std::fs::create_dir_all(&folder).expect("a configuration folder");
    let kept = folder.join("authority");
    provision::Issuer::kept_or_made(&kept, "test authority", provision::now(), 86_400)
        .expect("an authority");
    let cluster = "[service]\nname = \"n\"\n\n\
                   [nodes.d.provision.operator]\nauthority = \"authority\"\n";
    let signed = runtime::signed::sliced_signed(
        cluster,
        &folder.join("cluster.xmip"),
        "d",
        Some("c"),
        Some(&kept),
    )
    .expect("signed");
    let configuration = folder.join("node.xmip");
    std::fs::write(&configuration, signed).expect("written");
    configuration.display().to_string()
}
