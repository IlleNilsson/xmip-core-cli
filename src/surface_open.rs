//! Which surface a command reads: the line over `cli.toml` beside the
//! executable, by the one precedence every surface shares
//! (`operate::surface::choice`; ADR-0052 clause 1 and its amendments of
//! 2026-09-18 and 2026-09-20). What is the executable's own is only where
//! its document lies, and what it says of the audit and the purpose.
//!
//! One surface, never a set of them (ADR-0052, amendment 2026-09-20): an
//! invocation answers one question at one scope and ends, and a rollup or a
//! sum over two clusters would be a figure at a scope that is in neither
//! tree. `--snapshot` names which cluster; where the document names
//! several, the first is read.

use std::path::{Path, PathBuf};
use std::sync::Arc;

use operate::surface::OperatorSurface;
use operate::surface::choice::table::COMMAND_LINE;
use operate::surface::choice::{self, Chosen, Document};

use crate::invocation::Invocation;

/// A surface a line opened, released when dropped.
pub type Surface = Box<dyn OperatorSurface + Send + Sync>;

/// Opens the surface a line reads, or says why not: [`open`] is the
/// executable's; a test hands one of its own.
pub type Opener<'a> = &'a dyn Fn(&Invocation) -> Result<Surface, String>;

/// The folder the executable is in.
#[must_use]
pub fn beside() -> PathBuf {
    std::env::current_exe().map_or_default(|executable| folder(&executable))
}

/// The folder of the program `executable` names, its links followed on
/// Unix: `/usr/bin/xmip`, the link a package puts on the path, finds the
/// document beside `/opt/xmip/bin/xmip`. Not on Windows, where a package
/// puts `bin` itself on the path and the canonical form is verbatim,
/// `\\?\C:\...`, in which a document's `/` is no separator.
#[must_use]
pub fn folder(executable: &Path) -> PathBuf {
    #[cfg(unix)]
    let executable =
        &std::fs::canonicalize(executable).unwrap_or_else(|_| executable.to_path_buf());

    executable.parent().map_or_default(Path::to_path_buf)
}

/// The executable's document.
#[must_use]
pub fn document() -> Document {
    Document::read(&beside().join(COMMAND_LINE))
}

/// The surface `invocation` reads, where it answers; else why not.
///
/// # Errors
///
/// The document's refusal, or the surface's source saying why it does not
/// answer.
pub fn open(invocation: &Invocation) -> Result<Surface, String> {
    let clock: Arc<dyn xcore::Clock> = Arc::new(xcore::SystemClock);

    choice::answering(&invocation.line(), &beside().join(COMMAND_LINE), &clock).map(boxed)
}

/// A chosen surface, boxed.
#[must_use]
pub fn boxed(chosen: Chosen) -> Surface {
    match chosen {
        Chosen::Snapshot(snapshot) => Box::new(snapshot),
        Chosen::Listener(node) => Box::new(node),
    }
}

/// Where this executable's audit records go: `AuditDirectory` in its
/// document, resolved beside it; `None` where it names none, and the
/// capability decides.
#[must_use]
pub fn audit_directory(document: &Document) -> Option<PathBuf> {
    document
        .value("AuditDirectory")
        .filter(|named| !named.trim().is_empty())
        .map(|named| choice::resolve(Path::new(named), &document.base))
}

/// What this process is for, as its document says: `test` or `runtime`,
/// `runtime` where it says nothing.
#[must_use]
pub fn purpose(document: &Document) -> String {
    document
        .value("Purpose")
        .filter(|word| !word.trim().is_empty())
        .unwrap_or("runtime")
        .to_string()
}
