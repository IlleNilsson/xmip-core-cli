//! Rows as a person reads them: each column as wide as its widest cell, two
//! spaces between, the last cell as it is and nothing trailing.

use std::io::Write;

/// Write `rows`, each line opening with `indent`; nothing for none.
pub fn write(output: &mut dyn Write, indent: &str, rows: &[Vec<String>]) {
    let Some(first) = rows.first() else {
        return;
    };

    let widths: Vec<usize> = (0..first.len())
        .map(|column| {
            rows.iter()
                .map(|row| row.get(column).map_or(0, |cell| width(cell)))
                .max()
                .unwrap_or(0)
        })
        .collect();

    for row in rows {
        let mut line = String::from(indent);
        for (column, cell) in row.iter().enumerate() {
            if column > 0 {
                line.push_str("  ");
            }
            line.push_str(cell);
            if column + 1 < row.len() {
                let pad = widths
                    .get(column)
                    .copied()
                    .unwrap_or(0)
                    .saturating_sub(width(cell));
                line.extend(std::iter::repeat_n(' ', pad));
            }
        }
        crate::say(output, line.trim_end());
    }
}

/// How wide a cell is, counted as .NET pads a column: in UTF-16 units.
#[must_use]
pub fn width(cell: &str) -> usize {
    cell.encode_utf16().count()
}

/// `text` padded with spaces to `width` UTF-16 units on its right, as
/// .NET's `{text,-width}` pads it.
#[must_use]
pub fn left(text: &str, width: usize) -> String {
    let pad = width.saturating_sub(self::width(text));
    format!("{text}{}", " ".repeat(pad))
}

/// `text` padded with spaces to `width` UTF-16 units on its left, as
/// .NET's `{text,width}` pads it.
#[must_use]
pub fn right(text: &str, width: usize) -> String {
    let pad = width.saturating_sub(self::width(text));
    format!("{}{text}", " ".repeat(pad))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn columns_are_as_wide_as_their_widest_and_the_last_trails_nothing() {
        let rows = vec![
            vec!["a".to_string(), "bb".to_string(), "c".to_string()],
            vec!["aaa".to_string(), "b".to_string(), String::new()],
        ];
        let mut out = Vec::new();

        write(&mut out, "  ", &rows);

        assert_eq!(
            String::from_utf8(out).expect("UTF-8"),
            "  a    bb  c\n  aaa  b\n"
        );
        assert_eq!(left("ab", 4), "ab  ");
        assert_eq!(right("ab", 4), "  ab");
    }
}
