//! The `xmip` command line over a running Xmip (ADR-0014; ADR-0052 clause
//! 5), in Rust and needing no .NET (the owner, 2026-10-12: *the CLI has to
//! be usable without .NET*). Text for a person on stdout, `--json` one
//! document and `--follow` JSON Lines (ADR-0014 clause 10); every complaint
//! on stderr with a non-zero exit, 2 when the line could not be obeyed and
//! 1 when the thing asked about is wrong.
//!
//! It is argument parsing and rendering over `operate::surface`, the
//! surface every operator surface reads, and the runtime's crates beneath
//! it, called Rust to Rust (ADR-0014, amendment 2026-10-12): [`invocation`]
//! parses the line, each [`command`] renders over the writers it is handed,
//! so every rendering is tested without a console, [`run`] answers a parsed
//! line — the executable's, or an MCP tool's ([`mcp`]) — and
//! [`command_audit`] records each one through the audit capability.

pub mod command;
pub mod command_audit;
pub mod invocation;
pub mod json;
pub mod mcp;
pub mod run;
pub mod surface_open;
pub mod system;
pub mod text_table;
pub mod time;
pub mod usage;

use std::io::Write;

/// One line on `writer`, ended by a line feed; a writer that fails takes
/// nothing more, as a closed pipe does.
pub fn say(writer: &mut dyn Write, text: &str) {
    let _ = writeln!(writer, "{text}");
}
