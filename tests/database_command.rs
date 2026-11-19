//! `xmip database <server>`: the line names the server, the versions and the
//! folder; a line that cannot be obeyed is refused in words; and an
//! upgrade the schema has none of is the runtime's refusal, nothing
//! written.

use xmip_core_cli::command::Command;
use xmip_core_cli::command::database;
use xmip_core_cli::invocation::Invocation;
use xmip_core_cli::invocation::database_arguments::DatabaseAsk;

fn line(words: &[&str]) -> Vec<String> {
    words.iter().map(ToString::to_string).collect()
}

fn parse(words: &[&str]) -> Invocation {
    Invocation::parse(&line(words)).expect("a line that can be obeyed")
}

fn refused(words: &[&str]) -> String {
    Invocation::parse(&line(words)).expect_err("a line that cannot be obeyed")
}

#[test]
fn the_line_names_the_server_the_versions_and_the_folder() {
    let asked = parse(&["database", "postgresql", "--from", "1", "--out", "upgrade"]);

    assert_eq!(asked.command, Command::Database);
    assert_eq!(
        asked.database,
        Some(DatabaseAsk {
            server: "postgresql".into(),
            from: 1,
            to: 0,
            out: Some("upgrade".into()),
        })
    );
    assert_eq!(asked.certificate, None);
    assert_eq!(asked.audit, None);

    let named = parse(&["database", "mssql", "--from", "2", "--to", "3"]);
    assert_eq!(
        named.database,
        Some(DatabaseAsk {
            server: "mssql".into(),
            from: 2,
            to: 3,
            out: None,
        })
    );
}

#[test]
fn a_line_that_cannot_be_obeyed_is_refused_in_words() {
    assert!(refused(&["database", "mariadb"]).contains("needs --from"));
    assert!(
        refused(&["database", "mariadb", "--from", "one"])
            .contains("a whole number from 1, not 'one'")
    );
    assert!(
        refused(&["database", "mariadb", "--from", "1", "--to", "0"])
            .contains("--to needs a schema version")
    );
    assert!(refused(&["database", "--from", "1"]).contains("exactly one argument"));
    assert!(
        refused(&[
            "database",
            "mariadb",
            "--from",
            "1",
            "--operator",
            "alice@example.org"
        ])
        .contains("The options of 'certificate' only apply to 'certificate'")
    );
    assert_eq!(
        refused(&["database", "mariadb", "--from", "1", "--severity", "Error"]),
        "--severity only applies to 'audit'."
    );
}

#[test]
fn the_runtime_refuses_an_upgrade_that_is_none_and_nothing_is_written() {
    let place = std::env::temp_dir().join(format!("xmip-database-{}", std::process::id()));
    let place = place.display().to_string();
    let asked = parse(&[
        "database",
        "postgresql",
        "--from",
        "1",
        "--out",
        &place,
        "--json",
    ]);
    let (mut output, mut error) = (Vec::new(), Vec::new());

    let exit = database::run(&asked, &mut output, &mut error);

    assert_eq!(exit, 1);
    assert_eq!(output.len(), 0, "nothing said on stdout");
    let refused: serde_json::Value = serde_json::from_slice(&error).expect("one document");
    let said = refused["refused"].as_str().unwrap_or_default();
    assert!(said.contains("is none"), "{said}");
    assert!(!std::path::Path::new(&place).exists(), "nothing written");
}
