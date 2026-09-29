using System.Globalization;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// The options only <c>xmip-cli subscriptions</c> takes (ADR-0065,
/// amendment 2026-09-29): <c>--id</c>, the number that names a subscription
/// on its node, and the act — <c>--pause</c>, <c>--resume</c> or
/// <c>--remove</c>, one at most. Where the drill stands and the order are the
/// query words every listing shares — <c>--location</c>, <c>--sort</c>,
/// <c>--order</c> — read as <see cref="AuditArguments"/> reads them.
/// </summary>
public static class SubscriptionArguments
{
    /// <summary>Every option, as typed.</summary>
    public static IReadOnlyList<string> Options { get; } =
        ["--id", "--pause", "--resume", "--remove"];

    /// <summary>The query options <c>subscriptions</c> shares with
    /// <c>audit</c>.</summary>
    public static IReadOnlyList<string> Shared { get; } = ["--location", "--sort", "--order"];

    /// <summary>
    /// Take the option at <paramref name="index"/>. False when it is none of
    /// <see cref="Options"/>; true when it is, with <paramref name="problem"/>
    /// saying why the line cannot be obeyed where <c>--id</c> is not a whole
    /// number or a second act is named.
    /// </summary>
    public static bool Take(
        IReadOnlyList<string> args,
        ref int index,
        ref ulong? id,
        ref SubscriptionAct? act,
        out string? problem)
    {
        ArgumentNullException.ThrowIfNull(args);

        string option = args[index];
        problem = null;

        if (!Options.Contains(option, StringComparer.Ordinal))
        {
            return false;
        }

        if (option == "--id")
        {
            if (index + 1 >= args.Count || !ulong.TryParse(
                args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out ulong number))
            {
                problem = "--id needs a subscription's number.";
                return true;
            }

            index++;
            id = number;
            return true;
        }

        SubscriptionAct named = option switch
        {
            "--pause" => SubscriptionAct.Pause,
            "--resume" => SubscriptionAct.Resume,
            _ => SubscriptionAct.Remove,
        };

        if (act is { } before && before != named)
        {
            problem = "--pause, --resume and --remove are one act; name one.";
            return true;
        }

        act = named;
        return true;
    }
}
