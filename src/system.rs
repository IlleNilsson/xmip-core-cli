//! What the command line asks the operating system, in one folder, the one
//! that may hold unsafe code: that Ctrl+C ends a follow cleanly rather than
//! the process mid-line ([`interrupt`]), and who the command runs as
//! ([`user`]).

pub mod interrupt;
pub mod user;
