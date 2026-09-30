using System.Globalization;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// The option only <c>xmip-cli event-subscriptions</c> takes (ADR-0065,
/// amendment 2026-09-29): <c>--id</c>, the number that names an Event
/// subscription on its node; and the act the line named
/// (<see cref="NounArguments"/>) as an Event subscription takes it — pause,
/// resume or remove.
/// </summary>
public static class EventSubscriptionArguments
{
    /// <summary>Every option, as typed.</summary>
    public static IReadOnlyList<string> Options { get; } = ["--id"];

    /// <summary>
    /// Take the option at <paramref name="index"/>. False when it is none of
    /// <see cref="Options"/>; true when it is, with <paramref name="problem"/>
    /// saying why the line cannot be obeyed where <c>--id</c> is not a whole
    /// number.
    /// </summary>
    public static bool Take(
        IReadOnlyList<string> args, ref int index, ref ulong? id, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(args);

        problem = null;

        if (!Options.Contains(args[index], StringComparer.Ordinal))
        {
            return false;
        }

        if (index + 1 >= args.Count || !ulong.TryParse(
            args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out ulong number))
        {
            problem = "--id needs an Event subscription's number.";
            return true;
        }

        index++;
        id = number;
        return true;
    }

    /// <summary>The act <paramref name="word"/> names, every one of which an
    /// Event subscription takes; null for none.</summary>
    public static EventSubscriptionAct? Act(string? word)
    {
        return word is null
            ? null
            : Enum.GetValues<EventSubscriptionAct>()
                .Single(act => EventSubscriptionOperation.Word(act) == word);
    }
}
