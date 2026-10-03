using System.Globalization;
using System.Text.Json;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip-cli dead-messages</c>: what each node's Dead Message Queue keeps
/// — every accepted Message no Subscription matched — one opened with its
/// gate verdicts, promoted properties and every Subscription's decline, and
/// Replay on one of them (ADR-0052, amendment 2026-10-01). It is not a dead
/// letter queue: a failed Journey never goes there. One command for the
/// noun, the act an option on it, as the PowerShell module's one cmdlet
/// takes it as a parameter. Which entries a line selects and in what order
/// is <see cref="DeadMessageQuery"/>'s, the one every surface asks; how a
/// Replay reaches the node is the surface's; only the rendering is here. A
/// Replay is exit 0 when it was applied or left for the node, 1 when it was
/// not.
/// </summary>
public static class DeadMessageCommand
{
    // The columns a person reads, in the order DeadMessageQuery lists them.
    private static readonly string[] Headings =
        ["received", "message", "cluster", "node", "receive-location", "declines"];

    /// <summary>List what <paramref name="query"/> selects — opening the one
    /// Message it names — or, where <paramref name="act"/> is given, take it
    /// on that Message.</summary>
    public static int Over(
        IOperatorSurface surface,
        DeadMessageQuery query,
        DeadMessageAct? act,
        string who,
        bool json,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        IReadOnlyList<DeadMessageRecord> chosen;

        try
        {
            chosen = query.Apply(surface.DeadMessages().DeadMessages);
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
            : List(surface, query, chosen, json, output);
    }

    /// <summary>The entries as one document, each with its pairs.</summary>
    public static string Document(string source, IReadOnlyList<DeadMessageRecord> chosen)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        return JsonText.Document(writer =>
        {
            writer.WriteString("source", source);
            writer.WriteNumber("matched", chosen.Count);
            writer.WriteStartArray("dead_messages");

            foreach (DeadMessageRecord entry in chosen)
            {
                writer.WriteStartObject();
                writer.WriteString("node", entry.Node);
                writer.WriteString("message", entry.Message);
                writer.WriteString("cluster", DeadMessageQuery.Cluster(entry));
                writer.WriteNumber("sequence", entry.Sequence);
                writer.WriteString("location", entry.ReceiveLocation);
                writer.WriteString("received", entry.Received);
                Pairs(writer, "validation", entry.Validation);
                Pairs(writer, "promoted", entry.Promoted);
                Pairs(writer, "declines", entry.Declines);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }

    /// <summary>What came of a Replay, as one document.</summary>
    public static string Document(DeadMessageOperation done)
    {
        ArgumentNullException.ThrowIfNull(done);

        return JsonText.Document(writer =>
        {
            writer.WriteString("node", done.Node);
            writer.WriteString("message", done.Message);
            writer.WriteString("act", DeadMessageOperation.Word(done.Act));
            writer.WriteBoolean("applied", done.Applied);
            writer.WriteString("result", done.Result);
        });
    }

    private static void Pairs(
        Utf8JsonWriter writer, string name, IReadOnlyList<KeyValuePair<string, string>> pairs)
    {
        writer.WriteStartArray(name);

        foreach ((string key, string value) in pairs)
        {
            writer.WriteStartObject();
            writer.WriteString("name", key);
            writer.WriteString("value", value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static int List(
        IOperatorSurface surface,
        DeadMessageQuery query,
        IReadOnlyList<DeadMessageRecord> chosen,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            output.WriteLine(Document(surface.Source, chosen));
            return 0;
        }

        // One Message named and found: opened.
        if (query.Message is { Length: > 0 } && chosen is [var one])
        {
            Open(one, output);
            return 0;
        }

        output.WriteLine(
            $"{chosen.Count} Message(s) in the Dead Message Queue — {surface.Source}");

        if (chosen.Count > 0)
        {
            TextTable.Write(output, "  ", [Headings, .. chosen.Select(Row)]);
        }

        return 0;
    }

    private static string[] Row(DeadMessageRecord entry)
    {
        return
        [
            English.Age(entry.Received),
            entry.Message,
            DeadMessageQuery.Cluster(entry),
            DeadMessageQuery.NodeName(entry),
            entry.ReceiveLocation,
            entry.Declines.Count.ToString(CultureInfo.InvariantCulture),
        ];
    }

    // One entry, every pair it carries, under the heading of each kind.
    private static void Open(DeadMessageRecord entry, TextWriter output)
    {
        output.WriteLine($"Message {entry.Message} in the Dead Message Queue of {entry.Node}");
        output.WriteLine(
            $"  received at {entry.ReceiveLocation}, "
            + $"{entry.Received.ToString("O", CultureInfo.InvariantCulture)}, "
            + $"place {entry.Sequence.ToString(CultureInfo.InvariantCulture)}");

        Section(output, "gate verdicts", entry.Validation);
        Section(output, "promoted properties", entry.Promoted);
        Section(output, "declines", entry.Declines);
    }

    private static void Section(
        TextWriter output, string heading, IReadOnlyList<KeyValuePair<string, string>> pairs)
    {
        if (pairs.Count == 0)
        {
            output.WriteLine($"  {heading}: none");
            return;
        }

        output.WriteLine($"  {heading}:");
        TextTable.Write(output, "    ", [.. pairs.Select(pair => new[] { pair.Key, pair.Value })]);
    }

    // A Replay names one Message: a node, by --location, and the Message, by
    // --message. Anything else is refused before the node is asked.
    private static int Act(
        IOperatorSurface surface,
        DeadMessageQuery query,
        IReadOnlyList<DeadMessageRecord> chosen,
        DeadMessageAct act,
        string who,
        bool json,
        TextWriter output,
        TextWriter error)
    {
        if (query.Message is not { Length: > 0 } message
            || query.Location is not { Length: > 0 } location
            || ScopeTree.Node(location).Length == 0)
        {
            error.WriteLine(
                $"REFUSED: to {DeadMessageOperation.Word(act)} a Message, name its node with "
                + "--location xmip:///<cluster>/node/<name> and it with --message.");
            return 2;
        }

        if (chosen.SingleOrDefault(entry => ScopeTree.Beneath(location, entry.Node)) is not { } one)
        {
            error.WriteLine(
                $"REFUSED: no Message {message} is in the Dead Message Queue of {location}.");
            return 1;
        }

        DeadMessageOperation done = surface.Act(one, act, who);

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
