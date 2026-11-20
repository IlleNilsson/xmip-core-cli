//! `xmip report` (ADR-0072, amendment 2026-11-10): the line read — the
//! Report's name, `--configuration`, `--parameter` and `--row` its own,
//! `--node` shared with `certificate` — and every Report listed, one shown
//! and one probed through the runtime. Which Reports there are and what a
//! probe finds are `xmip-core-report`'s and tested there; an entry is found
//! here by what it holds, never by its name.

use file::temporary::Folder;
use serde_json::Value;
use xmip_core_cli::command::Command;
use xmip_core_cli::command::report::{Audited, answer};
use xmip_core_cli::invocation::Invocation;
use xmip_core_cli::invocation::report_arguments::ReportAsk;

fn line(words: &[&str]) -> Vec<String> {
    words.iter().map(ToString::to_string).collect()
}

fn parse(words: &[&str]) -> Invocation {
    Invocation::parse(&line(words)).expect("a line that can be obeyed")
}

fn refused(words: &[&str]) -> String {
    Invocation::parse(&line(words)).expect_err("a line that cannot be obeyed")
}

// An audit directory of the test's own, gone when it is dropped.
struct Directory(Folder);

impl Directory {
    fn new(name: &str) -> Self {
        Self(Folder::new(&format!("cli-report-{name}")))
    }

    // `asked` answered, audited here: its exit, what it said and what it
    // complained of.
    fn run(&self, asked: &ReportAsk, json: bool) -> (i32, String, String) {
        let audited = Audited {
            program: "xmip",
            directory: Some(self.0.to_path_buf()),
        };
        let (mut output, mut error) = (Vec::new(), Vec::new());
        let exit = answer(asked, &audited, json, &mut output, &mut error);
        (
            exit,
            String::from_utf8(output).expect("UTF-8"),
            String::from_utf8(error).expect("UTF-8").trim().to_string(),
        )
    }

    // Every Report, as the runtime lists them.
    fn listing(&self) -> Value {
        let (exit, said, error) = self.run(&ReportAsk::default(), true);
        assert_eq!(exit, 0, "{error}");
        serde_json::from_str(&said).expect("one document")
    }
}

fn entries(listing: &Value) -> Vec<Value> {
    listing["reports"].as_array().cloned().unwrap_or_default()
}

fn kind_name(listing: &Value, entry: &Value) -> String {
    listing["kinds"]
        .as_array()
        .into_iter()
        .flatten()
        .find(|kind| kind["kind"] == entry["kind"])
        .and_then(|kind| kind["name"].as_str())
        .unwrap_or_default()
        .to_string()
}

fn text<'v>(value: &'v Value, name: &str) -> &'v str {
    value[name].as_str().unwrap_or_default()
}

#[test]
fn the_line_names_the_report_and_what_its_quest_probes() {
    let all = parse(&["report"]);
    assert_eq!(all.command, Command::Report);
    assert_eq!(all.report, Some(ReportAsk::default()));
    assert_eq!(all.report.and_then(|ask| ask.configuration), None);

    let cluster = configure::fixture::test_cluster();
    let node = cluster.node(0).name.clone();
    let probing = parse(&[
        "report",
        "q",
        "--configuration",
        "cluster.xmip",
        "--node",
        &node,
        "--parameter",
        "p",
        "--row",
        "r",
        "--json",
    ]);
    assert_eq!(
        probing.report,
        Some(ReportAsk {
            name: "q".into(),
            configuration: Some("cluster.xmip".into()),
            parameter: Some("p".into()),
            row: Some("r".into()),
            node: Some(node),
        })
    );
    assert_eq!(probing.certificate, None);
}

#[test]
fn a_line_that_cannot_be_obeyed_is_refused_in_words() {
    let cluster = configure::fixture::test_cluster();
    let node = cluster.node(0).name.clone();

    assert!(refused(&["report", "--configuration", "c"]).contains("needs the Report to open"));
    assert!(refused(&["report", "Q", "--parameter", "GDPR"]).contains("--configuration <path>"));
    assert!(refused(&["report", "Q", "--node", &node]).contains("--configuration <path>"));
    assert_eq!(
        refused(&["health", "x", "--row", "r"]),
        "--row only applies to 'report'."
    );
    assert_eq!(
        refused(&[
            "certificate",
            "--configuration",
            "c",
            "--operator",
            "a@example.org"
        ]),
        "--configuration only applies to 'report'."
    );
    assert!(refused(&["report", "--pause"]).contains("only applies to 'event-subscriptions'"));
    assert_eq!(
        refused(&["report", "--severity", "x"]),
        "--severity only applies to 'audit'."
    );
    assert!(refused(&["report", "a", "b"]).contains("at most one argument"));
}

#[test]
fn the_list_says_every_entry_with_its_kind_s_name() {
    let audit = Directory::new("list");
    let listing = audit.listing();
    let (exit, said, _) = audit.run(&ReportAsk::default(), false);

    assert_eq!(exit, 0);
    for kind in listing["kinds"].as_array().expect("kinds") {
        assert!(
            said.contains(&format!("{}  ", text(kind, "name"))),
            "{said}"
        );
    }
    for entry in entries(&listing) {
        let opening = format!("  {} ", kind_name(&listing, &entry));
        assert!(
            said.lines().any(|line| line.starts_with(&opening)
                && line[opening.len()..]
                    .trim_start()
                    .starts_with(text(&entry, "name"))),
            "{said}"
        );
    }
    assert_ne!(entries(&listing).len(), 0, "the runtime lists Reports");
}

#[test]
fn a_quick_entry_is_shown_with_the_command_that_presents_it() {
    let audit = Directory::new("quick");
    let listing = audit.listing();
    let quick = entries(&listing)
        .into_iter()
        .find(|entry| entry["read_by"] == ::abi::operate::audit::AUDIT_READ_ENTRYPOINT)
        .expect("the audit's Report");
    let asked = ReportAsk {
        name: text(&quick, "name").to_string(),
        ..ReportAsk::default()
    };

    let (exit, said, _) = audit.run(&asked, false);
    assert_eq!(exit, 0);
    assert!(said.contains("Presented by: xmip audit"), "{said}");
    assert!(said.contains(text(&quick, "covers")), "{said}");

    // As JSON, the runtime's own answer for the one entry.
    let (exit, said, _) = audit.run(&asked, true);
    let one: Value = serde_json::from_str(&said).expect("one document");
    assert_eq!(exit, 0);
    assert_eq!(entries(&one).len(), 1);
    assert_eq!(entries(&one)[0]["name"], quick["name"]);
}

#[test]
fn a_probe_says_the_tally_and_a_row_per_requirement_or_one_in_full() {
    let audit = Directory::new("probe");
    let listing = audit.listing();
    let quest = listing["kinds"]
        .as_array()
        .into_iter()
        .flatten()
        .find(|kind| kind["word"] == "quest")
        .map(|kind| kind["kind"].clone())
        .expect("the kind a Quest is");
    let entry = entries(&listing)
        .into_iter()
        .find(|entry| entry["kind"] == quest && entry["available"] == true)
        .expect("a Quest built");
    let cluster = configure::fixture::test_cluster();
    let asked = ReportAsk {
        name: text(&entry, "name").to_string(),
        configuration: Some(cluster.path.display().to_string()),
        parameter: entry["parameter"]["choices"][0]
            .as_str()
            .map(str::to_string),
        row: None,
        node: Some(cluster.with_role("storage").name.clone()),
    };

    let (exit, said, error) = audit.run(&asked, true);
    assert_eq!(exit, 0, "{error}");
    let probed: Value = serde_json::from_str(&said).expect("one document");
    let row = text(&probed["rows"][0], "name").to_string();
    let tally: Vec<String> = probed["tally"]
        .as_array()
        .into_iter()
        .flatten()
        .map(|count| format!("{} {}", text(count, "state"), count["count"]))
        .collect();

    let (exit, said, _) = audit.run(&asked, false);
    assert_eq!(exit, 0);
    assert!(said.contains(&format!("  {}", tally.join(", "))), "{said}");
    assert!(
        said.lines().any(|line| {
            let words: Vec<&str> = line.split_whitespace().collect();
            line.starts_with("  state ")
                && words.get(1) == Some(&text(&probed, "row"))
                && words.last() == Some(&"citation")
        }),
        "{said}"
    );
    assert!(said.contains(&row), "{said}");
    assert!(said.contains("nothing changed"), "{said}");

    let (exit, said, _) = audit.run(
        &ReportAsk {
            row: Some(row.clone()),
            ..asked
        },
        false,
    );
    assert_eq!(exit, 0);
    assert!(said.contains(&format!("{row} (")), "{said}");
}

#[test]
fn a_refusal_exits_one_in_the_runtime_s_words_and_as_json() {
    let audit = Directory::new("refused");
    let listing = audit.listing();
    let quick = entries(&listing)
        .into_iter()
        .find(|entry| entry["read_by"].is_string())
        .expect("a quick Report");
    let cluster = configure::fixture::test_cluster();
    // A cluster's file and no node: refused for the Report, before the file
    // is read.
    let opening = ReportAsk {
        name: text(&quick, "name").to_string(),
        configuration: Some(cluster.path.display().to_string()),
        ..ReportAsk::default()
    };

    let (exit, _, complaint) = audit.run(&opening, false);
    assert_eq!(exit, 1);
    assert!(complaint.starts_with("REFUSED:"), "{complaint}");
    assert!(complaint.contains(text(&quick, "read_by")), "{complaint}");

    let (exit, _, complaint) = audit.run(&opening, true);
    let refused: Value = serde_json::from_str(&complaint).expect("one document");
    assert_eq!(exit, 1);
    assert!(
        text(&refused, "refused").starts_with("REFUSED:"),
        "{complaint}"
    );

    let nowhere = ReportAsk {
        name: "nowhere".into(),
        ..ReportAsk::default()
    };
    let (exit, _, complaint) = audit.run(&nowhere, false);
    assert_eq!(exit, 1);
    assert!(complaint.contains(text(&quick, "name")), "{complaint}");
}
