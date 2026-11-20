//! From and to on every observation the command line says (the owner,
//! 2026-11-10; ADR-0052, amendment 2026-11-11): `xmip health` and `xmip
//! list` say where the work at each scope came from and went, in the
//! runtime's words, in text and in a document; `xmip subscriptions` the
//! Send Ports each Subscription reaches.

#[path = "support/fake.rs"]
mod fake;

use std::path::PathBuf;
use std::sync::Arc;

use configure::fixture::test_cluster;
use file::temporary::Folder;
use operate::surface::snapshot::SnapshotSurface;
use serde_json::Value;
use xmip_core_cli::command::{health, scope};

/// A snapshot a test wrote, in a folder of its own removed when it is
/// dropped.
struct Written {
    path: PathBuf,
    processor: String,
    node: String,
    _folder: Folder,
}

impl Written {
    fn new(name: &str) -> Self {
        let cluster = test_cluster();
        let processor = cluster.node(0).name.clone();
        let node = format!("{}/node/{processor}", cluster.scope());
        let folder = Folder::new(&format!("cli-from-to-{name}"));
        let path = folder.join("snapshot.xmip");
        let text = format!(
            "node = \"{scope}\"\n\
             [[records]]\nscope = \"{node}/receive/In\"\nstate = \"fine\"\n\
             [[records]]\nscope = \"{node}/receive/In/delivery\"\nstate = \"stressed\"\n\
             severity = 60\nevidence = \"a document no Subscription takes was dropped\"\n\
             [[subscriptions]]\nnode = \"{node}\"\nname = \"onward\"\nsend_ports = [\"Out\"]\n\
             [topology]\nobserved_unix_nanos = 5\n\
             [[topology.nodes]]\nid = \"node/{processor}/receive/In\"\nkind = \"endpoint\"\n\
             scope = \"{node}/receive/In\"\n\
             [[topology.links]]\nid = \"in\"\nfrom = \"node/{processor}/receive/In\"\n\
             to = \"node/{processor}/process/onward\"\n",
            scope = cluster.scope(),
        );
        std::fs::write(&path, text).expect("the snapshot is written");

        Self {
            path,
            processor,
            node,
            _folder: folder,
        }
    }

    fn surface(&self) -> SnapshotSurface {
        SnapshotSurface::new(&self.path, Arc::new(xcore::SystemClock))
    }

    fn said(&self) -> String {
        format!("from In [{}] → to Out", self.processor)
    }
}

fn text(output: Vec<u8>) -> String {
    String::from_utf8(output).expect("UTF-8")
}

#[test]
fn health_says_from_and_to_on_every_record_as_text_and_as_a_document() {
    let written = Written::new("health");
    let surface = written.surface();
    let at = format!("{}/receive", written.node);
    let (mut said, mut json) = (Vec::new(), Vec::new());

    health::run(&surface, &at, false, &mut said, &mut Vec::new());
    health::run(&surface, &at, true, &mut json, &mut Vec::new());

    let said = text(said);
    assert!(said.contains(&written.said()), "{said}");
    let document: Value = serde_json::from_str(&text(json)).expect("JSON");
    let record = &document["records"][0];
    assert_eq!(
        record["from"],
        format!("In [{}]", written.processor).as_str()
    );
    assert_eq!(record["to"], "Out");
}

#[test]
fn a_listed_row_says_from_and_to() {
    let written = Written::new("list");
    let surface = written.surface();
    let mut said = Vec::new();

    scope::list_scope(
        &surface,
        &format!("{}/receive", written.node),
        false,
        &mut said,
        &mut Vec::new(),
    );

    let said = text(said);
    assert!(said.contains(&written.said()), "{said}");
}

#[test]
fn a_subscription_says_the_send_ports_it_reaches() {
    let written = Written::new("subscriptions");
    let path = written.path.clone();

    let answered = fake::answered(&["subscriptions", "--json"], || {
        Box::new(SnapshotSurface::new(&path, Arc::new(xcore::SystemClock)))
    });

    let document: Value = serde_json::from_str(&answered.output).expect("JSON");
    assert_eq!(
        document["subscriptions"][0]["send_ports"],
        serde_json::json!(["Out"])
    );
}
