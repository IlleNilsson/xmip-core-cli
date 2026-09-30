using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// The option only <c>xmip-cli subscriptions</c> takes (ADR-0013,
/// amendment 2026-09-30): <c>--name</c>, the configured name of a
/// Subscription on its node; and the act the line named
/// (<see cref="NounArguments"/>) as a Subscription takes it — pause or
/// resume, and remove refused in words: a Subscription is added and removed
/// in the TOML configuration of the Xmip Application that draws it.
/// </summary>
public static class SubscriptionArguments
{
    /// <summary>Every option, as typed.</summary>
    public static IReadOnlyList<string> Options { get; } = ["--name"];

    /// <summary>
    /// Take the option at <paramref name="index"/>. False when it is none of
    /// <see cref="Options"/>; true when it is, with <paramref name="problem"/>
    /// saying why the line cannot be obeyed where no name follows.
    /// </summary>
    public static bool Take(
        IReadOnlyList<string> args, ref int index, ref string? name, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(args);

        problem = null;

        if (!Options.Contains(args[index], StringComparer.Ordinal))
        {
            return false;
        }

        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            problem = "--name needs a Subscription's name.";
            return true;
        }

        name = args[++index];
        return true;
    }

    /// <summary>
    /// The act <paramref name="word"/> names as a Subscription takes it; null
    /// for none, and null with <paramref name="problem"/> REFUSED for remove,
    /// which no Subscription takes.
    /// </summary>
    public static SubscriptionAct? Act(string? word, out string? problem)
    {
        problem = null;

        if (word is null)
        {
            return null;
        }

        foreach (SubscriptionAct act in Enum.GetValues<SubscriptionAct>())
        {
            if (SubscriptionOperation.Word(act) == word)
            {
                return act;
            }
        }

        problem = $"REFUSED: --{word}: {SubscriptionOperation.Configured}";
        return null;
    }
}
