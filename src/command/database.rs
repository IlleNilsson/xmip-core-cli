//! `xmip database <server> --from <version> [--to <version>] [--out
//! <folder>]`: the scripts that upgrade each database on a server from the
//! schema version it holds to a later one (ADR-0015, amendment 2026-10-20),
//! said or written into a folder, never over a file. The scripts are
//! `operate::database`'s, over Xmip Storage's schema; only the rendering is
//! here.

use std::io::Write;
use std::path::{Path, PathBuf};

use operate::database::{self, Upgrade};

use crate::invocation::Invocation;
use crate::invocation::database_arguments::DatabaseAsk;
use crate::json::Json;
use crate::say;

/// Answer the line's `database`.
pub fn run(invocation: &Invocation, output: &mut dyn Write, error: &mut dyn Write) -> i32 {
    let asked = invocation.database.clone().unwrap_or_else(|| DatabaseAsk {
        server: invocation.argument.clone(),
        from: 0,
        to: 0,
        out: None,
    });

    answer(&asked, invocation.json, output, error)
}

/// Answer `asked`: exit 0 with the scripts, 1 where the upgrade or its
/// writing is refused.
pub fn answer(
    asked: &DatabaseAsk,
    json: bool,
    output: &mut dyn Write,
    error: &mut dyn Write,
) -> i32 {
    let upgrade = database::upgrade(&asked.server, asked.from, asked.to);
    let written = match (&upgrade, &asked.out) {
        (Ok(upgrade), Some(folder)) => Some(database::write(upgrade, &full(folder))),
        _ => None,
    };

    let refused = match (&upgrade, &written) {
        (Err(refused), _) | (Ok(_), Some(Err(refused))) => Some(refused.clone()),
        _ => None,
    };
    let (Ok(upgrade), None) = (upgrade, refused.as_ref()) else {
        let refused = refused.unwrap_or_default();
        say(
            error,
            &if json {
                Json::document(|json| json.string("refused", &refused))
            } else {
                refused
            },
        );
        return 1;
    };
    let written = written.and_then(Result::ok);

    if json {
        say(
            output,
            &Json::document(|json| document(json, &asked.server, &upgrade, written.as_deref())),
        );
        return 0;
    }

    for (at, (name, text)) in upgrade.scripts.iter().enumerate() {
        say(
            output,
            &match &written {
                None => format!("-- {name}\n{text}"),
                Some(paths) => format!("Wrote {}", paths[at].display()),
            },
        );
    }
    0
}

fn document(json: &mut Json, server: &str, upgrade: &Upgrade, written: Option<&[PathBuf]>) {
    json.string("server", server);
    json.number("version", upgrade.version);
    json.array("scripts", |json| {
        for (at, (name, text)) in upgrade.scripts.iter().enumerate() {
            json.object_value(|json| {
                json.string("name", name);
                match written {
                    None => json.string("text", text),
                    Some(paths) => json.string("written", &paths[at].display().to_string()),
                }
            });
        }
    });
}

fn full(path: &str) -> PathBuf {
    std::path::absolute(Path::new(path)).unwrap_or_else(|_| PathBuf::from(path))
}
