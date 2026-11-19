//! `xmip mcp`: a Model Context Protocol server over standard input and
//! output (ADR-0071) — JSON-RPC 2.0, one message a line (`net::rpc`), the
//! fifth way into the operator boundary. Its tools are the command line's
//! read commands ([`tools`]) and its resources the scope tree, each scope
//! an Xmip URI ([`resources`]); both answer through `run`, as the command
//! typed would, and each answer is audited as that command
//! (`command_audit`). Read only: no act is reachable, so the caller acts as
//! no more than an Observer (ADR-0009, amendment 2026-09-06), whatever the
//! role it holds.

pub mod resources;
pub mod server;
pub mod tools;

use serde_json::Value;

use crate::json::{self, Escaping, Json};

/// MCP's code for a resource that is not there; JSON-RPC's own are
/// `net::rpc::code`'s.
pub const RESOURCE_NOT_FOUND: i64 = -32002;

/// A result, as one line: `jsonrpc`, the id as the request gave it, and the
/// result `body` writes.
#[must_use]
pub fn result(id: &Value, body: impl FnOnce(&mut Json)) -> String {
    message(Some(id), |json| json.object("result", body))
}

/// An error, as one line; a null id where the request's could not be read.
#[must_use]
pub fn error(id: Option<&Value>, code: i64, said: &str) -> String {
    message(id, |json| {
        json.object("error", |json| {
            json.number("code", code);
            json.string("message", said);
        });
    })
}

// One JSON-RPC message: jsonrpc, the id as the request gave it, then what
// the body writes; escaped as JSON requires and no more, so a document
// inside a text is read as written.
fn message(id: Option<&Value>, body: impl FnOnce(&mut Json)) -> String {
    Json::with(Escaping::Relaxed, |json| {
        json.string("jsonrpc", "2.0");
        match id {
            Some(id) => json::value(json, "id", id),
            None => json.null("id"),
        }
        body(json);
    })
}
