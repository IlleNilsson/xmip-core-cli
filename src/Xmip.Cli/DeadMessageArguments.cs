using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// The option only <c>xmip-cli dead-messages</c> takes (ADR-0052, amendment
/// 2026-10-01): <c>--message</c>, the identifier of a Message in its node's
/// Dead Message Queue; and the act the line named
/// (<see cref="NounArguments"/>) as such a Message takes it — replay, its one
/// act, and every other word refused in words.
/// </summary>
public static class DeadMessageArguments
{
    /// <summary>Every option, as typed.</summary>
    public static IReadOnlyList<string> Options { get; } = ["--message"];

    /// <summary>
    /// Take the option at <paramref name="index"/>. False when it is none of
    /// <see cref="Options"/>; true when it is, with <paramref name="problem"/>
    /// saying why the line cannot be obeyed where no identifier follows.
    /// </summary>
    public static bool Take(
        IReadOnlyList<string> args, ref int index, ref string? message, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(args);

        problem = null;

        if (!Options.Contains(args[index], StringComparer.Ordinal))
        {
            return false;
        }

        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            problem = "--message needs a Message's identifier.";
            return true;
        }

        message = args[++index];
        return true;
    }

    /// <summary>
    /// The act <paramref name="word"/> names as a Message in a Dead Message
    /// Queue takes it; null for none, and null with <paramref name="problem"/>
    /// REFUSED for a word it does not take.
    /// </summary>
    public static DeadMessageAct? Act(string? word, out string? problem)
    {
        problem = null;

        if (word is null)
        {
            return null;
        }

        foreach (DeadMessageAct act in Enum.GetValues<DeadMessageAct>())
        {
            if (DeadMessageOperation.Word(act) == word)
            {
                return act;
            }
        }

        problem = $"REFUSED: --{word}: a Message in a Dead Message Queue is replayed, "
            + "and --replay is its one act.";
        return null;
    }
}
