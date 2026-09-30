using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip-cli subscriptions</c>: the Subscriptions the cluster's nodes route
/// by, and pause or resume on one of them (ADR-0013, amendment 2026-09-30).
/// A Subscription picks a published Message up and opens a Journey; it is
/// configuration, so an operator never removes one
/// (<see cref="SubscriptionOperation.Configured"/>) — and it is not an Event
/// subscription, which is <see cref="EventSubscriptionCommand"/>'s. One
/// command for the noun, the act an option on it, as the PowerShell module's
/// one cmdlet takes it as a parameter. Which Subscriptions a line selects and
/// in what order is <see cref="SubscriptionQuery"/>'s, the one every surface
/// asks; how an act reaches the node is the surface's; only the rendering is
/// here. An act is exit 0 when it was applied or left for the node, 1 when
/// it was not.
/// </summary>
public static class SubscriptionCommand
{
    // The columns a person reads, in the order SubscriptionQuery lists them.
    private static readonly string[] Headings =
        ["subscription", "cluster", "node", "filter", "destination", "state", "picked-up",
         "held", "since"];

    /// <summary>List what <paramref name="query"/> selects, or — where
    /// <paramref name="act"/> is given — take it on the one Subscription the
    /// query names.</summary>
    public static int Over(
        IOperatorSurface surface,
        SubscriptionQuery query,
        SubscriptionAct? act,
        string who,
        bool json,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        IReadOnlyList<SubscriptionRecord> chosen;

        try
        {
            chosen = query.Apply(surface.Subscriptions().Subscriptions);
        }
        catch (ArgumentException refused)
        {
            string said = English.Refusal(refused);
            error.WriteLine(json
                ? JsonText.Document(writer => writer.WriteString("refused", said))
                : said);
            return 2;
        }

        return act is { } taken
            ? Act(surface, query, chosen, taken, who, json, output, error)
            : List(surface, chosen, json, output);
    }

    /// <summary>The Subscriptions as one document.</summary>
    public static string Document(string source, IReadOnlyList<SubscriptionRecord> chosen)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        return JsonText.Document(writer =>
        {
            writer.WriteString("source", source);
            writer.WriteNumber("matched", chosen.Count);
            writer.WriteStartArray("subscriptions");

            foreach (SubscriptionRecord entry in chosen)
            {
                writer.WriteStartObject();
                writer.WriteString("node", entry.Node);
                writer.WriteString("name", entry.Name);
                writer.WriteString("cluster", SubscriptionQuery.Cluster(entry));
                writer.WriteString("application", entry.Application);
                writer.WriteString("filter", entry.Filter);
                writer.WriteString("destination", entry.Destination);
                writer.WriteString("file", entry.File);
                writer.WriteString("configuration", entry.Configuration);
                writer.WriteString("state", entry.State);
                writer.WriteBoolean("paused", entry.Paused);
                writer.WriteString("by", entry.By);
                writer.WriteNumber("picked_up", entry.PickedUp);
                writer.WriteNumber("held", entry.Held);
                writer.WriteString("since", entry.Since);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }

    /// <summary>What came of an act, as one document.</summary>
    public static string Document(SubscriptionOperation done)
    {
        ArgumentNullException.ThrowIfNull(done);

        return JsonText.Document(writer =>
        {
            writer.WriteString("node", done.Node);
            writer.WriteString("name", done.Name);
            writer.WriteString("act", SubscriptionOperation.Word(done.Act));
            writer.WriteBoolean("applied", done.Applied);
            writer.WriteString("result", done.Result);
        });
    }

    private static int List(
        IOperatorSurface surface,
        IReadOnlyList<SubscriptionRecord> chosen,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            output.WriteLine(Document(surface.Source, chosen));
            return 0;
        }

        output.WriteLine(
            $"{chosen.Count} Subscription(s), {chosen.Count(entry => entry.Paused)} paused, "
            + $"{English.Figure(chosen.Aggregate(0ul, (sum, entry) => sum + entry.Held))} "
            + $"held — {surface.Source}");

        if (chosen.Count > 0)
        {
            TextTable.Write(output, "  ", [Headings, .. chosen.Select(Row)]);
        }

        return 0;
    }

    private static string[] Row(SubscriptionRecord entry)
    {
        return
        [
            entry.Name,
            SubscriptionQuery.Cluster(entry),
            SubscriptionQuery.NodeName(entry),
            entry.Filter,
            entry.Destination,
            entry.State,
            English.Figure(entry.PickedUp),
            English.Figure(entry.Held),
            English.Age(entry.Since),
        ];
    }

    // An act names one Subscription: a node, by --location, and its name, by
    // --name. Anything else is refused before the node is asked.
    private static int Act(
        IOperatorSurface surface,
        SubscriptionQuery query,
        IReadOnlyList<SubscriptionRecord> chosen,
        SubscriptionAct act,
        string who,
        bool json,
        TextWriter output,
        TextWriter error)
    {
        if (query.Name is not { Length: > 0 } name
            || query.Location is not { Length: > 0 } location
            || ScopeTree.Node(location).Length == 0)
        {
            error.WriteLine(
                $"REFUSED: to {SubscriptionOperation.Word(act)} a Subscription, name its node "
                + "with --location xmip:///<cluster>/node/<name> and it with --name.");
            return 2;
        }

        if (chosen.SingleOrDefault(entry => ScopeTree.Beneath(location, entry.Node)) is not { } one)
        {
            error.WriteLine(
                $"REFUSED: no Subscription '{name}' is listed on {location}; no Xmip "
                + "Application draws it there.");
            return 1;
        }

        SubscriptionOperation done = surface.Act(one, act, who);

        if (json)
        {
            output.WriteLine(Document(done));
        }
        else
        {
            (done.Applied ? output : error).WriteLine(
                done.Applied ? $"OK. {done.Result}" : $"REFUSED: {done.Result}");
        }

        return done.Applied ? 0 : 1;
    }
}
