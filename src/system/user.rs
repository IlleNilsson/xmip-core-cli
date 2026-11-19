//! Who the command runs as: the account's name, as a person who paused a
//! scope or an audit record names the user it ran as.

/// The account this process runs as; empty where the system names none.
#[must_use]
pub fn name() -> String {
    named().unwrap_or_default()
}

#[cfg(windows)]
fn named() -> Option<String> {
    std::env::var("USERNAME").ok()
}

#[cfg(unix)]
fn named() -> Option<String> {
    nix::unistd::User::from_uid(nix::unistd::geteuid())
        .ok()
        .flatten()
        .map(|user| user.name)
        .or_else(|| std::env::var("USER").ok())
}

#[cfg(not(any(windows, unix)))]
fn named() -> Option<String> {
    std::env::var("USER").ok()
}
