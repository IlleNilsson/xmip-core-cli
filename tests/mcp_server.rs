//! One MCP session scripted over in-memory streams against the test
//! cluster's names: the scope tree read, the commands answering the tools,
//! every answer audited as its command, and no act reachable.

#[path = "support/fake.rs"]
mod fake;

use std::path::PathBuf;

use audit_file::FileSink;
use configure::fixture::test_cluster;
use net::rpc::code;
use observe::Health;
use serde_json::Value;
use xaudit::program_audit::ProgramAudit;
use xmip_core_cli::command_audit::PROGRAM;
use xmip_core_cli::invocation::Invocation;
use xmip_core_cli::mcp::server::{McpServer, PROTOCOL_VERSION};
use xmip_core_cli::surface_open::Surface;

use fake::{FakeSurface, leaf};

fn request(id: u32, method: &str, parameters: &str) -> String {
    format!(r#"{{"jsonrpc":"2.0","id":{id},"method":"{method}","params":{parameters}}}"#)
}

fn call(tool: &str, arguments: &str) -> String {
    format!(r#"{{"name":"{tool}","arguments":{{{arguments}}}}}"#)
}

fn scratch(name: &str) -> PathBuf {
    let directory = std::env::temp_dir().join(format!("xmip-mcp-{name}-{}", std::process::id()));
    let _ = std::fs::remove_dir_all(&directory);
    std::fs::create_dir_all(&directory).expect("made");
    directory
}

fn error_code(answer: &Value) -> i64 {
    answer["error"]["code"].as_i64().expect("an error")
}

// The session a client holds: every method once, a notification, an act
// asked of a tool, a method this server does not answer and a line that is
// not JSON.
fn session_script(node: &str, orders: &str) -> String {
    let script = [
        request(
            1,
            "initialize",
            concat!(
                r#"{"protocolVersion":"2025-11-25","capabilities":{},"#,
                r#""clientInfo":{"name":"test","version":"1"}}"#
            ),
        ),
        r#"{"jsonrpc":"2.0","method":"notifications/initialized"}"#.to_string(),
        request(2, "tools/list", "{}"),
        request(3, "resources/templates/list", "{}"),
        request(4, "resources/list", "{}"),
        request(5, "resources/read", &format!(r#"{{"uri":"{node}"}}"#)),
        request(
            6,
            "tools/call",
            &call("health", &format!(r#""scope":"{orders}""#)),
        ),
        request(
            7,
            "tools/call",
            &call("show", &format!(r#""scope":"{orders}","who":"someone""#)),
        ),
        request(8, "resources/subscribe", "{}"),
        "not json".to_string(),
    ];
    format!("{}\n", script.join("\n"))
}

#[test]
fn a_session_reads_the_scope_tree_and_answers_by_the_commands() {
    let cluster = test_cluster();
    let orders = format!("{}/receive/orders", cluster.node_scope(0));
    let billing = format!("{}/send/billing", cluster.node_scope(1));
    let records = vec![
        leaf(&orders, Health::Holding, 70, "disk full"),
        leaf(&billing, Health::Fine, 0, ""),
    ];
    let directory = scratch("session");
    let audit = ProgramAudit::new(PROGRAM, FileSink::stated(Some(&directory)));
    let session = Invocation::parse(&["mcp".to_string()]).expect("mcp");
    let open = |_: &Invocation| -> Result<Surface, String> {
        Ok(Box::new(FakeSurface::new(records.clone())))
    };
    let input = session_script(&cluster.node_scope(0), &orders);
    let (mut output, mut diagnostics) = (Vec::new(), Vec::new());

    let exit = McpServer::new(&session, &audit, &open).run(
        &mut input.as_bytes(),
        &mut output,
        &mut diagnostics,
    );

    // One answer per request, the notification unanswered, and every line a
    // JSON-RPC 2.0 message.
    let text = String::from_utf8(output).expect("UTF-8");
    let answers: Vec<Value> = text
        .split('\n')
        .filter(|line| !line.is_empty())
        .map(|line| serde_json::from_str(line).expect("JSON"))
        .collect();
    assert_eq!(exit, 0);
    assert_eq!(answers.len(), 9, "{text}");
    assert!(answers.iter().all(|answer| answer["jsonrpc"] == "2.0"));

    let initialized = &answers[0]["result"];
    assert_eq!(initialized["protocolVersion"], PROTOCOL_VERSION);
    assert_eq!(initialized["serverInfo"]["name"], "xmip");
    assert!(initialized["capabilities"].get("resources").is_some());

    // The tools are the read commands, and no act is one.
    let tools: Vec<&str> = answers[1]["result"]["tools"]
        .as_array()
        .expect("tools")
        .iter()
        .map(|tool| tool["name"].as_str().expect("a name"))
        .collect();
    assert!(tools.contains(&"health") && tools.contains(&"dead-messages"));
    // A Quest reads what it probes and changes nothing (ADR-0072, amendment
    // 2026-11-10): every Report is a read.
    assert!(tools.contains(&"report"));
    assert!(!tools.contains(&"pause") && !tools.contains(&"resume") && !tools.contains(&"probe"));

    let templates = answers[2]["result"]["resourceTemplates"]
        .as_array()
        .expect("templates");
    assert!(
        templates
            .iter()
            .any(|one| one["uriTemplate"] == "xmip:///{cluster}/node/{node}")
    );
    let resources = answers[3]["result"]["resources"]
        .as_array()
        .expect("resources");
    assert!(resources.iter().any(|one| one["uri"] == orders.as_str()));

    // A read is the drill: the node's row, then the rows beneath it.
    let contents = answers[4]["result"]["contents"]
        .as_array()
        .expect("contents");
    assert_eq!(contents.len(), 2, "{text}");
    let row: Value =
        serde_json::from_str(contents[0]["text"].as_str().expect("text")).expect("row");
    assert_eq!(row["scope"], cluster.node_scope(0).as_str());
    assert_eq!(row["health"], "holding");
    assert_eq!(row["worst"], orders.as_str());
    assert!(
        contents[1]["text"]
            .as_str()
            .expect("text")
            .contains(&orders)
    );

    // A tool answers the command's document, with its scope.
    let called = &answers[5]["result"];
    assert_eq!(called["isError"], false);
    assert_eq!(called["_meta"]["xmip/scope"], orders.as_str());
    let health: Value =
        serde_json::from_str(called["content"][0]["text"].as_str().expect("text")).expect("doc");
    assert_eq!(health["scope"], orders.as_str());
    assert_eq!(health["worst"]["evidence"], "disk full");

    // An act is not an argument of any tool; nor is a method MCP has and this
    // server does not answer; nor is a line that is not JSON.
    assert_eq!(error_code(&answers[6]), code::INVALID_PARAMS);
    assert_eq!(error_code(&answers[7]), code::METHOD_NOT_FOUND);
    assert_eq!(error_code(&answers[8]), code::PARSE_ERROR);
    assert_eq!(answers[8]["id"], Value::Null);

    // Each answer is audited as the command it is.
    let audited = std::fs::read_to_string(directory.join("audit.toml")).expect("records");
    assert!(audited.contains("action = \"health\""), "{audited}");
    assert!(audited.contains("action = \"show\""), "{audited}");
    let _ = std::fs::remove_dir_all(&directory);
}

#[test]
fn a_line_that_acts_is_refused_whatever_reaches_it() {
    let directory = scratch("acts");
    let audit = ProgramAudit::new(PROGRAM, FileSink::stated(Some(&directory)));
    let session = Invocation::parse(&["mcp".to_string()]).expect("mcp");
    let open =
        |_: &Invocation| -> Result<Surface, String> { Err("no surface in this test".into()) };
    let server = McpServer::new(&session, &audit, &open);
    let node = test_cluster().node_scope(0);
    let line: Vec<String> = [
        "dead-messages",
        "--location",
        &node,
        "--message",
        "m",
        "--replay",
    ]
    .iter()
    .map(ToString::to_string)
    .collect();

    let (exit, output, error) = server.answered(&line);

    assert_eq!(exit, 2);
    assert_eq!(output, "");
    assert!(error.contains("mcp reads only"), "{error}");
    let _ = std::fs::remove_dir_all(&directory);
}
