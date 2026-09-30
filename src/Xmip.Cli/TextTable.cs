namespace Xmip.Cli;

/// <summary>
/// Rows for a person, in columns: every cell but the last padded to the
/// widest in its column, two spaces between. Written once for every command
/// that lists — <c>audit</c>, <c>event-subscriptions</c>,
/// <c>subscriptions</c>.
/// </summary>
public static class TextTable
{
    /// <summary>Write <paramref name="rows"/>, the headings first, each line
    /// begun with <paramref name="indent"/>.</summary>
    public static void Write(TextWriter output, string indent, IReadOnlyList<string[]> rows)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return;
        }

        int[] widths =
            [.. Enumerable.Range(0, rows[0].Length).Select(i => rows.Max(row => row[i].Length))];

        foreach (string[] row in rows)
        {
            output.WriteLine(indent + string.Join(
                "  ",
                row.Select((cell, i) => i == row.Length - 1 ? cell : cell.PadRight(widths[i])))
                .TrimEnd());
        }
    }
}
