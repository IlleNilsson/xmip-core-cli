//! What the nouns' tests share: a publication written to a folder of its
//! own and read as a snapshot, a line answered by the noun it names, and
//! the words a person reads found in their order whatever the spacing.

#![allow(dead_code, reason = "each test file uses its own part")]

use std::path::PathBuf;
use std::sync::Arc;
use std::sync::atomic::AtomicBool;

use file::temporary::Folder;
use operate::surface::OperatorSurface;
use operate::surface::snapshot::SnapshotSurface;
use xmip_core_cli::command::noun;
use xmip_core_cli::invocation::Invocation;
use xmip_core_cli::run::Writers;
use xmip_core_cli::system::user;

/// A publication in a folder of its own, gone with it.
pub struct Published {
    folder: Folder,
    pub file: PathBuf,
}

impl Published {
    /// `text` written as `<cluster>-snapshot.xmip` in a new folder.
    pub fn new(kind: &str, cluster: &str, text: &str) -> Self {
        let folder = Folder::new(&format!("cli-{kind}"));
        let file = folder.join(format!("{cluster}-snapshot.xmip"));
        std::fs::write(&file, text).expect("written");

        Self { folder, file }
    }

    /// The publication's surface.
    pub fn surface(&self) -> SnapshotSurface {
        SnapshotSurface::new(&self.file, Arc::new(xcore::SystemClock))
    }

    /// Another file in its folder, written with `text`.
    pub fn beside(&self, name: &str, text: &str) -> PathBuf {
        let file = self.folder.join(name);
        std::fs::write(&file, text).expect("written");
        file
    }
}

/// The line `args` says, which must parse.
pub fn line(args: &[&str]) -> Invocation {
    let words: Vec<String> = args.iter().map(ToString::to_string).collect();
    Invocation::parse(&words).unwrap_or_else(|problem| panic!("{problem}"))
}

/// Why the line `args` cannot be obeyed.
pub fn refused(args: &[&str]) -> String {
    let words: Vec<String> = args.iter().map(ToString::to_string).collect();
    Invocation::parse(&words).expect_err("refused")
}

/// What `invocation`'s noun answered over `surface`, its input `input`:
/// its exit, its output and its error.
pub fn run(
    surface: &dyn OperatorSurface,
    invocation: &Invocation,
    input: &str,
) -> (i32, String, String) {
    let who = operate::surface::acted::who(invocation.who.as_deref(), &user::name());
    let (mut output, mut error) = (Vec::new(), Vec::new());
    let mut input = input.as_bytes();
    let stop = AtomicBool::new(false);
    let exit = noun::answer(
        surface,
        invocation,
        &who,
        &mut Writers {
            output: &mut output,
            error: &mut error,
            input: &mut input,
            stop: &stop,
        },
    );

    (
        exit,
        String::from_utf8(output).expect("UTF-8"),
        String::from_utf8(error).expect("UTF-8"),
    )
}

/// Whether `parts` stand in `text` in their order, each run of white space
/// between them of any length, as a person's columns hold them.
pub fn in_order(text: &str, parts: &[&str]) -> bool {
    let collapsed = text.split_whitespace().collect::<Vec<_>>().join(" ");
    let wanted = parts
        .iter()
        .map(|part| part.split_whitespace().collect::<Vec<_>>().join(" "))
        .collect::<Vec<_>>()
        .join(" ");
    collapsed.contains(&wanted)
}

/// A JSON document, read.
pub fn document(text: &str) -> serde_json::Value {
    serde_json::from_str(text).unwrap_or_else(|unread| panic!("{unread}: {text}"))
}
