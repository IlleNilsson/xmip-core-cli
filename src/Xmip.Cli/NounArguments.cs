namespace Xmip.Cli;

/// <summary>
/// What the nouns a node publishes for an operator to act on share on the
/// line (<c>observe::Noun</c>): <c>xmip-cli event-subscriptions</c>
/// (ADR-0065, amendment 2026-09-29), <c>xmip-cli subscriptions</c>
/// (ADR-0013, amendment 2026-09-30) and <c>xmip-cli dead-messages</c>
/// (ADR-0052, amendment 2026-10-01). The act — <c>--pause</c>,
/// <c>--resume</c>, <c>--remove</c> or <c>--replay</c>, one at most — read
/// as the word the runtime names it by, which each noun then takes or
/// refuses; and the query words every noun shares with <c>audit</c> —
/// <c>--location</c>, <c>--sort</c>, <c>--order</c> — read as
/// <see cref="AuditArguments"/> reads them.
/// </summary>
public static class NounArguments
{
    /// <summary>Every act option, as typed.</summary>
    public static IReadOnlyList<string> Options { get; } =
        ["--pause", "--resume", "--remove", "--replay"];

    /// <summary>The query options every noun shares with <c>audit</c>.</summary>
    public static IReadOnlyList<string> Shared { get; } = ["--location", "--sort", "--order"];

    /// <summary>
    /// Take the option at <paramref name="index"/>. False when it is none of
    /// <see cref="Options"/>; true when it is, its word — pause, resume,
    /// remove or replay — in <paramref name="act"/>, with <paramref name="problem"/>
    /// saying why the line cannot be obeyed where a second act is named.
    /// </summary>
    public static bool Take(
        IReadOnlyList<string> args, int index, ref string? act, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(args);

        string option = args[index];
        problem = null;

        if (!Options.Contains(option, StringComparer.Ordinal))
        {
            return false;
        }

        string named = option[2..];

        if (act is { } before && before != named)
        {
            problem = "--pause, --resume, --remove and --replay are one act; name one.";
            return true;
        }

        act = named;
        return true;
    }
}
