using System.Globalization;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip report</c>: every Report in one list, each saying its kind —
/// quick, it presents data that already exists, or it takes longer, it
/// presents what a Quest probes first — and what to expect of it (ADR-0072,
/// amendment 2026-11-10). <c>xmip report &lt;name&gt;</c> shows one entry, a
/// quick one with the command that presents it; with
/// <c>--configuration</c>, a Report that presents what a Quest probes is
/// opened: its Quest probes the configuration, and the Report shows its tally
/// and a row per requirement, or with <c>--row</c> one in full. The list, the
/// words, each probe and every refusal are the runtime's
/// (<see cref="ReportCatalog"/>); this renders them. A probe reads and
/// changes nothing, and is audited as program <c>xmip</c> where this
/// executable's records go, which is where the list reads each Quest's last
/// probe back. A refusal exits 1.
/// </summary>
public static class ReportCommand
{
    // This surface's command for each reader of the header a quick Report
    // names: the one place a quick entry is opened.
    private static readonly Dictionary<string, string> Presenting =
        new(StringComparer.Ordinal)
        {
            [OperateAbi.AuditReadEntrypoint] = "xmip audit",
            [OperateAbi.DeadMessagesEntrypoint] = "xmip dead-messages",
            [OperateAbi.SubscriptionsEntrypoint] = "xmip subscriptions",
            [OperateAbi.PublicationReadEntrypoint] = "xmip list",
        };

    private static readonly string[] Headings = ["kind", "report", "expect", "covers"];

    /// <summary>Answer <paramref name="invocation"/> through the runtime
    /// library it states (<see cref="SurfaceOpen.Runtime"/>), with no node
    /// behind it. Exit 1 when the library cannot be loaded, is a remote
    /// build, or refuses.</summary>
    public static int Over(
        Invocation invocation, ProgramAudit audit, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(error);

        _ = SurfaceOpen.Runtime(invocation);

        try
        {
            return Run(
                RuntimeLibrary.Rules.Reports, invocation.Report ?? new ReportAsk(), audit,
                invocation.Json, output, error);
        }
        catch (InvalidOperationException unanswered)
        {
            return Refused(unanswered.Message, invocation.Json, error);
        }
    }

    /// <summary>Answer <paramref name="asked"/> with the runtime's section 25
    /// in <paramref name="reports"/>, each probe audited as
    /// <paramref name="audit"/> records.</summary>
    public static int Run(
        RuntimeReports reports,
        ReportAsk asked,
        ProgramAudit audit,
        bool json,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(asked);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            return asked.Probe is { } probe
                ? Probed(ReportCatalog.Open(reports, probe, audit), json, output)
                : asked.Name.Length == 0
                    ? Listed(ReportCatalog.List(reports, audit), json, output)
                    : Shown(ReportCatalog.Named(reports, audit, asked.Name), json, output);
        }
        catch (InvalidOperationException unanswered)
        {
            return Refused(unanswered.Message, json, error);
        }
    }

    // Every Report, the kinds' words first.
    private static int Listed(ReportListing listing, bool json, TextWriter output)
    {
        if (json)
        {
            output.WriteLine(listing.Json);
            return 0;
        }

        output.WriteLine($"{listing.Entries.Count} Report(s), each one of:");
        TextTable.Write(
            output, "  ", [.. listing.Kinds.Select(kind => new[] { kind.Name, kind.Meaning })]);
        output.WriteLine();
        TextTable.Write(
            output,
            "  ",
            [
                Headings,
                .. listing.Entries.Select(entry => new[]
                {
                    entry.Words.Name, entry.Name, entry.Expect ?? string.Empty, entry.Covers,
                }),
            ]);
        Unread(listing, output);
        return 0;
    }

    // Why each Quest's last probe could not be read, where it could not.
    private static void Unread(ReportListing listing, TextWriter output)
    {
        if (listing.Unread is { } unread)
        {
            output.WriteLine();
            output.WriteLine($"  {unread}");
        }
    }

    // One entry, as the runtime listed it alone: what it is, and how it is
    // opened.
    private static int Shown(ReportListing listing, bool json, TextWriter output)
    {
        if (json)
        {
            output.WriteLine(listing.Json);
            return 0;
        }

        ReportEntry entry = listing.Entries[0];
        output.WriteLine($"{entry.Name} — {entry.Words.Name}: {entry.Words.Meaning}");
        output.WriteLine($"  {entry.Covers}");

        if (entry.Expect is { } expect)
        {
            output.WriteLine($"  Expect: {expect}");
        }

        if (entry.ReadBy is { } reader)
        {
            output.WriteLine(Presenting.TryGetValue(reader, out string? command)
                ? $"  Presented by: {command}"
                : $"  Read by {reader}, which this command line does not present.");
        }

        if (entry.Parameter is { } parameter)
        {
            output.WriteLine(
                $"  {parameter.Name}: {string.Join(", ", parameter.Choices)}");
        }

        if (entry.Levels.Count > 0)
        {
            string choice = entry.Parameter is { } named
                ? $" --parameter <{named.Name}>"
                : string.Empty;

            output.WriteLine($"  Levels: {string.Join("; ", entry.Levels)}");
            output.WriteLine(
                $"  Probe it: xmip report \"{entry.Name}\" --configuration <path> "
                    + $"[--node <node>]{choice} [--row <{entry.Row}>]");
        }

        Unread(listing, output);
        return 0;
    }

    // A probe: the tally and a row per requirement, or one in full; what the
    // Quest did not answer is left out, never shown as nothing found.
    private static int Probed(ReportProbed probed, bool json, TextWriter output)
    {
        if (json)
        {
            output.WriteLine(probed.Json);
            return 0;
        }

        string title = probed.Title.Length > 0 ? probed.Title : probed.Report;
        output.WriteLine(
            $"{title} — node {probed.Node}, probed in "
                + $"{probed.TookMilliseconds.ToString(CultureInfo.InvariantCulture)} ms, "
                + "nothing changed");

        if (probed.Requirement is { } one)
        {
            output.WriteLine($"{one.Name} ({one.Citation}): {one.State}");
            output.WriteLine($"  {one.Says}");
            Said(output, "behind it", one.Behind);
            Said(output, "actions", one.Actions);
            return 0;
        }

        if (probed.Tally.Count > 0)
        {
            output.WriteLine($"  {string.Join(", ", probed.Tally)}");
        }

        if (probed.Rows.Count > 0)
        {
            output.WriteLine();
            TextTable.Write(
                output,
                "  ",
                [
                    ["state", probed.Row, "citation"],
                    .. probed.Rows.Select(row => new[] { row.State, row.Name, row.Citation }),
                ]);
        }

        return 0;
    }

    private static void Said<T>(TextWriter output, string heading, IReadOnlyList<T> said)
    {
        if (said.Count == 0)
        {
            return;
        }

        output.WriteLine($"  {heading}:");

        foreach (T each in said)
        {
            output.WriteLine($"    {each}");
        }
    }

    private static int Refused(string refused, bool json, TextWriter error)
    {
        error.WriteLine(json
            ? JsonText.Document(writer => writer.WriteString("refused", refused))
            : refused);
        return 1;
    }
}
