using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// The act <c>xmip message</c> takes (runtime-model.md section 13;
/// ADR-0013): the word the line named (<see cref="NounArguments"/>) as a
/// Message that failed takes it — retry or dismiss — and every other word
/// refused in words. The Message's Journey identifier is the command's argument and its node
/// the shared <c>--location</c>; with neither one nor an act the command
/// lists the Messages that failed there, paged by <c>--offset</c> and
/// <c>--limit</c>.
/// </summary>
public static class MessageArguments
{
    /// <summary>The audit options <c>message</c> shares: where it stands, and
    /// the page of a list.</summary>
    public static IReadOnlyList<string> Shared { get; } = ["--location", "--offset", "--limit"];

    /// <summary>
    /// The act <paramref name="word"/> names as a Message that failed takes
    /// it; null to list, where <paramref name="named"/> is false and no word
    /// was given; null with <paramref name="problem"/> saying why for a
    /// Message named with no act, an act with none named, or a word it does
    /// not take.
    /// </summary>
    public static MessageAct? Act(string? word, bool named, out string? problem)
    {
        problem = null;

        if (word is null)
        {
            if (named)
            {
                problem = "'message <id>' needs an act: --retry or --dismiss.";
            }

            return null;
        }

        foreach (MessageAct act in Enum.GetValues<MessageAct>())
        {
            if (MessageOperation.Word(act) == word)
            {
                if (!named)
                {
                    problem = $"REFUSED: to {word} a Message, name its Journey: "
                        + $"'message <id> --{word}'.";
                    return null;
                }

                return act;
            }
        }

        problem = $"REFUSED: --{word}: a Message that failed is retried or dismissed, "
            + "and --retry and --dismiss are its acts.";
        return null;
    }
}
