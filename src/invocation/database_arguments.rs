//! What `database` asks (ADR-0015, amendment 2026-10-20): the scripts that
//! upgrade each database on the server its argument names from the schema
//! version `--from` says it holds to `--to`, the newest where none is
//! given, written into the folder `--out` names or said.

/// What a line asks of `database`.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DatabaseAsk {
    pub server: String,
    pub from: u32,
    /// The version to reach; 0 for the newest.
    pub to: u32,
    pub out: Option<String>,
}

/// What the line asks: `server`, `--from`, `--to` and `--out`.
///
/// # Errors
///
/// No `--from`, or a version that is not a whole number from 1.
pub fn ask(
    server: &str,
    from: Option<&str>,
    to: Option<&str>,
    out: Option<&str>,
) -> Result<DatabaseAsk, String> {
    let Some(from) = from else {
        return Err(
            "'database' needs --from <version>, the schema version the databases hold.".to_string(),
        );
    };
    let held = version(from, "--from")?;
    let to = to.map(|to| version(to, "--to")).transpose()?;

    Ok(DatabaseAsk {
        server: server.to_string(),
        from: held,
        to: to.unwrap_or(0),
        out: out.map(str::to_string),
    })
}

fn version(value: &str, option: &str) -> Result<u32, String> {
    (!value.is_empty() && value.bytes().all(|byte| byte.is_ascii_digit()))
        .then(|| value.parse::<u32>().ok())
        .flatten()
        .filter(|&number| number > 0)
        .ok_or_else(|| {
            format!("{option} needs a schema version, a whole number from 1, not '{value}'.")
        })
}
