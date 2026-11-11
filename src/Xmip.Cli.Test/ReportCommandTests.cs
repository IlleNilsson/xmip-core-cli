using System.Text.Json;
using System.Text.RegularExpressions;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip report</c> (ADR-0072, amendment 2026-11-10): the line read — the
/// Report's name, <c>--configuration</c>, <c>--parameter</c> and
/// <c>--row</c> its own, <c>--node</c> shared with <c>certificate</c> — and
/// every Report listed, one shown and one probed through the runtime this
/// estate built. Which Reports there are and what a probe finds are
/// <c>xmip-core-report</c>'s and tested there; an entry is found here by what
/// it holds, never by its name.
/// </summary>
public sealed class ReportCommandTests : IDisposable
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    private readonly string _directory =
        Directory.CreateTempSubdirectory("xmip-cli-report-").FullName;

    private ProgramAudit Audit => new(CommandAudit.Program, _directory);

    private static RuntimeReports Reports => RuntimeLibrary.Rules.Reports;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void TheLineNamesTheReportAndWhatItsQuestProbes()
    {
        Invocation all = Parse("report");
        Assert.Equal(Command.Report, all.Command);
        Assert.Equal(new ReportAsk(), all.Report);
        Assert.Null(all.Report?.Probe);

        string node = Cluster.Nodes[0];
        Invocation probing = Parse(
            "report", "q", "--configuration", "cluster.xmip", "--node", node,
            "--parameter", "p", "--row", "r", "--json");
        Assert.Equal(new ReportAsk("q", "cluster.xmip", "p", "r", node), probing.Report);
        Assert.Equal(
            new ReportProbe("q", "cluster.xmip") { Parameter = "p", Row = "r", Node = node },
            probing.Report?.Probe);
        Assert.Null(probing.Certificate);
    }

    [Fact]
    public void ALineThatCannotBeObeyedIsRefusedInWords()
    {
        Assert.Contains("needs the Report to open", Refused("report", "--configuration", "c"));
        Assert.Contains("--configuration <path>", Refused("report", "Q", "--parameter", "GDPR"));
        Assert.Contains(
            "--configuration <path>", Refused("report", "Q", "--node", Cluster.Nodes[0]));
        Assert.Equal("--row only applies to 'report'.", Refused("health", "x", "--row", "r"));
        Assert.Equal(
            "--configuration only applies to 'report'.",
            Refused("certificate", "--configuration", "c", "--operator", "a@example.org"));
        Assert.Contains("only applies to 'event-subscriptions'", Refused("report", "--pause"));
        Assert.Equal("--severity only applies to 'audit'.", Refused("report", "--severity", "x"));
        Assert.Contains("at most one argument", Refused("report", "a", "b"));
    }

    [Fact]
    public void TheListSaysEveryEntryWithItsKindsName()
    {
        (int exit, string said, _) = Run(new ReportAsk(), json: false);
        ReportListing listing = ReportCatalog.List(Reports, Audit);

        Assert.Equal(0, exit);
        Assert.All(listing.Kinds, kind =>
            Assert.Contains($"{kind.Name}  ", said, StringComparison.Ordinal));
        Assert.All(listing.Entries, entry => Assert.Matches(
            $"(?m)^  {Regex.Escape(entry.Words.Name)} +{Regex.Escape(entry.Name)}", said));

        (exit, said, _) = Run(new ReportAsk(), json: true);
        using JsonDocument document = JsonDocument.Parse(said);
        Assert.Equal(0, exit);
        Assert.Equal(
            listing.Entries.Count, document.RootElement.GetProperty("reports").GetArrayLength());
    }

    [Fact]
    public void AQuickEntryIsShownWithTheCommandThatPresentsIt()
    {
        ReportEntry audit = ReportCatalog.List(Reports, Audit).Entries
            .First(entry => entry.ReadBy == OperateAbi.AuditReadEntrypoint);

        (int exit, string said, _) = Run(new ReportAsk(audit.Name), json: false);

        Assert.Equal(0, exit);
        Assert.Contains("Presented by: xmip audit", said, StringComparison.Ordinal);
        Assert.Contains(audit.Covers, said, StringComparison.Ordinal);

        // As JSON, the runtime's own answer for the one entry.
        (exit, said, _) = Run(new ReportAsk(audit.Name), json: true);
        using JsonDocument one = JsonDocument.Parse(said);
        Assert.Equal(0, exit);
        JsonElement entry = Assert.Single(one.RootElement.GetProperty("reports").EnumerateArray());
        Assert.Equal(audit.Name, entry.GetProperty("name").GetString());
    }

    [Fact]
    public void AProbeSaysTheTallyAndARowPerRequirementOrOneInFull()
    {
        ReportEntry quest = ReportCatalog.List(Reports, Audit).Entries
            .First(entry => entry is { Kind: ReportKind.Quest, Available: true });
        ReportAsk asked = new(
            quest.Name, Cluster.Path, quest.Parameter?.Choices[0], null,
            Cluster.WithRole("storage"));

        (int exit, string said, _) = Run(asked, json: true);
        Assert.Equal(0, exit);
        ReportProbed probed = ReportCatalog.Probed(said);
        string row = probed.Rows[0].Name;

        // Every word the runtime's: each state's count, and what a row is
        // called as the heading of its column.
        (exit, said, _) = Run(asked, json: false);
        Assert.Equal(0, exit);
        Assert.Contains($"  {string.Join(", ", probed.Tally)}", said, StringComparison.Ordinal);
        Assert.Matches($"(?m)^  state +{Regex.Escape(probed.Row)} +citation", said);
        Assert.Contains(row, said, StringComparison.Ordinal);
        Assert.Contains("nothing changed", said, StringComparison.Ordinal);

        (exit, said, _) = Run(asked with { Row = row }, json: false);
        Assert.Equal(0, exit);
        Assert.Contains($"{row} (", said, StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusalExitsOneInTheRuntimesWordsAndAsJson()
    {
        ReportEntry quick = ReportCatalog.List(Reports, Audit).Entries
            .First(entry => entry.ReadBy is not null);

        // A cluster's file and no node: refused for the Report, before the
        // file is read.
        ReportAsk opening = new(quick.Name, Cluster.Path);

        (int exit, _, string complaint) = Run(opening, json: false);
        Assert.Equal(1, exit);
        Assert.StartsWith("REFUSED:", complaint, StringComparison.Ordinal);
        Assert.Contains(quick.ReadBy!, complaint, StringComparison.Ordinal);

        (exit, _, complaint) = Run(opening, json: true);
        using JsonDocument refused = JsonDocument.Parse(complaint);
        Assert.Equal(1, exit);
        Assert.StartsWith(
            "REFUSED:", refused.RootElement.GetProperty("refused").GetString(),
            StringComparison.Ordinal);

        (exit, _, complaint) = Run(new ReportAsk("nowhere"), json: false);
        Assert.Equal(1, exit);
        Assert.Contains(quick.Name, complaint, StringComparison.Ordinal);
    }

    private (int Exit, string Said, string Complaint) Run(ReportAsk asked, bool json)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = ReportCommand.Run(Reports, asked, Audit, json, output, error);

        return (exit, output.ToString(), error.ToString().Trim());
    }

    private static Invocation Parse(params string[] line)
    {
        return Invocation.Parse(line, out string problem)
            ?? throw new InvalidOperationException(problem);
    }

    private static string Refused(params string[] line)
    {
        Assert.Null(Invocation.Parse(line, out string problem));
        return problem;
    }
}
