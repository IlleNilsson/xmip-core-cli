//! The text behind `xmip help`: every command's description starts at one
//! column, a command alone on its line described on the lines below it.

use xmip_core_cli::usage;

/// The command a line names, its arguments with it — `xmip` then words
/// each after one space — and where its description starts, past a gap of
/// two spaces or more; `None` for a line that goes on with the one above.
fn command(line: &str) -> Option<(String, Option<usize>)> {
    let indent = line.len() - line.trim_start().len();

    if !line[indent..].starts_with("xmip") {
        return None;
    }

    let mut end = indent + "xmip".len();
    while let Some(word) = line[end..].strip_prefix(' ') {
        let length = word.find(char::is_whitespace).unwrap_or(word.len());
        if length == 0 {
            break;
        }
        end += 1 + length;
    }

    let after = &line[end..];
    let gap = after.len() - after.trim_start().len();
    let described = (gap >= 2 && gap < after.len()).then_some(end + gap);

    Some((line[indent..end].trim_end().to_string(), described))
}

#[test]
fn every_command_s_description_starts_at_one_column() {
    let text = usage::text();
    let lines: Vec<&str> = text
        .split('\n')
        .map(|line| line.trim_end_matches('\r'))
        .collect();
    let named = |line: &str| command(line).map(|(named, _)| named);
    let first = lines
        .iter()
        .position(|line| named(line).as_deref() == Some("xmip abi"));
    let last = lines
        .iter()
        .position(|line| named(line).as_deref() == Some("xmip help"));
    let (Some(first), Some(last)) = (first, last) else {
        panic!("the commands are listed");
    };
    let column = command(lines[first])
        .and_then(|(_, described)| described)
        .expect("described");
    assert!(last > first && column > 0, "the commands are listed");

    for line in &lines[first..=last] {
        let parsed = command(line);
        if line.trim().is_empty() || matches!(parsed, Some((_, None))) {
            // A command alone on its line: its description is below it.
            continue;
        }
        let starts = match parsed {
            Some((_, Some(described))) => described,
            _ => line.len() - line.trim_start().len(),
        };
        assert_eq!(
            starts,
            column,
            "'{}' starts at {starts}, not {column}",
            line.trim()
        );
    }
}

#[test]
fn no_option_names_a_runtime_library_the_executable_links() {
    // The runtime is linked into the executable (ADR-0014, amendment
    // 2026-11-17): no line names a library to load it from.
    let text = usage::text();

    assert!(!text.contains("--runtime"), "{text}");
    assert!(!text.contains("XMIP_RUNTIME_LIBRARY"), "{text}");
}
