//! `xmip audit`: the audit read back through the capability's one reader
//! from a directory of the test's own, as text, as a document, one record
//! whole and each writer's chain. How the line reads as the query's words
//! is `invocation.rs`'s.

use std::collections::BTreeMap;
use std::path::Path;

use audit_file::FileSink;
use configure::fixture::test_cluster;
use file::temporary::Folder;
use operate::surface::english;
use xaudit::emit::AuditOutcome;
use xaudit::program_audit::ProgramAudit;
use xcore::{ExecutionPhase, Severity};
use xmip_core_cli::command::audit::read_back;
use xmip_core_cli::command_audit::{self, PROGRAM};
use xmip_core_cli::invocation::Invocation;
use xmip_core_cli::invocation::audit_arguments::AuditAsk;

fn parsed(words: &[&str]) -> Result<Invocation, String> {
    Invocation::parse(&words.iter().map(ToString::to_string).collect::<Vec<_>>())
}

// Two records, written as any program writes them: a start, and a stop
// that warns.
fn written() -> Folder {
    let directory = Folder::new("cli-audit-read");
    let audit = audit(&directory);

    assert!(matches!(
        audit.record(
            "start",
            ExecutionPhase::Begin,
            Severity::Information,
            None,
            BTreeMap::new()
        ),
        Ok(AuditOutcome::Persisted)
    ));
    audit
        .record(
            "stop",
            ExecutionPhase::Finished,
            Severity::Warning,
            Some("stopped"),
            BTreeMap::from([("tested".to_string(), "yes".to_string())]),
        )
        .expect("kept");

    directory
}

fn audit(directory: &Path) -> ProgramAudit {
    ProgramAudit::new(PROGRAM, FileSink::stated(Some(directory)))
}

/// What a read back said: its exit, its output and its error.
fn read(directory: &Path, ask: &AuditAsk, json: bool) -> (i32, String, String) {
    let (mut output, mut error) = (Vec::new(), Vec::new());
    let exit = read_back(Some(directory), ask, json, &mut output, &mut error);

    (
        exit,
        String::from_utf8(output).expect("UTF-8"),
        String::from_utf8(error).expect("UTF-8"),
    )
}

#[test]
fn what_the_operator_asked_is_recorded_in_the_words_of_the_query() {
    let pattern = format!("{}/*", test_cluster().scope());
    let parsed =
        parsed(&["audit", &pattern, "--severity", "Error", "--limit", "5"]).expect("parsed");
    let said = command_audit::properties(&parsed);

    assert_eq!(said["argument"], pattern);
    assert_eq!(said["severity"], "Error");
    assert_eq!(said["limit"], "5");
    assert!(!said.contains_key("pattern"));
}

#[test]
fn a_query_the_capability_does_not_take_is_its_refusal_and_exit_two() {
    let directory = written();
    let asked = AuditAsk {
        severity: Some("loud".into()),
        ..AuditAsk::default()
    };
    let (exit, output, error) = read(&directory, &asked, false);

    assert_eq!(exit, 2);
    assert!(error.starts_with("REFUSED"), "{error}");
    assert!(!error.contains("(Parameter"));
    assert_eq!(output, "");
}

#[test]
fn the_text_is_the_header_the_groups_and_a_row_a_record_newest_first() {
    let directory = written();
    let (exit, output, error) = read(&directory, &AuditAsk::default(), false);
    let lines: Vec<&str> = output.split('\n').collect();

    assert_eq!(exit, 0);
    assert!(lines[0].starts_with("2 of 2 records in "), "{output}");
    assert!(lines[0].ends_with("audit.toml"), "{output}");
    assert!(
        lines
            .iter()
            .any(|line| line.trim_start().starts_with("host")),
        "{output}"
    );
    let heading = lines
        .iter()
        .position(|line| line.starts_with("at "))
        .expect("a heading");
    assert!(heading > 0, "{output}");
    assert!(lines[heading].contains("severity"));
    assert!(lines[heading + 1].contains("stopped"), "{output}");
    assert!(lines[heading + 2].contains("start"), "{output}");
    assert!(error.is_empty(), "{error}");
}

#[test]
fn json_is_the_read_whole_and_one_record_prints_every_field() {
    let directory = written();
    let warning = AuditAsk {
        severity: Some("Warning".into()),
        ..AuditAsk::default()
    };
    let (exit, json, _) = read(&directory, &warning, true);
    assert_eq!(exit, 0);
    let root: serde_json::Value = serde_json::from_str(&json).expect("one document");

    assert_eq!(root["read"], 2);
    assert_eq!(root["matched"], 1);
    let record = &root["records"][0];
    assert_eq!(record["action"], "stop");
    assert_eq!(record["properties"]["tested"], "yes");
    assert!(
        root["severities"]
            .as_array()
            .expect("severities")
            .iter()
            .any(|word| word == "Information")
    );

    let id = record["audit_id"].as_str().expect("an id").to_string();
    let one = AuditAsk {
        record: Some(id.clone()),
        ..AuditAsk::default()
    };
    let (exit, said, _) = read(&directory, &one, false);
    assert_eq!(exit, 0);
    assert!(said.contains(&format!("audit_id    {id}")), "{said}");
    assert!(said.contains("message     stopped"), "{said}");
    assert!(said.contains("  tested  yes"), "{said}");
}

#[test]
fn a_record_that_is_not_there_is_said_and_exits_one() {
    let directory = written();
    let missing = AuditAsk {
        record: Some("no-such-record".into()),
        ..AuditAsk::default()
    };
    let (exit, _, error) = read(&directory, &missing, false);

    assert_eq!(exit, 1);
    assert!(error.starts_with("No record no-such-record in "), "{error}");
}

#[test]
fn with_no_audit_directory_there_is_nothing_to_read_and_it_is_said() {
    const NAME: &str = "with_no_audit_directory_there_is_nothing_to_read_and_it_is_said";

    // A directory the environment states is the capability's to read; this
    // test runs again in a process of its own without it.
    if std::env::var_os("XMIP_AUDIT_DIRECTORY").is_some() {
        let ran = std::process::Command::new(std::env::current_exe().expect("this test"))
            .args(["--exact", NAME, "--test-threads", "1"])
            .env_remove("XMIP_AUDIT_DIRECTORY")
            .output()
            .expect("ran");
        assert!(
            ran.status.success(),
            "{}",
            String::from_utf8_lossy(&ran.stdout)
        );
        return;
    }

    let (mut output, mut error) = (Vec::new(), Vec::new());
    let exit = read_back(None, &AuditAsk::default(), false, &mut output, &mut error);
    let error = String::from_utf8(error).expect("UTF-8");

    assert_eq!(exit, 1);
    assert_eq!(error.trim(), english::NO_AUDIT_DIRECTORY);
    assert!(error.contains("operating system's log"));
}

#[test]
fn verify_walks_each_writer_s_chain_and_says_it_in_words_after_the_records() {
    // ADR-0070 clause 5: every record carries the digest of the one before
    // it in its writer's chain, and a verification says where it breaks, or
    // that it is whole.
    let asked = parsed(&["audit", "--verify"])
        .expect("parsed")
        .audit
        .expect("query");
    assert!(asked.verify);
    let directory = written();

    let (exit, text, _) = read(&directory, &asked, false);
    let (_, json, _) = read(&directory, &asked, true);
    assert_eq!(exit, 0);
    assert!(
        text.contains(&format!(
            "OK: the audit chain of {PROGRAM} is whole: 2 records"
        )),
        "{text}"
    );
    assert!(json.contains("\"whole\":true"), "{json}");

    let file = directory.join("audit.toml");
    let changed = std::fs::read_to_string(&file)
        .expect("written")
        .replace("\"stopped\"", "\"started\"");
    std::fs::write(&file, changed).expect("changed");
    let (exit, text, _) = read(&directory, &asked, false);

    assert_eq!(exit, 1);
    assert!(text.contains("FAILED: "), "{text}");
    assert!(text.contains("number 2: it was changed"), "{text}");
}

#[test]
fn verify_says_an_interrupted_write_and_fails_only_once_it_is_sealed() {
    // ADR-0070 clause 5, built 2026-10-12: the next append seals an
    // interrupted record; it is in no chain and said apart from them.
    let asked = parsed(&["audit", "--verify"])
        .expect("parsed")
        .audit
        .expect("query");
    let directory = written();
    let file = directory.join("audit.toml");
    let mut text = std::fs::read_to_string(&file).expect("written");
    text.push_str("[[record]]\naudit_id = \"0");
    std::fs::write(&file, text).expect("cut short");

    let (exit, open, _) = read(&directory, &asked, false);
    let (_, open_json, _) = read(&directory, &asked, true);
    assert_eq!(exit, 0);
    assert!(open.contains("UNFINISHED: "), "{open}");
    assert!(open_json.contains("\"sealed\":false"), "{open_json}");

    audit(&directory)
        .record(
            "stop",
            ExecutionPhase::Finished,
            Severity::Warning,
            Some("again"),
            BTreeMap::new(),
        )
        .expect("kept");
    let (exit, sealed, _) = read(&directory, &asked, false);

    assert_eq!(exit, 1);
    assert!(
        sealed.contains(&format!(
            "OK: the audit chain of {PROGRAM} is whole: 3 records"
        )),
        "{sealed}"
    );
    assert!(sealed.contains("FAILED: "), "{sealed}");
}
