using System.Globalization;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip-cli subscriptions</c>: the Event subscriptions the cluster's nodes
/// hold, and pause, resume or remove on one of them (ADR-0065, amendment
/// 2026-09-29). One command for the noun, the act an option on it, as the
/// PowerShell module's one cmdlet takes it as a parameter. Which
/// subscriptions a line selects and in what order is
/// <see cref="SubscriptionQuery"/>'s, the one every surface asks; how an act
/// reaches the node is the surface's; only the rendering is here. An act is
/// exit 0 when it was applied or left for the node, 1 when it was not.
/// </summary>
public static class SubscriptionCommand
{
    // The columns a person reads, in the order SubscriptionQuery lists them,
    // with the number that names a subscription on its node first.
    private static readonly string[] Headings =
        ["id", "subscriber", "cluster", "node", "action", "state", "queued", "delivered",
         "missed", "since"];

    /// <summary>List what <paramref name="query"/> selects, or — where
    /// <paramref name="act"/> is given — take it on the one subscription the
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

    /// <summary>The subscriptions as one document.</summary>
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
                writer.WriteNumber("id", entry.Id);
                writer.WriteString("subscriber", SubscriptionQuery.Who(entry));
                writer.WriteString("party", entry.Party);
                writer.WriteString("cluster", SubscriptionQuery.Cluster(entry));
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
        });
    }

    /// <summary>What came of an act, as one document.</summary>
    public static string Document(SubscriptionOperation done)
    {
        ArgumentNullException.ThrowIfNull(done);

        return JsonText.Document(writer =>
        {
            writer.WriteString("node", done.Node);
            writer.WriteNumber("id", done.Id);
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
            $"{chosen.Count} subscription(s), {chosen.Count(entry => entry.Paused)} paused — "
            + surface.Source);

        if (chosen.Count == 0)
        {
            return 0;
        }

        string[][] rows = [Headings, .. chosen.Select(Row)];
        int[] widths =
        [
            .. Enumerable.Range(0, Headings.Length).Select(column => rows.Max(row => row[column].Length)),
        ];

        foreach (string[] row in rows)
        {
            output.WriteLine("  " + string.Join("  ", row.Select(
                (cell, column) => column == row.Length - 1 ? cell : cell.PadRight(widths[column]))));
        }

        return 0;
    }

    private static string[] Row(SubscriptionRecord entry)
    {
        return
        [
            entry.Id.ToString(CultureInfo.InvariantCulture),
            SubscriptionQuery.Who(entry),
            SubscriptionQuery.Cluster(entry),
            SubscriptionQuery.NodeName(entry),
            entry.Action,
            entry.State,
            $"{English.Figure(entry.Queued)}/{English.Figure(entry.Capacity)}",
            English.Figure(entry.Delivered),
            English.Figure(entry.Missed),
            English.Age(entry.Since),
        ];
    }

    // An act names one subscription: a node, by --location, and its number,
    // by --id. Anything else is refused before the node is asked.
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
        if (query.Id is null || query.Location is not { Length: > 0 } location
            || ScopeTree.Node(location).Length == 0)
        {
            error.WriteLine(
                $"REFUSED: to {SubscriptionOperation.Word(act)} a subscription, name its node "
                + "with --location xmip:///<cluster>/node/<name> and its number with --id.");
            return 2;
        }

        if (chosen.SingleOrDefault(entry => ScopeTree.Beneath(location, entry.Node)) is not { } one)
        {
            error.WriteLine(
                $"REFUSED: no subscription {query.Id} is listed on {location}; it was removed, "
                + "or never made.");
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
