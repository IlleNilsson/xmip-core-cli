//! `--local` at a node that gathers its cluster's view — a node declaring the
//! monitoring role — answers for the whole cluster, and `health` says so
//! after where the node is reached, in the node's own words; at a node that
//! gathers none it answers for that node (the design approved 2026-11-12,
//! section 3.5). Which of the two is the node's answer, never its name: both
//! are served under names of their own.

use std::sync::Arc;
use std::sync::atomic::{AtomicU32, Ordering};

use audit_file::FileSink;
use configure::fixture::test_cluster;
use runtime::plane::local::{Admitting, running_as};
use runtime::plane::{Board, Grants, Listener, Published};
use serde_json::Value;
use xaudit::program_audit::ProgramAudit;
use xmip_core_cli::command::health;
use xmip_core_cli::invocation::Invocation;
use xmip_core_cli::surface_open::{self, Surface};

fn words() -> String {
    format!(
        "{}, gathered by a monitoring node: 1 of 2 nodes reached",
        test_cluster().name
    )
}

/// A local listener this account observes, posting one node's own
/// publication and, where it gathers, the cluster's view: one member
/// reached, the other not.
struct Served {
    listener: Listener,
    _own: Arc<Board>,
    _cluster: Option<Arc<Board>>,
}

fn serve(gathers: bool) -> Served {
    static NEXT: AtomicU32 = AtomicU32::new(0);
    let endpoint = format!(
        "xmip-cli-gathered-{}-{}",
        std::process::id(),
        NEXT.fetch_add(1, Ordering::SeqCst)
    );
    let directory = std::env::temp_dir().join(&endpoint);
    std::fs::create_dir_all(&directory).expect("an audit directory");
    let audit = ProgramAudit::new("gathered-cli-test", Some(FileSink::new(&directory)));
    let me = running_as().expect("this process's account");
    let none: &[String] = &[];
    let grants = Grants::listed([std::slice::from_ref(&me), none, none]);
    let own = Arc::new(Board::default());
    let listener = Listener::local(
        &endpoint,
        Admitting::default(),
        Arc::clone(&own) as Arc<dyn Published>,
        grants,
        &audit,
        Arc::new(xcore::SystemClock),
    )
    .expect("it serves");
    let cluster = test_cluster();

    own.post(publication(
        "own",
        &[record(
            &format!("{}/receive/orders", cluster.node_scope(0)),
            "fine",
        )],
    ));

    let gathered = gathers.then(|| {
        let board = Arc::new(Board::default());
        listener.gathers(Arc::clone(&board) as Arc<dyn Published>);
        board.post(publication(
            &words(),
            &[
                record(&format!("{}/reached", cluster.node_scope(0)), "fine"),
                record(&format!("{}/reached", cluster.node_scope(1)), "exhausted"),
            ],
        ));
        board
    });

    Served {
        listener,
        _own: own,
        _cluster: gathered,
    }
}

fn publication(source: &str, records: &[String]) -> String {
    format!(
        "source = \"{source}\"\nnode = \"{}\"\n\n{}",
        test_cluster().scope(),
        records.join("\n")
    )
}

fn record(scope: &str, state: &str) -> String {
    format!(
        "[[records]]\nscope = \"{scope}\"\nstate = \"{state}\"\nseverity = 0\nevidence = \"\"\n\
         observed_unix_nanos = 1789111688000000000\n"
    )
}

// The surface the executable opens for a health line stating these.
fn opened(stated: &[&str]) -> Surface {
    let mut line = vec!["health".to_string(), test_cluster().scope()];
    line.extend(stated.iter().map(ToString::to_string));
    let invocation = Invocation::parse(&line).expect("the line parses");
    surface_open::open(&invocation).expect("the surface answers")
}

fn run(surface: &Surface, scope: &str, json: bool) -> (i32, String) {
    let mut output = Vec::new();
    let exit = health::run(surface.as_ref(), scope, json, &mut output, &mut Vec::new());
    (exit, String::from_utf8(output).expect("UTF-8"))
}

#[test]
fn health_at_a_gathering_node_answers_for_the_cluster_and_says_who_gathered() {
    let served = serve(true);
    let address = served.listener.address();
    let surface = opened(&["--local", &address]);
    let cluster = test_cluster();

    let (exit, said) = run(&surface, &cluster.scope(), false);

    assert_eq!(exit, 0);
    assert!(
        said.contains(&format!("source LOCAL — {address} — {}", words())),
        "{said}"
    );
    assert!(
        said.contains(&format!("{}/reached", cluster.node_scope(0))),
        "{said}"
    );
    assert!(
        said.contains(&format!("{}/reached", cluster.node_scope(1))),
        "{said}"
    );

    let (exit, json) = run(&surface, &cluster.scope(), true);
    assert_eq!(exit, 0);
    let document: Value = serde_json::from_str(&json).expect("JSON");
    assert_eq!(
        document["source"],
        format!("LOCAL — {address} — {}", words()).as_str()
    );
}

#[test]
fn health_at_a_node_gathering_none_answers_for_that_node() {
    let served = serve(false);
    let address = served.listener.address();
    let surface = opened(&["--local", &address]);
    let cluster = test_cluster();

    let (exit, said) = run(&surface, &cluster.node_scope(0), false);

    assert_eq!(exit, 0);
    assert!(
        said.contains(&format!("source LOCAL — {address}\n")),
        "{said}"
    );
    assert!(!said.contains(&cluster.node_scope(1)), "{said}");
}
