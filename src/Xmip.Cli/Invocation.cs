using System.Globalization;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// One command line, parsed: the command, its one argument, and its
/// options. Parsing knows nothing about the runtime or the console, which is
/// what lets it be tested with a string array and nothing else.
/// </summary>
/// <param name="Command">The command named first on the line.</param>
/// <param name="Argument">The command's argument — a status code, a library
/// path, a scope, a configuration path — or empty for the ones without.</param>
/// <param name="Json">Emit one JSON document instead of text for a person.
/// ADR-0014 clause 10.</param>
/// <param name="Follow">Emit JSON Lines of health or figures as they change,
/// until interrupted. Implies <see cref="Json"/>.</param>
/// <param name="Runtime">An explicit runtime library, overriding discovery.
/// Null when the discovery rule decides.</param>
/// <param name="Remote">A web host on another machine to follow instead of a
/// runtime here (ADR-0052, amendment 2026-09-15). Null when the surface is
/// local.</param>
/// <param name="Snapshot">One published snapshot to read, overriding the
/// document. One and never several: a command answers at one scope, and a
/// rollup or a sum over two clusters would be a figure at a scope that is in
/// neither tree (ADR-0052, amendment 2026-09-20). Null when the document
/// decides.</param>
/// <param name="Who">Who is pausing, as the runtime records it; null for the
/// user this process runs as (<see cref="ScopeOperation.Who"/>).</param>
/// <param name="Audit">What <c>audit</c> asks of the audit, its argument the
/// pattern (<see cref="AuditArguments"/>); null for every other command.</param>
/// <param name="EventSubscriptions">What <c>event-subscriptions</c> asks,
/// its argument the pattern (<see cref="EventSubscriptionArguments"/>); null
/// for every other command.</param>
/// <param name="EventAct">The act <c>event-subscriptions</c> takes on the one
/// Event subscription it names; null to list.</param>
/// <param name="Subscriptions">What <c>subscriptions</c> asks, its argument
/// the pattern (<see cref="SubscriptionArguments"/>); null for every other
/// command.</param>
/// <param name="Act">The act <c>subscriptions</c> takes on the one
/// Subscription it names; null to list.</param>
/// <param name="DeadMessages">What <c>dead-messages</c> asks, its argument
/// the pattern (<see cref="DeadMessageArguments"/>); null for every other
/// command.</param>
/// <param name="Replay">The act <c>dead-messages</c> takes on the one Message
/// it names; null to list or open.</param>
/// <param name="JourneyAct">The act <c>journey</c> takes on the Journey its
/// argument names (<see cref="JourneyArguments"/>); null for every other
/// command.</param>
/// <param name="Location">Where <c>journey</c>'s Journey is sent: its node, or
/// the Send Port's scope beneath it; null for every other command.</param>
public sealed record Invocation(
    Command Command,
    string Argument,
    bool Json,
    bool Follow,
    string? Runtime,
    string? Remote,
    string? Snapshot = null,
    string? Who = null,
    AuditQuery? Audit = null,
    EventSubscriptionQuery? EventSubscriptions = null,
    EventSubscriptionAct? EventAct = null,
    SubscriptionQuery? Subscriptions = null,
    SubscriptionAct? Act = null,
    DeadMessageQuery? DeadMessages = null,
    DeadMessageAct? Replay = null,
    JourneyAct? JourneyAct = null,
    string? Location = null)
{
    /// <summary>What this line states about the surface to read, for the one
    /// precedence every surface shares (<see cref="SurfaceChoice.Stated"/>).</summary>
    public SurfaceLine Line => new(Remote, Snapshot, Runtime);

    // Each command, and how many arguments it takes: none, one, or one at most.
    private static readonly Dictionary<string, (Command Command, int Minimum, int Maximum)>
        Known = new(StringComparer.Ordinal)
        {
            ["help"] = (Command.Help, 0, 0),
            ["abi"] = (Command.Abi, 0, 0),
            ["status"] = (Command.Status, 1, 1),
            ["probe"] = (Command.Probe, 1, 1),
            ["health"] = (Command.Health, 1, 1),
            ["measure"] = (Command.Measure, 0, 1),
            ["list"] = (Command.List, 0, 1),
            ["show"] = (Command.Show, 1, 1),
            ["pause"] = (Command.Pause, 1, 1),
            ["resume"] = (Command.Resume, 1, 1),
            ["validate"] = (Command.Validate, 1, 1),
            ["audit"] = (Command.Audit, 0, 1),
            ["event-subscriptions"] = (Command.EventSubscriptions, 0, 1),
            ["subscriptions"] = (Command.Subscriptions, 0, 1),
            ["dead-messages"] = (Command.DeadMessages, 0, 1),
            ["journey"] = (Command.Journey, 1, 1),
        };

    /// <summary>
    /// Parse a command line. Options may sit anywhere; the first bare word is
    /// the command and the second its argument. Null with a problem in
    /// <paramref name="problem"/> when the line cannot be obeyed — the caller
    /// prints it and exits 2.
    /// </summary>
    public static Invocation? Parse(IReadOnlyList<string> args, out string problem)
    {
        List<string> words = [];
        bool json = false;
        bool follow = false;
        string? runtime = null;
        string? remote = null;
        string? snapshot = null;
        string? who = null;
        AuditQuery? audit = null;
        List<string> auditOptions = [];
        ulong? id = null;
        string? name = null;
        string? message = null;
        string? act = null;
        List<string> nounOptions = [];

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];

            if (AuditArguments.Take(args, ref i, ref audit, out string? unobeyed))
            {
                if (unobeyed is not null)
                {
                    problem = unobeyed;
                    return null;
                }

                auditOptions.Add(arg);
                continue;
            }

            if (EventSubscriptionArguments.Take(args, ref i, ref id, out unobeyed)
                || SubscriptionArguments.Take(args, ref i, ref name, out unobeyed)
                || DeadMessageArguments.Take(args, ref i, ref message, out unobeyed)
                || NounArguments.Take(args, i, ref act, out unobeyed))
            {
                if (unobeyed is not null)
                {
                    problem = unobeyed;
                    return null;
                }

                nounOptions.Add(arg);
                continue;
            }

            switch (arg)
            {
                case "--json":
                    json = true;
                    break;
                case "--follow":
                    follow = true;
                    break;
                case "--runtime":
                    if (i + 1 >= args.Count)
                    {
                        problem = "--runtime needs a path.";
                        return null;
                    }

                    runtime = args[++i];
                    break;
                case "--remote":
                    if (RemoteOperator.Refusal(i + 1 < args.Count ? args[i + 1] : null)
                        is { } notAWebHost)
                    {
                        problem = notAWebHost;
                        return null;
                    }

                    remote = args[++i];
                    break;
                case "--who":
                    if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        problem = "--who needs a name.";
                        return null;
                    }

                    who = args[++i];
                    break;
                case "--snapshot":
                    if (i + 1 >= args.Count)
                    {
                        problem = "--snapshot needs a path.";
                        return null;
                    }

                    snapshot = args[++i];
                    break;
                case "--help" or "-h":
                    words.Insert(0, "help");
                    break;
                default:
                    // A status code is zero or negative, so '-22' is a word,
                    // not an option.
                    if (arg.StartsWith('-') && !int.TryParse(
                        arg, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                    {
                        problem = $"'{arg}' is not an xmip-cli option. Try 'xmip-cli help'.";
                        return null;
                    }

                    words.Add(arg);
                    break;
            }
        }

        if (words.Count == 0)
        {
            problem = string.Empty;

            return new Invocation(
                Command.Help, string.Empty, json, follow, runtime, remote, snapshot, who);
        }

        if (!Known.TryGetValue(words[0], out (Command Command, int Minimum, int Maximum) known))
        {
            problem = $"'{words[0]}' is not an xmip-cli command. Try 'xmip-cli help'.";
            return null;
        }

        int arguments = words.Count - 1;

        if (arguments < known.Minimum || arguments > known.Maximum)
        {
            problem = known.Minimum == 0 && known.Maximum == 0
                ? $"'{words[0]}' takes no argument. Try 'xmip-cli help'."
                : known.Minimum == 0
                    ? $"'{words[0]}' takes at most one argument. Try 'xmip-cli help'."
                    : $"'{words[0]}' takes exactly one argument. Try 'xmip-cli help'.";
            return null;
        }

        if (follow && known.Command is not (Command.Health or Command.Measure))
        {
            problem = "--follow only applies to 'health' or 'measure'.";
            return null;
        }

        bool noun = known.Command
            is Command.EventSubscriptions or Command.Subscriptions or Command.DeadMessages
            or Command.Journey;

        if (who is not null && known.Command is not Command.Pause && !(noun && act is not null))
        {
            problem = "--who only applies to 'pause' and to an act of 'event-subscriptions', "
                + "'subscriptions', 'dead-messages' or 'journey'.";
            return null;
        }

        // Replay is a Message's act alone; the other nouns never see the word.
        if (act == "replay" && known.Command is not Command.DeadMessages)
        {
            problem = "--replay only applies to 'dead-messages'.";
            return null;
        }

        // Retry and Dismiss are a Journey's acts alone.
        if (act is "retry" or "dismiss" && known.Command is not Command.Journey)
        {
            problem = $"--{act} only applies to 'journey'.";
            return null;
        }

        // Every noun shares where it stands and its order with audit, a
        // Journey only where it stands; the rest of audit's words are audit's.
        string? foreign = known.Command is Command.Journey
            ? auditOptions.FirstOrDefault(option => option != "--location")
            : noun
                ? auditOptions.FirstOrDefault(option => !NounArguments.Shared.Contains(option))
                : known.Command is Command.Audit ? null : auditOptions.FirstOrDefault();

        if (foreign is not null)
        {
            problem = $"{foreign} only applies to 'audit'.";
            return null;
        }

        if (Stray(known.Command, nounOptions) is { } stray)
        {
            problem = stray;
            return null;
        }

        string? refused = null;
        SubscriptionAct? taken = known.Command is Command.Subscriptions
            ? SubscriptionArguments.Act(act, out refused)
            : null;
        DeadMessageAct? replay = known.Command is Command.DeadMessages
            ? DeadMessageArguments.Act(act, out refused)
            : null;
        JourneyAct? journeyAct = known.Command is Command.Journey
            ? JourneyArguments.Act(act, out refused)
            : null;

        if (refused is not null)
        {
            problem = refused;
            return null;
        }

        string argument = arguments == 1 ? words[1] : string.Empty;
        string? pattern = argument.Length > 0 ? argument : null;

        problem = string.Empty;

        return new Invocation(
            known.Command,
            argument,
            json || follow,
            follow,
            runtime,
            remote,
            snapshot,
            who,
            known.Command is Command.Audit
                ? (audit ?? new AuditQuery()) with { Pattern = pattern }
                : null,
            known.Command is Command.EventSubscriptions
                ? new EventSubscriptionQuery
                {
                    Pattern = pattern,
                    Location = audit?.Location,
                    Id = id,
                    Sort = audit?.Sort,
                    Order = audit?.Order,
                }
                : null,
            known.Command is Command.EventSubscriptions
                ? EventSubscriptionArguments.Act(act)
                : null,
            known.Command is Command.Subscriptions
                ? new SubscriptionQuery
                {
                    Pattern = pattern,
                    Location = audit?.Location,
                    Name = name,
                    Sort = audit?.Sort,
                    Order = audit?.Order,
                }
                : null,
            taken,
            known.Command is Command.DeadMessages
                ? new DeadMessageQuery
                {
                    Pattern = pattern,
                    Location = audit?.Location,
                    Message = message,
                    Sort = audit?.Sort,
                    Order = audit?.Order,
                }
                : null,
            replay,
            journeyAct,
            known.Command is Command.Journey ? audit?.Location : null);
    }

    // The first option of the nouns' that the command named does not take,
    // said as the line's problem; null when there is none.
    private static string? Stray(Command command, List<string> nounOptions)
    {
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
