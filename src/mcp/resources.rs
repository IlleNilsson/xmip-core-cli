//! The scope tree as MCP resources: every scope an Xmip URI (ADR-0027
//! clause 3). `resources/list` gives every scope the surface publishes, the
//! templates are the tree, and `resources/read` drills as an operator does:
//! the scope's row as `show` gives it, then the rows beneath it as `list`
//! does. A URI with a wildcard is not a resource; the tools take wildcards.

use serde_json::Value;

use net::rpc::code;
use operate::surface::scope_selection::has_wildcard;

use crate::json::Json;
use crate::mcp::server::McpServer;
use crate::mcp::{RESOURCE_NOT_FOUND, error, result};

/// What every resource is.
const JSON: &str = "application/json";

/// `resources/list`: every scope the session's surface publishes.
#[must_use]
pub fn list(server: &McpServer<'_>, id: &Value) -> String {
    let surface = match server.surface() {
        Ok(surface) => surface,
        Err(reason) => return error(Some(id), code::INTERNAL_ERROR, &reason),
    };
    let scopes = surface.index().matching("*");

    result(id, |json| {
        json.array("resources", |json| {
            for scope in &scopes {
                json.object_value(|json| {
                    json.string("uri", scope);
                    json.string("name", scope);
                    json.string("mimeType", JSON);
                });
            }
        });
    })
}

/// `resources/templates/list`'s result: the tree, a cluster, a node and the
/// path beneath it.
pub fn write_templates(json: &mut Json) {
    json.array("resourceTemplates", |json| {
        template(
            json,
            ("xmip:///{cluster}", "cluster"),
            "A cluster: every node beneath it.",
        );
        template(
            json,
            ("xmip:///{cluster}/node/{node}", "node"),
            "A node of a cluster: everything it runs.",
        );
        template(
            json,
            ("xmip:///{cluster}/node/{node}/{+path}", "beneath a node"),
            &format!(
                "What a node runs, as its path beneath the node: a stage ({}) and what runs \
                 there, and the rest the node publishes; list a node to see it.",
                node::Stage::WORDS.join(", ")
            ),
        );
    });
}

/// `resources/read`: the scope's row, then the rows beneath it — a leaf's
/// row alone — or why there is none.
#[must_use]
pub fn read(server: &McpServer<'_>, id: &Value, parameters: Option<&Value>) -> String {
    let Some(uri) = parameters
        .and_then(|given| given.get("uri"))
        .and_then(Value::as_str)
        .filter(|uri| uri.starts_with("xmip:"))
    else {
        return error(
            Some(id),
            code::INVALID_PARAMS,
            "resources/read needs an Xmip URI, xmip:///…",
        );
    };

    if has_wildcard(uri) {
        return error(
            Some(id),
            code::INVALID_PARAMS,
            &format!("{uri} is a wildcard, and a resource is one scope; the tools take wildcards."),
        );
    }

    let (exit, row, why) = server.answered(&server.line(&["show", uri]));
    if exit != 0 {
        return error(Some(id), RESOURCE_NOT_FOUND, why.trim());
    }

    // A leaf has nothing beneath it, which list says as a failure; the read
    // is the row alone then.
    let (listed, beneath, _) = server.answered(&server.line(&["list", uri]));

    result(id, |json| {
        json.array("contents", |json| {
            content(json, uri, &row);
            if listed == 0 {
                content(json, uri, &beneath);
            }
        });
    })
}

fn content(json: &mut Json, uri: &str, document: &str) {
    json.object_value(|json| {
        json.string("uri", uri);
        json.string("mimeType", JSON);
        json.string("text", document.trim_end());
    });
}

fn template(json: &mut Json, (template, name): (&str, &str), description: &str) {
    json.object_value(|json| {
        json.string("uriTemplate", template);
        json.string("name", name);
        json.string("description", description);
        json.string("mimeType", JSON);
    });
}
