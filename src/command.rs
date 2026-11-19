//! The commands `xmip` answers, one word each (ADR-0014 clause 8), and how
//! many arguments each takes: none, one, or one at most; and each command's
//! answer, one module each, rendered over the writers it is handed.

pub mod abi;
pub mod audit;
pub mod certificate;
pub mod database;
pub mod follow;
pub mod health;
pub mod json_selection;
pub mod measure;
pub mod noun;
pub mod probe;
pub mod report;
pub mod scope;
pub mod status;
pub mod validate;

/// One command.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Command {
    /// Print the usage text.
    Help,
    /// The two boundaries this build speaks.
    Abi,
    /// What a status code means.
    Status,
    /// Load a module and report what it says it is.
    Probe,
    /// Health at and beneath a scope, from the runtime.
    Health,
    /// The six figures at a scope.
    Measure,
    /// The direct children of a scope, one row each.
    List,
    /// One scope as a row: its mood, its evidence, its figures.
    Show,
    /// Pause everything at and beneath a scope.
    Pause,
    /// Resume everything at and beneath a scope.
    Resume,
    /// Check a node configuration without starting it.
    Validate,
    /// A person's or a node's certificate, and the cluster's authority
    /// (ADR-0034, amendments 2026-10-15 and 2026-10-22).
    Certificate,
    /// The scripts that upgrade each database on a server (ADR-0015,
    /// amendment 2026-10-20).
    Database,
    /// What the audit recorded (ADR-0062, amendment 2026-09-29).
    Audit,
    /// The Event subscriptions the nodes hold, and an act on one (ADR-0065,
    /// amendment 2026-09-29).
    EventSubscriptions,
    /// The Subscriptions the nodes route by, and an act on one (ADR-0013,
    /// amendment 2026-09-30).
    Subscriptions,
    /// What each node's Dead Message Queue keeps, and Replay on one
    /// (ADR-0052, amendment 2026-10-01).
    DeadMessages,
    /// The Messages that failed, and Retry or Dismiss on one
    /// (runtime-model.md section 13).
    Message,
    /// The secrets a node keeps, never a value, and Set on one (ADR-0019,
    /// amendment 2026-10-29).
    Secret,
    /// Every Report, and one opened (ADR-0072, amendment 2026-11-10).
    Report,
    /// A Model Context Protocol server over standard input and output
    /// (ADR-0071).
    Mcp,
}

impl Command {
    /// Every command's word, its command, and the fewest and most
    /// arguments it takes.
    pub const KNOWN: [(&'static str, Self, usize, usize); 21] = [
        ("help", Self::Help, 0, 0),
        ("abi", Self::Abi, 0, 0),
        ("status", Self::Status, 1, 1),
        ("probe", Self::Probe, 1, 1),
        ("health", Self::Health, 1, 1),
        ("measure", Self::Measure, 0, 1),
        ("list", Self::List, 0, 1),
        ("show", Self::Show, 1, 1),
        ("pause", Self::Pause, 1, 1),
        ("resume", Self::Resume, 1, 1),
        ("validate", Self::Validate, 1, 1),
        ("certificate", Self::Certificate, 0, 1),
        ("database", Self::Database, 1, 1),
        ("audit", Self::Audit, 0, 1),
        ("event-subscriptions", Self::EventSubscriptions, 0, 1),
        ("subscriptions", Self::Subscriptions, 0, 1),
        ("dead-messages", Self::DeadMessages, 0, 1),
        ("message", Self::Message, 0, 1),
        ("secret", Self::Secret, 0, 1),
        ("report", Self::Report, 0, 1),
        ("mcp", Self::Mcp, 0, 0),
    ];

    /// The command `word` names, with the fewest and most arguments it
    /// takes.
    #[must_use]
    pub fn named(word: &str) -> Option<(Self, usize, usize)> {
        Self::KNOWN
            .iter()
            .find(|(known, ..)| *known == word)
            .map(|&(_, command, fewest, most)| (command, fewest, most))
    }

    /// The word it is typed as.
    #[must_use]
    pub fn word(self) -> &'static str {
        Self::KNOWN
            .iter()
            .find(|(_, command, ..)| *command == self)
            .map_or("help", |(word, ..)| word)
    }

    /// Whether it lists one of the nouns a node publishes for an operator
    /// to act on, or a node's secrets.
    #[must_use]
    pub const fn is_noun(self) -> bool {
        matches!(
            self,
            Self::EventSubscriptions
                | Self::Subscriptions
                | Self::DeadMessages
                | Self::Message
                | Self::Secret
        )
    }
}
