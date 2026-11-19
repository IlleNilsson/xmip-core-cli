//! A moment as the command line writes it, from Unix nanoseconds, to the
//! tenth of a microsecond its readers have always been given: in a
//! document, `2026-09-11T12:00:00+00:00` with a fraction only where there
//! is one, its trailing zeros dropped; on a line for a person, the
//! round-trip form `2026-09-11T12:00:00.0000000+00:00`.

use codec::civil::CivilTime;

/// Ticks of a hundred nanoseconds in a second.
const TICKS_A_SECOND: i64 = 10_000_000;

/// The whole seconds and the ticks past them of `unix_nanos`, cut toward
/// zero to the tick as it always was.
fn split(unix_nanos: i64) -> (CivilTime, i64) {
    let ticks = unix_nanos / 100;
    let seconds = ticks.div_euclid(TICKS_A_SECOND);

    (
        CivilTime::from_unix(seconds),
        ticks.rem_euclid(TICKS_A_SECOND),
    )
}

fn date_and_time(at: &CivilTime) -> String {
    format!(
        "{:04}-{:02}-{:02}T{:02}:{:02}:{:02}",
        at.year, at.month, at.day, at.hour, at.minute, at.second
    )
}

/// `unix_nanos` as a document writes a moment.
#[must_use]
pub fn document(unix_nanos: i64) -> String {
    let (at, ticks) = split(unix_nanos);
    let fraction = format!("{ticks:07}");
    let fraction = fraction.trim_end_matches('0');

    if fraction.is_empty() {
        format!("{}+00:00", date_and_time(&at))
    } else {
        format!("{}.{fraction}+00:00", date_and_time(&at))
    }
}

/// `unix_nanos` in the round-trip form a line for a person says.
#[must_use]
pub fn round_trip(unix_nanos: i64) -> String {
    let (at, ticks) = split(unix_nanos);

    format!("{}.{ticks:07}+00:00", date_and_time(&at))
}

/// `unix_nanos` as `yyyy-MM-dd HH:mm:ss`, to the second, in UTC.
#[must_use]
pub fn to_the_second(unix_nanos: i64) -> String {
    let (at, _) = split(unix_nanos);

    date_and_time(&at).replace('T', " ")
}

#[cfg(test)]
mod tests {
    use super::*;

    const SEEN: i64 = 1_789_128_000 * 1_000_000_000;

    #[test]
    fn a_moment_is_written_as_a_document_and_a_line_always_wrote_it() {
        assert_eq!(document(SEEN), "2026-09-11T12:00:00+00:00");
        assert_eq!(
            document(SEEN + 123_400),
            "2026-09-11T12:00:00.0001234+00:00"
        );
        assert_eq!(round_trip(SEEN), "2026-09-11T12:00:00.0000000+00:00");
        assert_eq!(round_trip(SEEN + 199), "2026-09-11T12:00:00.0000001+00:00");
        assert_eq!(to_the_second(SEEN), "2026-09-11 12:00:00");
    }
}
