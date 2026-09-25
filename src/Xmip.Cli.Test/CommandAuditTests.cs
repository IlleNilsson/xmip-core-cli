using System.Diagnostics;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// What <c>xmip-cli</c> records of itself (ADR-0062), through the audit
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
        Invocation invocation = Line("validate", "missing.toml");

        CommandAudit.Begun(audit, invocation);
        AuditOutcome outcome = CommandAudit.Ended(
            audit, invocation, 2, "No file at missing.toml.\n");

        Assert.Equal(AuditKept.Persisted, outcome.Kept);
        string text = Records(directory);
        Assert.Contains("[[record]]", text, StringComparison.Ordinal);
        Assert.Contains("program = \"xmip-cli\"", text, StringComparison.Ordinal);
        Assert.Contains("action = \"validate\"", text, StringComparison.Ordinal);
        Assert.Contains("phase = \"begin\"", text, StringComparison.Ordinal);
        Assert.Contains("phase = \"failure\"", text, StringComparison.Ordinal);
        Assert.Contains("message = \"No file at missing.toml.\"", text, StringComparison.Ordinal);
        Assert.Contains("\"exit\" = \"2\"", text, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void ACommandThatExitsZeroIsFinished()
    {
        string directory = Scratch();
        ProgramAudit audit = new(CommandAudit.Program, directory);

        CommandAudit.Ended(audit, Line("abi"), 0, string.Empty);

        Assert.Contains("phase = \"finished\"", Records(directory), StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void ALineThatCannotBeObeyedIsRecordedAsRefused()
    {
        string directory = Scratch();

        CommandAudit.Refused(new ProgramAudit(CommandAudit.Program, directory), "no such word");

        string text = Records(directory);
        Assert.Contains("action = \"refused\"", text, StringComparison.Ordinal);
        Assert.Contains("phase = \"failure\"", text, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void AWebHostsUserAndPasswordAreNeverRecorded()
    {
        // Handed over as typed; the audit capability leaves the secret out of
        // every record, for every program, once.
        string directory = Scratch();
        ProgramAudit audit = new(CommandAudit.Program, directory);

        CommandAudit.Begun(
            audit, Line("measure", "--remote", "http://operator:secret@elsewhere:5087"));

        string text = Records(directory);
        Assert.Contains("\"remote\" = \"http://elsewhere:5087\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void TheExecutableRecordsALineItRefuses()
    {
        string directory = Scratch();
        ProcessStartInfo start = new("dotnet")
        {
            ArgumentList = { Path.Combine(AppContext.BaseDirectory, "xmip-cli.dll"), "nonsense" },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            Environment = { ["XMIP_AUDIT_DIRECTORY"] = directory },
        };

        using Process run = Process.Start(start)!;
        string complaint = run.StandardError.ReadToEnd();
        run.WaitForExit();

        Assert.Equal(2, run.ExitCode);
        Assert.Contains(
            "'nonsense' is not an xmip-cli command", complaint, StringComparison.Ordinal);
        string text = Records(directory);
        Assert.Contains("program = \"xmip-cli\"", text, StringComparison.Ordinal);
        Assert.Contains("action = \"refused\"", text, StringComparison.Ordinal);
        Assert.Contains("phase = \"failure\"", text, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    private static string Scratch()
    {
        return Path.Combine(Path.GetTempPath(), $"xmip-cli-audit-{Guid.NewGuid():n}");
    }

    private static string Records(string directory)
    {
        return File.ReadAllText(Path.Combine(directory, "audit.toml"));
    }

    private static Invocation Line(params string[] words)
    {
        Invocation? parsed = Invocation.Parse(words, out string problem);
        Assert.True(parsed is not null, problem);

        return parsed;
    }
}
