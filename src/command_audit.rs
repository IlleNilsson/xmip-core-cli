//! Every invocation audited as program `xmip` through the audit capability
//! (ADR-0062): the command as it begins — action its command's name, phase
//! Begin, the arguments and options as properties — its end, a non-zero
//! exit as a Failure with what it said on stderr, a line that could not be
//! obeyed as `refused`, and anything unhandled before the process ends as
//! it would have. Records go to `AuditDirectory` in `cli.toml`, else where
//! the capability decides: `XMIP_AUDIT_DIRECTORY`, else the operating
//! system's log, which also takes a record the directory cannot.

use std::collections::BTreeMap;

use audit_file::FileSink;
use xaudit::program_audit::ProgramAudit;
use xcore::{ExecutionPhase, Severity};

use crate::command::Command;
use crate::invocation::Invocation;
use crate::invocation::audit_arguments::AuditAsk;
use crate::surface_open;
use crate::system::user;

/// The program every record names.
pub const PROGRAM: &str = "xmip";

/// The action a line that could not be obeyed is recorded under.
pub const REFUSAL: &str = "refused";

/// The audit of this executable, its records where its document says, the
/// operating system's log handed to the capability for when they cannot
/// persist.
#[must_use]
pub fn open() -> ProgramAudit {
    #[cfg(windows)]
    xaudit::operating_system_log::hand(audit_windows_event_log::WindowsEventLog);
    #[cfg(unix)]
    xaudit::operating_system_log::hand(audit_syslog::Syslog);

    let directory = surface_open::audit_directory(&surface_open::document());
    ProgramAudit::new(PROGRAM, FileSink::stated(directory.as_deref()))
}

/// The action a command is recorded under: its name, lower case, as the
/// command line always recorded it.
#[must_use]
pub fn action(command: Command) -> &'static str {
    match command {
        Command::EventSubscriptions => "eventsubscriptions",
        Command::DeadMessages => "deadmessages",
        other => other.word(),
    }
}

/// What a line said, as a record's properties: the user, the argument, each
/// option stated, and what `audit` asked in its query's words.
#[must_use]
pub fn properties(invocation: &Invocation) -> BTreeMap<String, String> {
    let mut said = BTreeMap::from([("user".to_string(), user::name())]);
    let mut stated = |key: &str, value: Option<&str>| {
        if let Some(value) = value {
            said.insert(key.to_string(), value.to_string());
        }
    };

    stated(
        "argument",
        Some(invocation.argument.as_str()).filter(|it| !it.is_empty()),
    );
    stated("json", invocation.json.then_some("yes"));
    stated("follow", invocation.follow.then_some("yes"));
    stated("remote", invocation.remote.as_deref());
    stated("snapshot", invocation.snapshot.as_deref());
    stated("who", invocation.who.as_deref());

    for (key, value) in invocation.audit.iter().flat_map(AuditAsk::pairs) {
        if key != "pattern" {
            said.insert(key, value);
        }
    }

    said
}

/// The command as it begins.
pub fn begun(audit: &ProgramAudit, invocation: &Invocation) {
    kept(audit.record(
        action(invocation.command),
        ExecutionPhase::Begin,
        Severity::Information,
        None,
        properties(invocation),
    ));
}

/// Its end: Finished at 0, else a Failure saying what it said on stderr or,
/// where it said nothing, its exit.
pub fn ended(audit: &ProgramAudit, invocation: &Invocation, exit: i32, complaint: &str) {
    let mut said = properties(invocation);
    said.insert("exit".to_string(), exit.to_string());
    let words = complaint.trim();
    let failure = if words.is_empty() {
        format!("exit {exit}")
    } else {
        words.to_string()
    };

    kept(if exit == 0 {
        audit.record(
            action(invocation.command),
            ExecutionPhase::Finished,
            Severity::Information,
            None,
            said,
        )
    } else {
        audit.record(
            action(invocation.command),
            ExecutionPhase::Failure,
            Severity::Error,
            Some(&failure),
            said,
        )
    });
}

/// A line that could not be obeyed, with why.
pub fn refused(audit: &ProgramAudit, problem: &str) {
    let said = BTreeMap::from([
        ("user".to_string(), user::name()),
        ("exit".to_string(), "2".to_string()),
    ]);

    kept(audit.record(
        REFUSAL,
        ExecutionPhase::Failure,
        Severity::Error,
        Some(problem),
        said,
    ));
}

// What became of a record is the capability's to say: a record the
// operating system's log took instead is said there, and none is lost here.
fn kept<T, E>(_outcome: Result<T, E>) {}
