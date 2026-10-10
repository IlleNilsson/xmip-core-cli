using System.Text.Json;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip-cli audit</c>: what the audit recorded, read back by the audit
/// capability's one reader (<see cref="ProgramAudit.Read"/>, ADR-0062,
/// amendment 2026-09-29) from where this executable's own records go — the
/// directory <see cref="CommandAudit.Open"/> states, else
/// <c>XMIP_AUDIT_DIRECTORY</c>. Which records match, in what order, and the
/// groups one step down the drill are the capability's; only the rendering is
/// here. A query the capability refuses is its REFUSED sentence and exit 2,
/// as any line that cannot be obeyed; no audit to read is exit 1.
/// </summary>
public static class AuditCommand
{
    // The columns of the table, as the capability names them.
    private static readonly string[] Headings =
        ["at", "node", "program", "action", "phase", "severity", "summary"];

    /// <summary>Read the audit <paramref name="audit"/> writes to, as
    /// <paramref name="query"/> asks, and render it.</summary>
    public static int Run(
        ProgramAudit audit, AuditQuery query, bool json, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        AuditRead read;

        try
        {
            read = audit.Read(query);
        }
        catch (ArgumentException refused)
        {
            string said = English.Refusal(refused);
            error.WriteLine(json
                ? JsonText.Document(writer => writer.WriteString("refused", said))
                : said);
            return 2;
        }
        catch (Exception unread) when (unread is IOException or InvalidOperationException)
        {
            error.WriteLine(unread.Message);
            return 1;
        }

        if (read.File.Length == 0)
        {
            error.WriteLine(English.NoAuditDirectory());
            return 1;
        }

        bool one = !string.IsNullOrWhiteSpace(query.Record);

        if (one && read.Records.Count == 0)
        {
            error.WriteLine($"No record {query.Record} in {read.File}.");
        }

        if (json)
        {
            output.WriteLine(Document(read));
        }
        else if (one && read.Records.Count > 0)
        {
            WriteRecord(output, read.Records[0]);
        }
        else if (!one)
        {
            WriteText(output, read);
        }

        if (!json && read.Chains.Count > 0)
        {
            // Each writer's chain, in words: OK or FAILED, and where.
            output.WriteLine();
            foreach (AuditChain chain in read.Chains)
            {
                output.WriteLine(chain.Said);
            }
        }

        // A chain that breaks is a failure, as a record not found is.
        bool broken = read.Chains.Any(chain => !chain.Whole);
        return (one && read.Records.Count == 0) || broken ? 1 : 0;
    }

    /// <summary>The read as one document: the file, the counts, the page,
    /// the groups, the records whole, and the words to choose from.</summary>
    public static string Document(AuditRead read)
    {
        ArgumentNullException.ThrowIfNull(read);

        return JsonText.Document(writer =>
        {
            writer.WriteString("file", read.File);
            writer.WriteNumber("read", read.Read);
            writer.WriteNumber("matched", read.Matched);
            writer.WriteNumber("offset", read.Offset);
            writer.WriteNumber("limit", read.Limit);
            writer.WriteStartArray("groups");

            foreach (AuditGroup group in read.Groups)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", group.Kind);
                writer.WriteString("who", group.Who);
                writer.WriteNumber("count", group.Count);
                writer.WriteNumber("warnings", group.Warnings);
                writer.WriteNumber("errors", group.Errors);
                writer.WriteString("latest", group.Latest);
                writer.WriteBoolean("hidden", group.Hidden);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("records");

            foreach (AuditEntry entry in read.Records)
            {
                WriteEntry(writer, entry);
            }

            writer.WriteEndArray();
            Words(writer, "actions", read.Actions);
            writer.WriteStartArray("chains");

            foreach (AuditChain chain in read.Chains)
            {
                writer.WriteStartObject();
                writer.WriteString("writer", chain.Writer);
                writer.WriteNumber("records", chain.Records);
                writer.WriteBoolean("whole", chain.Whole);
                writer.WriteString("said", chain.Said);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            Words(writer, "columns", read.Columns);
            Words(writer, "severities", read.Severities);
        });
    }

    // The header, the groups one step down, then one row per record.
    private static void WriteText(TextWriter output, AuditRead read)
    {
        output.WriteLine($"{read.Matched} of {read.Read} records in {read.File}");

        if (read.Records.Count < read.Matched)
        {
            int last = read.Offset + read.Records.Count;
            output.WriteLine(read.Records.Count == 0
                ? $"none from offset {read.Offset}"
                : $"showing {read.Offset + 1} to {last}; --offset {last} for the next page");
        }

        if (read.Groups.Count > 0)
        {
            output.WriteLine();
            int kind = read.Groups.Max(group => group.Kind.Length);
            int who = read.Groups.Max(group => group.Who.Length);

            foreach (AuditGroup group in read.Groups)
            {
                output.WriteLine(
                    $"  {group.Kind.PadRight(kind)}  {group.Who.PadRight(who)}  " +
                    $"{group.Count} records, {group.Warnings} warnings, " +
                    $"{group.Errors} errors, latest {English.ToTheSecond(group.Latest)}" +
                    English.Test(group.Hidden));
            }
        }

        if (read.Records.Count == 0)
        {
            return;
        }

        output.WriteLine();
        TextTable.Write(output, string.Empty, [Headings, .. read.Records.Select(Row)]);
    }

    // One record, every field and every property, one to a line.
    private static void WriteRecord(TextWriter output, AuditEntry entry)
    {
        (string Name, string? Value)[] fields =
        [
            ("audit_id", entry.AuditId), ("at", entry.At), ("program", entry.Program),
            ("host", entry.Host), ("process", entry.Process), ("location", entry.Location),
            ("node", entry.Node), ("cluster", entry.Cluster), ("action", entry.Action),
            ("phase", entry.Phase), ("severity", entry.Severity), ("message", entry.Message),
            ("hidden", entry.Hidden ? "true" : null),
        ];

        foreach ((string name, string? value) in fields)
        {
            output.WriteLine($"{name,-10}  {English.Value(value)}");
        }

        Table(output, "scope", entry.Scope);
        Table(output, "properties", entry.Properties);
    }

    private static void Table(
        TextWriter output, string heading, IReadOnlyDictionary<string, string> pairs)
    {
        if (pairs.Count == 0)
        {
            return;
        }

        output.WriteLine(heading);
        int width = pairs.Keys.Max(key => key.Length);

        foreach ((string key, string value) in
            pairs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"  {key.PadRight(width)}  {value}");
        }
    }

    private static string[] Row(AuditEntry entry)
    {
        return
        [
            English.ToTheSecond(entry.At), English.Value(entry.Node) + English.Test(entry.Hidden),
            entry.Program, entry.Action, entry.Phase, entry.Severity, entry.Summary,
        ];
    }

    private static void WriteEntry(Utf8JsonWriter writer, AuditEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("audit_id", entry.AuditId);
        writer.WriteString("at", entry.At);
        writer.WriteString("program", entry.Program);
        writer.WriteString("host", entry.Host);
        writer.WriteString("process", entry.Process);
        writer.WriteString("location", entry.Location);
        writer.WriteString("node", entry.Node);
        writer.WriteString("cluster", entry.Cluster);
        writer.WriteString("action", entry.Action);
        writer.WriteString("phase", entry.Phase);
        writer.WriteString("severity", entry.Severity);
        writer.WriteString("message", entry.Message);
        writer.WriteString("summary", entry.Summary);
        writer.WriteBoolean("hidden", entry.Hidden);
        Pairs(writer, "scope", entry.Scope);
        Pairs(writer, "properties", entry.Properties);
        writer.WriteEndObject();
    }

    private static void Pairs(
        Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, string> pairs)
    {
        writer.WriteStartObject(name);

        foreach ((string key, string value) in pairs)
        {
            writer.WriteString(key, value);
        }

        writer.WriteEndObject();
    }

    private static void Words(Utf8JsonWriter writer, string name, IReadOnlyList<string> words)
    {
        writer.WriteStartArray(name);

        foreach (string word in words)
        {
            writer.WriteStringValue(word);
        }

        writer.WriteEndArray();
    }
}
