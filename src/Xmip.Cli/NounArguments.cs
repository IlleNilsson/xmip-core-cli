namespace Xmip.Cli;

/// <summary>
/// What the nouns a node publishes for an operator to act on share on the
/// line (<c>observe::Noun</c>): <c>xmip event-subscriptions</c>
/// (ADR-0065, amendment 2026-09-29), <c>xmip subscriptions</c>
/// (ADR-0013, amendment 2026-09-30), <c>xmip dead-messages</c>
/// (ADR-0052, amendment 2026-10-01) and <c>xmip message</c>
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
        ["--pause", "--resume", "--remove", "--replay", "--retry", "--dismiss", "--set"];

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
            problem = "--pause, --resume, --remove, --replay, --retry, --dismiss and --set are "
                + "one act; name one.";
            return true;
        }

        act = named;
        return true;
    }

    /// <summary>
    /// The first of audit's options <paramref name="command"/> does not take,
    /// said as the line's problem; null when there is none. Every noun shares
    /// where it stands and its order with audit, a failed Message where it stands and
    /// the page of its list; the rest of audit's words are audit's.
    /// </summary>
    public static string? Foreign(Command command, IEnumerable<string> auditOptions)
    {
        ArgumentNullException.ThrowIfNull(auditOptions);

        bool noun = command
            is Command.EventSubscriptions or Command.Subscriptions or Command.DeadMessages;

        string? foreign = command is Command.Message or Command.Database or Command.Secret
            ? auditOptions.FirstOrDefault(option => !(command switch
            {
                Command.Message => MessageArguments.Shared,
                Command.Secret => SecretArguments.Shared,
                _ => DatabaseArguments.Shared,
            }).Contains(option))
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

        // certificate's own are CertificateArguments.Refusal's to judge.
        foreach (string option in nounOptions.Except(CertificateArguments.Options))
        {
            string? owner = EventSubscriptionArguments.Options.Contains(option)
                ? command is Command.EventSubscriptions ? null : "'event-subscriptions'"
                : SubscriptionArguments.Options.Contains(option)
                    ? command is Command.Subscriptions ? null : "'subscriptions'"
                    : DeadMessageArguments.Options.Contains(option)
                        ? command is Command.DeadMessages ? null : "'dead-messages'"
                        : SecretArguments.Options.Contains(option)
                            ? command is Command.Secret ? null : "'secret'"
                        : command is Command.EventSubscriptions or Command.Subscriptions
                            or Command.DeadMessages or Command.Message or Command.Secret
                            ? null
                            : "'event-subscriptions', 'subscriptions', 'dead-messages' "
                                + "and 'message'";

            if (owner is not null)
            {
                return $"{option} only applies to {owner}.";
            }
        }

        return null;
    }

    /// <summary>
    /// Why <paramref name="act"/> is no act of <paramref name="command"/>
    /// where it is one noun's alone — Replay a Message's, Retry and Dismiss a
    /// failed Message's, Set a secret's — or null.
    /// </summary>
    public static string? Misplaced(Command command, string? act)
    {
        return act switch
        {
            "replay" when command is not Command.DeadMessages =>
                "--replay only applies to 'dead-messages'.",
            "retry" or "dismiss" when command is not Command.Message =>
                $"--{act} only applies to 'message'.",
            "set" when command is not Command.Secret => "--set only applies to 'secret'.",
            _ => null,
        };
    }
}
