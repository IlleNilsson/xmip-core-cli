using System.Diagnostics;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// What <c>xmip</c> records of itself (ADR-0062), through the audit
/// capability in the runtime's library: a command, its end, a failure, a
/// line that could not be obeyed — and that the executable itself does so.
/// </summary>
public sealed class CommandAuditTests
{
    [Fact]
    public void AFailedCommandLandsAsAFailureRecordWithItsExitCode()
    {
        string directory = Scratch();
        ProgramAudit audit = new(CommandAudit.Program, directory);
        Invocation invocation = Line("validate", "missing.xmip");

        CommandAudit.Begun(audit, invocation);
        AuditOutcome outcome = CommandAudit.Ended(
            audit, invocation, 2, "No file at missing.xmip.\n");

        Assert.Equal(AuditKept.Persisted, outcome.Kept);
        string text = Records(directory);
        Assert.Contains("[[record]]", text, StringComparison.Ordinal);
        Assert.Contains("program = \"xmip\"", text, StringComparison.Ordinal);
        Assert.Contains("action = \"validate\"", text, StringComparison.Ordinal);
        Assert.Contains("phase = \"Begin\"", text, StringComparison.Ordinal);
        Assert.Contains("phase = \"Failure\"", text, StringComparison.Ordinal);
        Assert.Contains("message = \"No file at missing.xmip.\"", text, StringComparison.Ordinal);
        Assert.Contains("\"exit\" = \"2\"", text, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void ACommandThatExitsZeroIsFinished()
    {
        string directory = Scratch();
        ProgramAudit audit = new(CommandAudit.Program, directory);

        CommandAudit.Ended(audit, Line("abi"), 0, string.Empty);

        Assert.Contains("phase = \"Finished\"", Records(directory), StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void ALineThatCannotBeObeyedIsRecordedAsRefused()
    {
        string directory = Scratch();

        CommandAudit.Refused(new ProgramAudit(CommandAudit.Program, directory), "no such word");

        string text = Records(directory);
        Assert.Contains("action = \"refused\"", text, StringComparison.Ordinal);
        Assert.Contains("phase = \"Failure\"", text, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void TheAuditRecordsThroughTheLibraryTheLineStates()
    {
        // --runtime reaches the audit: the library the line names is the one
        // the audit is handed, not one the rule would find without it.
        string stated = Path.Combine(Path.GetTempPath(), "stated-runtime.dll");

        ProgramAudit audit = CommandAudit.Open(Line("abi", "--runtime", stated));

        Assert.Equal(Path.GetFullPath(stated), audit.Library);
    }

    [Fact]
    public async Task TheExecutableRecordsALineItRefuses()
    {
        string directory = Scratch();
        ProcessStartInfo start = new("dotnet")
        {
            ArgumentList = { Path.Combine(AppContext.BaseDirectory, "xmip.dll"), "nonsense" },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            Environment = { ["XMIP_AUDIT_DIRECTORY"] = directory },
        };

        // Both streams read together, to the end: reading one while the other
        // fills its pipe is how a child blocks forever.
        ProcessTextOutput run = await Process.RunAndCaptureTextAsync(
            start, TestContext.Current.CancellationToken);

        Assert.Equal(2, run.ExitStatus.ExitCode);
        Assert.Contains(
            "'nonsense' is not an xmip command", run.StandardError, StringComparison.Ordinal);
        string text = Records(directory);
        Assert.Contains("program = \"xmip\"", text, StringComparison.Ordinal);
        Assert.Contains("action = \"refused\"", text, StringComparison.Ordinal);
        Assert.Contains("phase = \"Failure\"", text, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    private static string Scratch()
    {
        return Path.Combine(Path.GetTempPath(), $"xmip-audit-{Guid.NewGuid():n}");
    }

    private static string Records(string directory)
    {
        return File.ReadAllText(Path.Combine(directory, "audit.xmip"));
    }

    private static Invocation Line(params string[] words)
    {
        Invocation? parsed = Invocation.Parse(words, out string problem);
        Assert.True(parsed is not null, problem);

        return parsed;
    }
}
