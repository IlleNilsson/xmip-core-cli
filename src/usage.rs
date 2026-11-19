//! The text behind `xmip help`: every command and option, which surface a
//! line reads, and every Report.

mod commands;
mod reports;
mod surfaces;

use std::io::Write;

/// The usage text whole.
#[must_use]
pub fn text() -> String {
    format!(
        "{}\n\n{}\n\n{}",
        commands::USAGE,
        surfaces::USAGE,
        reports::USAGE
    )
}

/// Print the usage text: exit 0.
pub fn print(output: &mut dyn Write) -> i32 {
    crate::say(output, &text());
    0
}
