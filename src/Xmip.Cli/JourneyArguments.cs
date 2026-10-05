using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// The act <c>xmip-cli journey</c> takes (runtime-model.md section 13;
/// ADR-0013): the word the line named (<see cref="NounArguments"/>) as a
/// Journey that failed takes it — retry or dismiss — and every other word
/// refused in words. The Journey is the command's argument and its node the
/// shared <c>--location</c>; with neither a Journey nor an act the command
/// lists the Journeys that failed there, paged by <c>--offset</c> and
/// <c>--limit</c>.
/// </summary>
public static class JourneyArguments
{
    /// <summary>The audit options <c>journey</c> shares: where it stands, and
    /// the page of a list.</summary>
    public static IReadOnlyList<string> Shared { get; } = ["--location", "--offset", "--limit"];

    /// <summary>
    /// The act <paramref name="word"/> names as a Journey that failed takes
    /// it; null to list, where <paramref name="named"/> is false and no word
    /// was given; null with <paramref name="problem"/> saying why for a
    /// Journey named with no act, an act with no Journey, or a word it does
    /// not take.
    /// </summary>
    public static JourneyAct? Act(string? word, bool named, out string? problem)
    {
        problem = null;

        if (word is null)
        {
            if (named)
            {
                problem = "'journey <id>' needs an act: --retry or --dismiss.";
            }

            return null;
        }

        foreach (JourneyAct act in Enum.GetValues<JourneyAct>())
        {
            if (JourneyOperation.Word(act) == word)
            {
                if (!named)
                {
                    problem = $"REFUSED: to {word} a Journey, name it: 'journey <id> --{word}'.";
                    return null;
                }

                return act;
            }
        }

        problem = $"REFUSED: --{word}: a Journey that failed is retried or dismissed, "
            + "and --retry and --dismiss are its acts.";
        return null;
    }
}
