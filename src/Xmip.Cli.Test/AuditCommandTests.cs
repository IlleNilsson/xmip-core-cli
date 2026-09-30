using System.Text.Json;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip-cli audit</c> (ADR-0062, amendment 2026-09-29): the line becomes
/// the query in the capability's words, and what the capability read back
/// from a directory the test wrote through <see cref="ProgramAudit"/> is
/// rendered — or its refusal said, in its words.
/// </summary>
public sealed class AuditCommandTests
{
    [Fact]
    public void EveryOptionIsTheQuerysWordAndTheArgumentItsPattern()
    {
        Invocation? parsed = Invocation.Parse(
            [
                "audit", "xmip:///C1/*", "--location", "xmip:///C1", "--host", "H1",
                "--program", "xmip-cli", "--record", "r1", "--severity", "error",
                "--action", "pause", "--from", "2026-09-29", "--to", "2026-09-30T12:00",
                "--sort", "node", "--order", "ascending", "--offset", "5", "--limit", "7",
                "--json",
            ],
            out string problem);

        Assert.True(parsed is not null, problem);
        Assert.Equal(Command.Audit, parsed.Command);
        Assert.True(parsed.Json);
        Assert.Equal(
            new AuditQuery
            {
                Pattern = "xmip:///C1/*",
                Location = "xmip:///C1",
                Host = "H1",
                Program = "xmip-cli",
                Record = "r1",
                Severity = "error",
                Action = "pause",
                From = "2026-09-29",
                To = "2026-09-30T12:00",
                Sort = "node",
                Order = "ascending",
                Offset = 5,
                Limit = 7,
            },
            parsed.Audit);
    }

    [Fact]
    public void AuditWithNothingAskedIsTheEmptyQuery()
    {
        Invocation? parsed = Invocation.Parse(["audit"], out _);

        Assert.NotNull(parsed?.Audit);
        Assert.Equal(new AuditQuery(), parsed.Audit);
        Assert.Empty(parsed.Audit.Pairs());
    }

    [Theory]
    [InlineData(new[] { "health", "xmip:///", "--severity", "error" },
        "--severity only applies to 'audit'.")]
    [InlineData(new[] { "audit", "--limit", "many" }, "--limit needs a whole number, not 'many'.")]
    [InlineData(new[] { "audit", "--location" }, "--location needs a value.")]
    [InlineData(new[] { "health", "xmip:///", "--include-hidden" },
        "--include-hidden only applies to 'audit'.")]
    public void ALineTheAuditCannotObeyIsRefusedAsAnyLine(string[] line, string expected)
    {
        Assert.Null(Invocation.Parse(line, out string problem));
        Assert.Equal(expected, problem);
    }

    [Fact]
    public void AHiddenRunsRecordsAreLeftOutUntilIncludedAndThenMarkedTest()
    {
        // The owner, 2026-09-29, and ADR-0028, amendment 2026-09-30: what a
        // run that declared itself hidden recorded is read only when asked.
        Invocation? parsed = Invocation.Parse(["audit", "--include-hidden"], out string problem);
        Assert.True(parsed is not null, problem);
        Assert.True(parsed.Audit!.IncludeHidden);

        string directory = Written();
        File.AppendAllText(
            Path.Combine(directory, "audit.toml"),
            "[[record]]\naudit_id = \"h1\"\nat = \"2026-09-30T10:00:00.000000000Z\"\n"
            + "program = \"probe\"\nhost = \"edge-01\"\nprocess = \"7\"\n"
            + "location = \"xmip:///CT/node/one\"\nhidden = \"true\"\naction = \"start\"\n"
            + "phase = \"begin\"\nseverity = \"information\"\n\n");
        using StringWriter left = new();
        using StringWriter included = new();

        AuditCommand.Run(Audit(directory), new AuditQuery(), false, left, TextWriter.Null);
        AuditCommand.Run(
            Audit(directory), parsed.Audit, false, included, TextWriter.Null);

        Assert.StartsWith("2 of 2 records", left.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("xmip:///CT", left.ToString(), StringComparison.Ordinal);
        Assert.StartsWith("3 of 3 records", included.ToString(), StringComparison.Ordinal);
        Assert.Contains("one · test", included.ToString(), StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void WhatTheOperatorAskedIsRecordedInTheQuerysWords()
    {
        Invocation? parsed = Invocation.Parse(
            ["audit", "xmip:///C1/*", "--severity", "error", "--limit", "5"], out _);

        IReadOnlyDictionary<string, string> said = CommandAudit.Properties(parsed!);

        Assert.Equal("xmip:///C1/*", said["argument"]);
        Assert.Equal("error", said["severity"]);
        Assert.Equal("5", said["limit"]);
        Assert.False(said.ContainsKey("pattern"));
    }

    [Fact]
    public void AQueryTheCapabilityDoesNotTakeIsItsRefusalAndExitTwo()
    {
        string directory = Written();
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = AuditCommand.Run(
            Audit(directory), new AuditQuery { Severity = "loud" }, false, output, error);

        Assert.Equal(2, exit);
        Assert.StartsWith("REFUSED", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("(Parameter", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(output.ToString());
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void TheTextIsTheHeaderTheGroupsAndARowARecordNewestFirst()
    {
        string directory = Written();
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = AuditCommand.Run(Audit(directory), new AuditQuery(), false, output, error);

        Assert.Equal(0, exit);
        string[] lines = output.ToString().Split(Environment.NewLine);
        Assert.StartsWith("2 of 2 records in ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("audit.toml", lines[0], StringComparison.Ordinal);
        Assert.Contains(
            lines, line => line.TrimStart().StartsWith("host", StringComparison.Ordinal));
        int heading =
            Array.FindIndex(lines, line => line.StartsWith("at ", StringComparison.Ordinal));
        Assert.True(heading > 0, output.ToString());
        Assert.Contains("severity", lines[heading], StringComparison.Ordinal);
        Assert.Contains("stopped", lines[heading + 1], StringComparison.Ordinal);
        Assert.Contains("start", lines[heading + 2], StringComparison.Ordinal);
        Assert.Empty(error.ToString());
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void JsonIsTheReadWholeAndOneRecordPrintsEveryField()
    {
        string directory = Written();
        using StringWriter json = new();

        Assert.Equal(0, AuditCommand.Run(
            Audit(directory), new AuditQuery { Severity = "warning" }, true, json,
            TextWriter.Null));

        using JsonDocument document = JsonDocument.Parse(json.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal(2, root.GetProperty("read").GetInt32());
        Assert.Equal(1, root.GetProperty("matched").GetInt32());
        JsonElement record = root.GetProperty("records")[0];
        Assert.Equal("stop", record.GetProperty("action").GetString());
        Assert.Equal("yes", record.GetProperty("properties").GetProperty("tested").GetString());
        Assert.Contains("information", root.GetProperty("severities").EnumerateArray()
            .Select(word => word.GetString()));

        using StringWriter one = new();
        string id = record.GetProperty("audit_id").GetString()!;

        Assert.Equal(0, AuditCommand.Run(
            Audit(directory), new AuditQuery { Record = id }, false, one, TextWriter.Null));
        Assert.Contains($"audit_id    {id}", one.ToString(), StringComparison.Ordinal);
        Assert.Contains("message     stopped", one.ToString(), StringComparison.Ordinal);
        Assert.Contains("  tested  yes", one.ToString(), StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void ARecordThatIsNotThereIsSaidAndExitsOne()
    {
        string directory = Written();
        using StringWriter error = new();

        int exit = AuditCommand.Run(
            Audit(directory), new AuditQuery { Record = "no-such-record" }, false,
            TextWriter.Null, error);

        Assert.Equal(1, exit);
        Assert.StartsWith(
            "No record no-such-record in ", error.ToString(), StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void WithNoAuditDirectoryThereIsNothingToReadAndItIsSaid()
    {
        string? stated = Environment.GetEnvironmentVariable("XMIP_AUDIT_DIRECTORY");
        Environment.SetEnvironmentVariable("XMIP_AUDIT_DIRECTORY", null);
        using StringWriter error = new();

        try
        {
            int exit = AuditCommand.Run(
                new ProgramAudit(CommandAudit.Program), new AuditQuery(), false,
                TextWriter.Null, error);

            Assert.Equal(1, exit);
            Assert.Equal(English.NoAuditDirectory(), error.ToString().Trim());
            Assert.Contains("operating system's log", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XMIP_AUDIT_DIRECTORY", stated);
        }
    }

    // Two records, written as any program writes them: a start, and a stop
    // that warns.
    private static string Written()
    {
        string directory =
            Path.Combine(Path.GetTempPath(), $"xmip-cli-audit-read-{Guid.NewGuid():n}");
        ProgramAudit audit = Audit(directory);

        Assert.Equal(
            AuditKept.Persisted,
            audit.Record("start", AuditPhase.Begin, AuditSeverity.Information).Kept);
        audit.Record(
            "stop", AuditPhase.Finished, AuditSeverity.Warning, "stopped",
            new Dictionary<string, string> { ["tested"] = "yes" });

        return directory;
    }

    private static ProgramAudit Audit(string directory)
    {
        return new ProgramAudit(CommandAudit.Program, directory);
    }
}
