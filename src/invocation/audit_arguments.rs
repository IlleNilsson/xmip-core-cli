//! What `audit` asks of the audit, in the words every surface uses (ADR-0062,
//! amendment 2026-09-29): the pattern and every option, each the word its
//! key is; `--location`, `--sort` and `--order` are every noun's too, and
//! `--offset` and `--limit` a failed Message's list's, `--from` and `--to`
//! `database`'s.

/// The options that take a value.
pub const OPTIONS: [&str; 12] = [
    "--location",
    "--host",
    "--program",
    "--record",
    "--severity",
    "--action",
    "--from",
    "--to",
    "--sort",
    "--order",
    "--offset",
    "--limit",
];

/// Walk each writer's audit chain.
pub const VERIFY: &str = "--verify";

/// What a line asks of the audit.
#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct AuditAsk {
    pub pattern: Option<String>,
    pub location: Option<String>,
    pub host: Option<String>,
    pub program: Option<String>,
    pub record: Option<String>,
    pub severity: Option<String>,
    pub action: Option<String>,
    pub from: Option<String>,
    pub to: Option<String>,
    pub sort: Option<String>,
    pub order: Option<String>,
    pub offset: u32,
    pub limit: u32,
    pub verify: bool,
}

impl AuditAsk {
    /// Each word a reader takes and its value, the empty and the unset left
    /// out, in the order the capability lists them.
    #[must_use]
    pub fn pairs(&self) -> Vec<(String, String)> {
        let offset = (self.offset > 0).then(|| self.offset.to_string());
        let limit = (self.limit > 0).then(|| self.limit.to_string());
        let said = [
            ("pattern", self.pattern.clone()),
            ("location", self.location.clone()),
            ("host", self.host.clone()),
            ("program", self.program.clone()),
            ("record", self.record.clone()),
            ("severity", self.severity.clone()),
            ("action", self.action.clone()),
            ("from", self.from.clone()),
            ("to", self.to.clone()),
            ("sort", self.sort.clone()),
            ("order", self.order.clone()),
            ("offset", offset),
            ("limit", limit),
            ("verify", self.verify.then(|| "yes".to_string())),
        ];

        said.into_iter()
            .filter_map(|(key, value)| {
                value
                    .filter(|value| !value.trim().is_empty())
                    .map(|value| (key.to_string(), value))
            })
            .collect()
    }

    /// Whether newest comes first: unless `--order ascending`.
    #[must_use]
    pub fn descending(&self) -> bool {
        self.order.as_deref() != Some("ascending")
    }
}

/// Take the option at `index`: `None` where it is none of audit's; else
/// `Some`, with why the line cannot be obeyed where its value is missing or
/// no whole number. `index` moves past a value taken.
pub fn take(
    args: &[String],
    index: &mut usize,
    ask: &mut Option<AuditAsk>,
) -> Option<Result<(), String>> {
    let option = args[*index].as_str();

    if option == VERIFY {
        ask.get_or_insert_with(AuditAsk::default).verify = true;
        return Some(Ok(()));
    }
    if !OPTIONS.contains(&option) {
        return None;
    }

    let word = &option[2..];
    let counted = matches!(word, "offset" | "limit");
    let Some(value) = args
        .get(*index + 1)
        .filter(|value| !value.trim().is_empty())
    else {
        return Some(Err(if counted {
            format!("{option} needs a whole number.")
        } else {
            format!("{option} needs a value.")
        }));
    };
    *index += 1;

    let number = if counted {
        match whole(value) {
            Some(number) => number,
            None => {
                return Some(Err(format!(
                    "{option} needs a whole number, not '{value}'."
                )));
            }
        }
    } else {
        0
    };
    let asked = ask.get_or_insert_with(AuditAsk::default);
    let value = Some(value.clone());

    match word {
        "location" => asked.location = value,
        "host" => asked.host = value,
        "program" => asked.program = value,
        "record" => asked.record = value,
        "severity" => asked.severity = value,
        "action" => asked.action = value,
        "from" => asked.from = value,
        "to" => asked.to = value,
        "sort" => asked.sort = value,
        "order" => asked.order = value,
        "offset" => asked.offset = number,
        _ => asked.limit = number,
    }

    Some(Ok(()))
}

/// A whole number as .NET reads one with no sign, no space and no
/// separator, up to the largest 32-bit integer.
#[must_use]
pub fn whole(text: &str) -> Option<u32> {
    if text.is_empty() || !text.bytes().all(|byte| byte.is_ascii_digit()) {
        return None;
    }
    text.parse::<u32>()
        .ok()
        .filter(|&number| i32::try_from(number).is_ok())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn line(words: &[&str]) -> Vec<String> {
        words.iter().map(ToString::to_string).collect()
    }

    #[test]
    fn an_option_takes_its_value_and_a_count_a_whole_number() {
        let args = line(&["--limit", "5", "--offset", "x", "--from"]);
        let mut ask = None;
        let mut at = 0;

        assert_eq!(take(&args, &mut at, &mut ask), Some(Ok(())));
        assert_eq!((at, ask.as_ref().map(|ask| ask.limit)), (1, Some(5)));
        at = 2;
        assert_eq!(
            take(&args, &mut at, &mut ask),
            Some(Err("--offset needs a whole number, not 'x'.".into()))
        );
        at = 4;
        assert_eq!(
            take(&args, &mut at, &mut ask),
            Some(Err("--from needs a value.".into()))
        );
        assert_eq!(whole("+5"), None);
        assert_eq!(whole("2147483648"), None);
    }

    #[test]
    fn the_pairs_leave_out_what_is_not_said() {
        let scope = configure::fixture::test_cluster().scope();
        let ask = AuditAsk {
            location: Some(scope.clone()),
            sort: Some(" ".into()),
            limit: 3,
            verify: true,
            ..AuditAsk::default()
        };

        assert_eq!(
            ask.pairs(),
            [
                ("location".to_string(), scope),
                ("limit".to_string(), "3".to_string()),
                ("verify".to_string(), "yes".to_string()),
            ]
        );
        assert!(ask.descending());
    }
}
