using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// The act <c>xmip-cli journey</c> takes (runtime-model.md section 13;
/// ADR-0013): the word the line named (<see cref="NounArguments"/>) as a
/// Journey that failed takes it — retry or dismiss, one of them required —
/// and every other word refused in words. The Journey is the command's
/// argument and its node the shared <c>--location</c>.
/// </summary>
public static class JourneyArguments
{
    /// <summary>
    /// The act <paramref name="word"/> names as a Journey that failed takes
    /// it; null with <paramref name="problem"/> saying why for none, or for a
    /// word it does not take.
    /// </summary>
    public static JourneyAct? Act(string? word, out string? problem)
    {
        problem = null;

        foreach (JourneyAct act in Enum.GetValues<JourneyAct>())
        {
            if (JourneyOperation.Word(act) == word)
            {
                return act;
            }
        }

        problem = word is null
            ? "'journey' needs an act: --retry or --dismiss."
            : $"REFUSED: --{word}: a Journey that failed is retried or dismissed, "
                + "and --retry and --dismiss are its acts.";
        return null;
    }
}
