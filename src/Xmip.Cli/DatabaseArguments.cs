using System.Globalization;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// What <c>xmip-cli database &lt;server&gt;</c> takes (ADR-0015, amendment
/// 2026-10-20): <c>--from &lt;version&gt;</c>, the schema version the
/// databases hold, required; <c>--to &lt;version&gt;</c>, this build's unless
/// said; and <c>--out &lt;folder&gt;</c>, where the scripts are written. It
/// shares <c>--from</c> and <c>--to</c> with <c>audit</c>, where they are
/// times, and <c>--out</c> with <c>certificate</c>, so each is parsed once
/// and read here as a version.
/// </summary>
public static class DatabaseArguments
{
    /// <summary>The audit options <c>database</c> takes.</summary>
    public static IReadOnlyList<string> Shared { get; } = ["--from", "--to"];

    /// <summary>
    /// What <c>database</c> asks of <paramref name="server"/>, from the
    /// options <paramref name="audit"/> and <paramref name="certificate"/>
    /// took; null with <paramref name="refused"/> saying why where a version
    /// is not a whole number or <c>--from</c> is missing.
    /// </summary>
    public static DatabaseQuery? Query(
        string server, AuditQuery? audit, CertificateQuery? certificate, ref string? refused)
    {
        if (audit?.From is not { } from)
        {
            refused = "'database' needs --from <version>, the schema version the databases "
                + "hold.";
            return null;
        }

        uint? to = null;

        if (Version(from, "--from", ref refused) is not { } held
            || (audit.To is { } asked && (to = Version(asked, "--to", ref refused)) is null))
        {
            return null;
        }

        return new DatabaseQuery(server, held, to ?? 0, certificate?.Out);
    }

    private static uint? Version(string value, string option, ref string? refused)
    {
        if (uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint number)
            && number > 0)
        {
            return number;
        }

        refused = $"{option} needs a schema version, a whole number from 1, not '{value}'.";
        return null;
    }
}
