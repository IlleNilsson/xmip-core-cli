//! What `report` asks (ADR-0072, amendment 2026-11-10): every Report, one
//! by its name, or one opened, its Quest probing `--configuration`, sliced
//! for `--node` where it is a cluster's, at `--parameter`, one `--row` in
//! full.

/// The options of `report` alone; `--node` is `certificate`'s too.
pub const OPTIONS: [&str; 3] = ["--configuration", "--parameter", "--row"];

/// What a line asks of `report`.
#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct ReportAsk {
    pub name: String,
    pub configuration: Option<String>,
    pub parameter: Option<String>,
    pub row: Option<String>,
    pub node: Option<String>,
}

/// Take the option at `index`: `None` where it is none of `report`'s;
/// else `Some`, with why where its value is missing.
pub fn take(
    args: &[String],
    index: &mut usize,
    ask: &mut Option<ReportAsk>,
) -> Option<Result<(), String>> {
    let option = args[*index].as_str();

    if !OPTIONS.contains(&option) {
        return None;
    }

    let Some(value) = args
        .get(*index + 1)
        .filter(|value| !value.trim().is_empty())
    else {
        return Some(Err(if option == "--configuration" {
            "--configuration needs the path of a node's configuration or a cluster.xmip."
                .to_string()
        } else {
            format!("{option} needs a value.")
        }));
    };
    *index += 1;

    let taken = ask.get_or_insert_with(ReportAsk::default);
    let value = Some(value.clone());
    match option {
        "--configuration" => taken.configuration = value,
        "--parameter" => taken.parameter = value,
        _ => taken.row = value,
    }

    Some(Ok(()))
}

/// What the line asks of `report`, its argument `name` and `node` from
/// `--node`.
///
/// # Errors
///
/// A probe naming no Report, or a probe's option with nothing to probe.
pub fn ask(name: &str, asked: Option<&ReportAsk>, node: Option<&str>) -> Result<ReportAsk, String> {
    let ask = ReportAsk {
        name: name.to_string(),
        node: node.map(str::to_string),
        ..asked.cloned().unwrap_or_default()
    };

    if ask.configuration.is_some() && ask.name.is_empty() {
        return Err(
            "'report --configuration <path>' needs the Report to open: 'report <name> \
                    --configuration <path>'."
                .to_string(),
        );
    }
    if ask.configuration.is_none()
        && (ask.parameter.is_some() || ask.row.is_some() || ask.node.is_some())
    {
        return Err(
            "--parameter, --row and --node open a Report that presents what a Quest \
                    probes, and its Quest probes a configuration: 'report <name> \
                    --configuration <path>'."
                .to_string(),
        );
    }

    Ok(ask)
}
