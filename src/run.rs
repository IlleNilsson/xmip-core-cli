//! Every command, answered once: the surface the line and the document
//! choose opened, the answer written to the output and error writers the
//! caller hands in, and the exit returned. The executable runs one per
//! process over the console; `mcp` runs one per tool call over writers of
//! its own (ADR-0071), so a tool and its command cannot answer differently.

use std::io::{Read, Write};
use std::sync::atomic::AtomicBool;

use operate::surface::OperatorSurface;
use operate::surface::acted::{self, ScopeAction};
use operate::surface::scope_selection::ScopeSelection;
use xaudit::program_audit::ProgramAudit;

use crate::command::{self, Command};
use crate::invocation::Invocation;
use crate::surface_open::Opener;
use crate::system::user;
use crate::usage;

/// What a command answers with: where it writes, what it reads a secret's
/// value from, and the flag that ends a follow.
pub struct Writers<'a> {
    pub output: &'a mut dyn Write,
    pub error: &'a mut dyn Write,
    pub input: &'a mut dyn Read,
    pub stop: &'a AtomicBool,
}

/// Answer `invocation`. `help`, and `mcp` — the MCP server's, never
/// answered here — print the usage.
pub fn answer(
    invocation: &Invocation,
    audit: &ProgramAudit,
    open: Opener<'_>,
    to: &mut Writers<'_>,
) -> i32 {
    let json = invocation.json;
    let who = acted::who(invocation.who.as_deref(), &user::name());

    match invocation.command {
        Command::Abi => command::abi::run(json, to.output),
        Command::Status => command::status::run(&invocation.argument, json, to.output, to.error),
        Command::Probe => command::probe::run(&invocation.argument, json, to.output, to.error),
        Command::Health | Command::Measure | Command::List | Command::Show => over(
            invocation,
            open,
            to,
            |surface, chosen, to| match invocation.command {
                Command::Health => command::health::over(surface, chosen, invocation, to),
                Command::Measure => command::measure::over(surface, chosen, invocation, to),
                Command::List => command::scope::list(surface, chosen, json, to.output, to.error),
                _ => command::scope::show(surface, chosen, json, to.output, to.error),
            },
        ),
        Command::Pause | Command::Resume => over(invocation, open, to, |surface, chosen, to| {
            let (action, who) = if invocation.command == Command::Pause {
                (ScopeAction::Pause, who.clone())
            } else {
                (ScopeAction::Resume, acted::who(None, &user::name()))
            };
            command::scope::apply(surface, chosen, (action, &who), json, to.output, to.error)
        }),
        Command::Validate => {
            command::validate::run(&invocation.argument, json, to.output, to.error)
        }
        Command::Certificate => command::certificate::run(invocation, to.output, to.error),
        Command::Database => command::database::run(invocation, to.output, to.error),
        // Every Report, and one opened, each probe audited where this
        // executable's records go, and read back from there.
        Command::Report => command::report::run(invocation, audit, to.output, to.error),
        // The audit read back from where this executable's own records go.
        Command::Audit => command::audit::run(invocation, to.output, to.error),
        Command::EventSubscriptions
        | Command::Subscriptions
        | Command::DeadMessages
        | Command::Message
        | Command::Secret => on(invocation, open, to, |surface, to| {
            command::noun::answer(surface, invocation, &who, to)
        }),
        Command::Help | Command::Mcp => usage::print(to.output),
    }
}

// Every command that answers over scopes, once: open the surface, select
// what the argument names — a wildcard among the scopes that exist
// (ADR-0059 clause 7) — and refuse a pattern that names nothing here: the
// words for a person, the same refusal as a document for --json, on the
// error writer and never exit 0.
fn over(
    invocation: &Invocation,
    open: Opener<'_>,
    to: &mut Writers<'_>,
    answer: impl FnOnce(&dyn OperatorSurface, &ScopeSelection, &mut Writers<'_>) -> i32,
) -> i32 {
    let surface = match open(invocation) {
        Ok(surface) => surface,
        Err(reason) => {
            crate::say(to.error, &reason);
            return 1;
        }
    };

    match ScopeSelection::of(surface.as_ref(), &invocation.argument) {
        Ok(chosen) => answer(surface.as_ref(), &chosen, to),
        Err(unmatched) => {
            let said = if invocation.json {
                command::scope::unmatched(&invocation.argument, &unmatched)
            } else {
                unmatched
            };
            crate::say(to.error, &said);
            1
        }
    }
}

// A command that answers over the surface itself, not over scopes.
fn on(
    invocation: &Invocation,
    open: Opener<'_>,
    to: &mut Writers<'_>,
    answer: impl FnOnce(&dyn OperatorSurface, &mut Writers<'_>) -> i32,
) -> i32 {
    match open(invocation) {
        Ok(surface) => answer(surface.as_ref(), to),
        Err(reason) => {
            crate::say(to.error, &reason);
            1
        }
    }
}
