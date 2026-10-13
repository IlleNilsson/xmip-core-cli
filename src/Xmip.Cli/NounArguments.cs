namespace Xmip.Cli;

/// <summary>
/// What the nouns a node publishes for an operator to act on share on the
/// line (<c>observe::Noun</c>): <c>xmip-cli event-subscriptions</c>
/// (ADR-0065, amendment 2026-09-29), <c>xmip-cli subscriptions</c>
/// (ADR-0013, amendment 2026-09-30), <c>xmip-cli dead-messages</c>
/// (ADR-0052, amendment 2026-10-01) and <c>xmip-cli journey</c>
/// (runtime-model.md section 13). The act — <c>--pause</c>, <c>--resume</c>,
/// <c>--remove</c>, <c>--replay</c>, <c>--retry</c> or <c>--dismiss</c>, one
/// at most — read
/// as the word the runtime names it by, which each noun then takes or
/// refuses; and the query words every noun shares with <c>audit</c> —
/// <c>--location</c>, <c>--sort</c>, <c>--order</c> — read as
/// <see cref="AuditArguments"/> reads them.
/// </summary>
public static class NounArguments
{
    /// <summary>Every act option, as typed.</summary>
    public static IReadOnlyList<string> Options { get; } =
        ["--pause", "--resume", "--remove", "--replay", "--retry", "--dismiss"];

    /// <summary>The query options every noun shares with <c>audit</c>.</summary>
    public static IReadOnlyList<string> Shared { get; } = ["--location", "--sort", "--order"];

    /// <summary>
    /// Take the option at <paramref name="index"/>. False when it is none of
    /// <see cref="Options"/>; true when it is, its word — pause, resume,
    /// remove, replay, retry or dismiss — in <paramref name="act"/>, with
    /// <paramref name="problem"/>
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
            problem = "--pause, --resume, --remove, --replay, --retry and --dismiss are one "
                + "act; name one.";
            return true;
        }

        act = named;
        return true;
    }

    /// <summary>
    /// The first of audit's options <paramref name="command"/> does not take,
    /// said as the line's problem; null when there is none. Every noun shares
    /// where it stands and its order with audit, a Journey where it stands and
    /// the page of its list; the rest of audit's words are audit's.
    /// </summary>
    public static string? Foreign(Command command, IEnumerable<string> auditOptions)
    {
        ArgumentNullException.ThrowIfNull(auditOptions);

        bool noun = command
            is Command.EventSubscriptions or Command.Subscriptions or Command.DeadMessages;

        string? foreign = command is Command.Journey
            ? auditOptions.FirstOrDefault(option => !JourneyArguments.Shared.Contains(option))
            : noun
                ? auditOptions.FirstOrDefault(option => !Shared.Contains(option))
                : command is Command.Audit ? null : auditOptions.FirstOrDefault();

        return foreign is null ? null : $"{foreign} only applies to 'audit'.";
    }

    /// <summary>
    /// The first of the nouns' options <paramref name="command"/> does not
    /// take, said as the line's problem; null when there is none.
    /// </summary>
    public static string? Stray(Command command, IEnumerable<string> nounOptions)
    {
        ArgumentNullException.ThrowIfNull(nounOptions);

        foreach (string option in nounOptions)
        {
            string? owner = EventSubscriptionArguments.Options.Contains(option)
                ? command is Command.EventSubscriptions ? null : "'event-subscriptions'"
                : SubscriptionArguments.Options.Contains(option)
                    ? command is Command.Subscriptions ? null : "'subscriptions'"
                    : DeadMessageArguments.Options.Contains(option)
                        ? command is Command.DeadMessages ? null : "'dead-messages'"
                        : command is Command.EventSubscriptions or Command.Subscriptions
                            or Command.DeadMessages or Command.Journey
                            ? null
                            : "'event-subscriptions', 'subscriptions', 'dead-messages' "
                                + "and 'journey'";

            if (owner is not null)
            {
                return $"{option} only applies to {owner}.";
            }
        }

        return null;
    }
}
