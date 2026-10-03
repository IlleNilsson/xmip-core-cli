using System.Globalization;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip-cli event-subscriptions</c>: the Event subscriptions the cluster's
/// nodes hold, and pause, resume or remove on one of them (ADR-0065,
/// amendment 2026-09-29) — not the Subscriptions that pick a published
/// Message up, which are <see cref="SubscriptionCommand"/>'s. One command for
/// the noun, the act an option on it, as the PowerShell module's one cmdlet
/// takes it as a parameter. Which Event subscriptions a line selects and in
/// what order is <see cref="EventSubscriptionQuery"/>'s, the one every
/// surface asks; how an act reaches the node is the surface's; only the
/// rendering is here. An act is
/// exit 0 when it was applied or left for the node, 1 when it was not.
/// </summary>
public static class EventSubscriptionCommand
{
    // The columns a person reads, in the order EventSubscriptionQuery lists
    // them, with the number that names an Event subscription on its node first.
    private static readonly string[] Headings =
        ["id", "subscriber", "cluster", "node", "action", "state", "queued", "delivered",
         "missed", "since"];

    /// <summary>List what <paramref name="query"/> selects, or — where
    /// <paramref name="act"/> is given — take it on the one subscription the
    /// query names.</summary>
    public static int Over(
        IOperatorSurface surface,
        EventSubscriptionQuery query,
        EventSubscriptionAct? act,
        string who,
        bool json,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        IReadOnlyList<EventSubscriptionRecord> chosen;
        IReadOnlyList<UnheardRecord> unheard;

        try
        {
            EventSubscriptionList listed = surface.EventSubscriptions();
            chosen = query.Apply(listed.EventSubscriptions);
            unheard = query.Unheard(listed.Unheard);
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
            : List(surface, chosen, unheard, json, output);
    }

    /// <summary>The Event subscriptions, and the members their nodes do not
    /// hear, as one document.</summary>
    public static string Document(
        string source,
        IReadOnlyList<EventSubscriptionRecord> chosen,
        IReadOnlyList<UnheardRecord> unheard)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(unheard);

        return JsonText.Document(writer =>
        {
            writer.WriteString("source", source);
            writer.WriteNumber("matched", chosen.Count);
            writer.WriteStartArray("event_subscriptions");

            foreach (EventSubscriptionRecord entry in chosen)
            {
                writer.WriteStartObject();
                writer.WriteString("node", entry.Node);
                writer.WriteNumber("id", entry.Id);
                writer.WriteString("subscriber", EventSubscriptionQuery.Who(entry));
                writer.WriteString("party", entry.Party);
                writer.WriteString("cluster", EventSubscriptionQuery.Cluster(entry));
                writer.WriteString("action", entry.Action);
                writer.WriteString("scope", entry.Scope);
                writer.WriteString("state", entry.State);
                writer.WriteBoolean("paused", entry.Paused);
                writer.WriteNumber("queued", entry.Queued);
                writer.WriteNumber("capacity", entry.Capacity);
                writer.WriteNumber("delivered", entry.Delivered);
                writer.WriteNumber("missed", entry.Missed);
                writer.WriteString("since", entry.Since);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("unheard");

            foreach (UnheardRecord gone in unheard)
            {
                writer.WriteStartObject();
                writer.WriteString("by", gone.By);
                writer.WriteString("node", gone.Node);
                writer.WriteString("since", gone.Since);
                writer.WriteString("why", gone.Why);
                writer.WriteString("said", gone.Said);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }

    /// <summary>What came of an act, as one document.</summary>
    public static string Document(EventSubscriptionOperation done)
    {
        ArgumentNullException.ThrowIfNull(done);

        return JsonText.Document(writer =>
        {
            writer.WriteString("node", done.Node);
            writer.WriteNumber("id", done.Id);
            writer.WriteString("act", EventSubscriptionOperation.Word(done.Act));
            writer.WriteBoolean("applied", done.Applied);
            writer.WriteString("result", done.Result);
        });
    }

    private static int List(
        IOperatorSurface surface,
        IReadOnlyList<EventSubscriptionRecord> chosen,
        IReadOnlyList<UnheardRecord> unheard,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            output.WriteLine(Document(surface.Source, chosen, unheard));
            return 0;
        }

        output.WriteLine(
            $"{chosen.Count} Event subscription(s), "
            + $"{chosen.Count(entry => entry.Paused)} paused — {surface.Source}");

        if (chosen.Count > 0)
        {
            TextTable.Write(output, "  ", [Headings, .. chosen.Select(Row)]);
        }

        // Read-only: what the nodes here do not hear, so no Event is missing
        // silently; the links between nodes are never listed or acted on.
        foreach (UnheardRecord gone in unheard)
        {
            output.WriteLine($"  {EventSubscriptionQuery.Line(gone)}");
        }

        return 0;
    }

    private static string[] Row(EventSubscriptionRecord entry)
    {
        return
        [
            entry.Id.ToString(CultureInfo.InvariantCulture),
            EventSubscriptionQuery.Who(entry),
            EventSubscriptionQuery.Cluster(entry),
            EventSubscriptionQuery.NodeName(entry),
            entry.Action,
            entry.State,
            $"{English.Figure(entry.Queued)}/{English.Figure(entry.Capacity)}",
            English.Figure(entry.Delivered),
            English.Figure(entry.Missed),
            English.Age(entry.Since),
        ];
    }

    // An act names one Event subscription: a node, by --location, and its number,
    // by --id. Anything else is refused before the node is asked.
    private static int Act(
        IOperatorSurface surface,
        EventSubscriptionQuery query,
        IReadOnlyList<EventSubscriptionRecord> chosen,
        EventSubscriptionAct act,
        string who,
        bool json,
        TextWriter output,
        TextWriter error)
    {
        if (query.Id is null || query.Location is not { Length: > 0 } location
            || ScopeTree.Node(location).Length == 0)
        {
            error.WriteLine(
                $"REFUSED: to {EventSubscriptionOperation.Word(act)} an Event subscription, "
                + "name its node with --location xmip:///<cluster>/node/<name> and its "
                + "number with --id.");
            return 2;
        }

        if (chosen.SingleOrDefault(entry => ScopeTree.Beneath(location, entry.Node)) is not { } one)
        {
            error.WriteLine(
                $"REFUSED: no Event subscription {query.Id} is listed on {location}; "
                + "it was removed, or never made.");
            return 1;
        }

        EventSubscriptionOperation done = surface.Act(one, act, who);

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
