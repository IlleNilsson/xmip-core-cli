using System.Globalization;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// The options of <c>xmip-cli audit</c>: one per word of an audit query
/// (<see cref="AuditQuery"/>, ADR-0062, amendment 2026-09-29), named as the
/// query names it — <c>--location</c> is <c>location</c>. Only the line is
/// read here; whether a severity, a time or a column is one the capability
/// takes is the capability's to say. <c>--include-hidden</c> takes no value:
/// it reads the records of a run that declared itself hidden too (ADR-0028,
/// amendment 2026-09-30), <c>hidden=include</c> in the query's words; nor
/// does <c>--verify</c>: it walks the audit chain of each writer of the
/// records matched and says where it breaks (ADR-0070 clause 5),
/// <c>verify=yes</c>.
/// </summary>
public static class AuditArguments
{
    /// <summary>Every option, as typed.</summary>
    public static IReadOnlyList<string> Options { get; } =
    [
        "--location", "--host", "--program", "--record", "--severity", "--action",
        "--from", "--to", "--sort", "--order", "--offset", "--limit",
    ];

    /// <summary>The flag that reads what a hidden run recorded too.</summary>
    public const string IncludeHidden = "--include-hidden";

    /// <summary>The flag that walks each writer's audit chain.</summary>
    public const string Verify = "--verify";

    /// <summary>
    /// Take the option at <paramref name="index"/> into
    /// <paramref name="query"/>, with the value after it. False when it is no
    /// audit option; true when it is, with <paramref name="problem"/> saying
    /// why the line cannot be obeyed where the value is missing or is not a
    /// whole number where one is needed.
    /// </summary>
    public static bool Take(
        IReadOnlyList<string> args, ref int index, ref AuditQuery? query, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(args);

        string option = args[index];
        problem = null;

        if (string.Equals(option, IncludeHidden, StringComparison.Ordinal))
        {
            query = (query ?? new AuditQuery()) with { IncludeHidden = true };
            return true;
        }

        if (string.Equals(option, Verify, StringComparison.Ordinal))
        {
            query = (query ?? new AuditQuery()) with { Verify = true };
            return true;
        }

        if (!Options.Contains(option, StringComparer.Ordinal))
        {
            return false;
        }

        string word = option[2..];
        bool counted = word is "offset" or "limit";

        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            problem = counted ? $"{option} needs a whole number." : $"{option} needs a value.";
            return true;
        }

        string value = args[++index];
        AuditQuery asked = query ?? new AuditQuery();
        int number = 0;

        if (counted && !int.TryParse(
            value, NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            problem = $"{option} needs a whole number, not '{value}'.";
            return true;
        }

        query = word switch
        {
            "location" => asked with { Location = value },
            "host" => asked with { Host = value },
            "program" => asked with { Program = value },
            "record" => asked with { Record = value },
            "severity" => asked with { Severity = value },
            "action" => asked with { Action = value },
            "from" => asked with { From = value },
            "to" => asked with { To = value },
            "sort" => asked with { Sort = value },
            "order" => asked with { Order = value },
            "offset" => asked with { Offset = number },
            _ => asked with { Limit = number },
        };

        return true;
    }
}
