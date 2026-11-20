//! Every invocation audited as program `xmip` (ADR-0062): a command's end
//! as Finished or as a Failure with its exit, a line that could not be
//! obeyed as refused — and the executable itself recording a line it
//! refuses.

use std::path::Path;

use audit_file::FileSink;
use file::temporary::Folder;
use xaudit::program_audit::ProgramAudit;
use xmip_core_cli::command_audit::{self, PROGRAM};
use xmip_core_cli::invocation::Invocation;

fn scratch() -> Folder {
    Folder::new("cli-command-audit")
}

fn audit(directory: &Path) -> ProgramAudit {
    ProgramAudit::new(PROGRAM, FileSink::stated(Some(directory)))
}

fn records(directory: &Path) -> String {
    std::fs::read_to_string(directory.join("audit.toml")).expect("records")
}

fn line(words: &[&str]) -> Invocation {
    Invocation::parse(&words.iter().map(ToString::to_string).collect::<Vec<_>>()).expect("parsed")
}

#[test]
fn a_failed_command_lands_as_a_failure_record_with_its_exit_code() {
    let directory = scratch();
    let audit = audit(&directory);
    let invocation = line(&["validate", "missing.xmip"]);

    command_audit::begun(&audit, &invocation);
    command_audit::ended(&audit, &invocation, 2, "No file at missing.xmip.\n");
    let text = records(&directory);

    assert!(text.contains("[[record]]"), "{text}");
    assert!(text.contains("program = \"xmip\""), "{text}");
    assert!(text.contains("action = \"validate\""), "{text}");
    assert!(text.contains("phase = \"Begin\""), "{text}");
    assert!(text.contains("phase = \"Failure\""), "{text}");
    assert!(
        text.contains("message = \"No file at missing.xmip.\""),
        "{text}"
    );
    assert!(text.contains("\"exit\" = \"2\""), "{text}");
}

#[test]
fn a_command_that_exits_zero_is_finished() {
    let directory = scratch();

    command_audit::ended(&audit(&directory), &line(&["abi"]), 0, "");

    assert!(records(&directory).contains("phase = \"Finished\""));
}

#[test]
fn a_line_that_cannot_be_obeyed_is_recorded_as_refused() {
    let directory = scratch();

    command_audit::refused(&audit(&directory), "no such word");
    let text = records(&directory);

    assert!(text.contains("action = \"refused\""), "{text}");
    assert!(text.contains("phase = \"Failure\""), "{text}");
}

#[test]
fn the_executable_records_a_line_it_refuses() {
    let directory = scratch();
    // Both streams read together, to the end: reading one while the other
    // fills its pipe is how a child blocks forever.
    let ran = std::process::Command::new(env!("CARGO_BIN_EXE_xmip"))
        .arg("nonsense")
        .env("XMIP_AUDIT_DIRECTORY", &*directory)
        .output()
        .expect("ran");
    let error = String::from_utf8_lossy(&ran.stderr);

    assert_eq!(ran.status.code(), Some(2));
    assert!(
        error.contains("'nonsense' is not an xmip command"),
        "{error}"
    );
    let text = records(&directory);
    assert!(text.contains("program = \"xmip\""), "{text}");
    assert!(text.contains("action = \"refused\""), "{text}");
    assert!(text.contains("phase = \"Failure\""), "{text}");
}
