using System.Globalization;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// What <c>xmip-cli</c> records of itself through the audit capability
/// (ADR-0062): each command an operator runs as it begins, its end as
/// Finished, and a command that exits non-zero — refused, or answering that
/// the thing asked about is wrong — as a Failure with its exit code and what
/// it said on stderr. A line that cannot be obeyed is recorded as
/// <c>refused</c>. The record itself is the capability's, through
/// <see cref="ProgramAudit"/>; nothing here writes one.
/// </summary>
public static class CommandAudit
{
    /// <summary>The program's name on every record.</summary>
    public const string Program = "xmip-cli";

    /// <summary>The action a line that could not be obeyed is recorded as.</summary>
    public const string Refusal = "refused";

    /// <summary>
    /// The program's audit: into the directory <c>xmip.cli.toml</c> names as
    /// <c>AuditDirectory</c>, resolved from beside the executable, else where
    /// the capability decides — <c>XMIP_AUDIT_DIRECTORY</c>, else the
    /// operating system's log. The runtime library it records through is the
    /// one this invocation states (<see cref="SurfaceOpen.Runtime"/>).
    /// </summary>
    public static ProgramAudit Open(Invocation? invocation)
    {
        string beside = AppContext.BaseDirectory;

        SurfaceOpen.Runtime(invocation);

        return new ProgramAudit(
            Program,
            ProgramAudit.Stated(
                TomlDocument.Read(Path.Combine(beside, SurfaceOpen.ConfigurationFile)), beside));
    }

    /// <summary>The action a command is recorded as: its word on the line.</summary>
    public static string Action(Invocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        return invocation.Command.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// What the operator typed, as properties. No option of the line is a
    /// secret; a web host's address may carry a user and password, and the
    /// audit capability leaves those out of every record, so it is handed
    /// over as typed.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Properties(Invocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        Dictionary<string, string> said = new(StringComparer.Ordinal)
        {
            ["user"] = Environment.UserName,
        };

        if (invocation.Argument.Length > 0)
        {
            said["argument"] = invocation.Argument;
        }

        if (invocation.Json)
        {
            said["json"] = "yes";
        }

        if (invocation.Follow)
        {
            said["follow"] = "yes";
        }

        if (invocation.Runtime is { } runtime)
        {
            said["runtime"] = runtime;
        }

        if (invocation.Remote is { } remote)
        {
            said["remote"] = remote;
        }

        if (invocation.Snapshot is { } snapshot)
        {
            said["snapshot"] = snapshot;
        }

        return said;
    }

    /// <summary>Record that a command began.</summary>
    public static AuditOutcome Begun(ProgramAudit audit, Invocation invocation)
    {
        ArgumentNullException.ThrowIfNull(audit);

        return audit.Record(
            Action(invocation), AuditPhase.Begin, AuditSeverity.Information,
            properties: Properties(invocation));
    }

    /// <summary>
    /// Record how a command ended: Finished on exit 0, else a Failure with
    /// the exit code and <paramref name="complaint"/>, what it said on stderr.
    /// </summary>
    public static AuditOutcome Ended(
        ProgramAudit audit, Invocation invocation, int exit, string complaint)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(complaint);

        Dictionary<string, string> said = new(Properties(invocation), StringComparer.Ordinal)
        {
            ["exit"] = exit.ToString(CultureInfo.InvariantCulture),
        };

        return exit == 0
            ? audit.Record(
                Action(invocation), AuditPhase.Finished, AuditSeverity.Information,
                properties: said)
            : audit.Record(
                Action(invocation), AuditPhase.Failure, AuditSeverity.Error,
                complaint.Trim() is { Length: > 0 } words ? words : $"exit {exit}", said);
    }

    /// <summary>Record a line that could not be obeyed, with the sentence
    /// that said why and exit 2.</summary>
    public static AuditOutcome Refused(ProgramAudit audit, string problem)
    {
        ArgumentNullException.ThrowIfNull(audit);

        return audit.Record(
            Refusal, AuditPhase.Failure, AuditSeverity.Error, problem,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["user"] = Environment.UserName,
                ["exit"] = "2",
            });
    }
}
