using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip-cli dead-messages</c> (ADR-0052, amendment 2026-10-01): one
/// command for the noun, the act an option on it. The line becomes the one
/// query every surface asks; what a snapshot of the test cluster lists is
/// rendered, drilled and sorted; one Message named is opened with its gate
/// verdicts, promoted properties and declines; a Replay names one Message or
/// is refused before any node is asked; one taken is left where the
/// publication says; and no other noun's act is a Message's.
/// </summary>
public sealed class DeadMessageCommandTests : IDisposable
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's first two nodes, by place: the first's queue keeps
    // two Messages, the second's one.
    private static readonly string First = Cluster.Nodes[0];
    private static readonly string Second = Cluster.Nodes[1];

    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-cli-dead-messages-{Guid.NewGuid():N}");

    public DeadMessageCommandTests()
    {
        Directory.CreateDirectory(_place);
        File.WriteAllText(
            Snapshot,
            $"node = \"{Cluster.Scope}\"\n"
            + $"orders = '{Orders}'\n"
            + Entry(First, "m-1", 1, "declines = [[\"structured\", \"no MessageType\"]]\n")
            + Entry(
                First, "m-2", 2,
                "validation = [[\"schema\", \"valid\"]]\n"
                + "promoted = [[\"MessageType\", \"Invoice\"]]\n"
                + "declines = [[\"structured\", \"MessageType is Invoice\"], "
                + "[\"edi\", \"paused\"]]\n")
            + Entry(Second, "m-3", 1, string.Empty));
    }

    private string Snapshot => Path.Combine(_place, $"{Cluster.Name}-snapshot.toml");

    private string Orders => Path.Combine(_place, "orders");

    public void Dispose()
    {
        Directory.Delete(_place, recursive: true);
    }

    private static string Entry(string node, string message, int sequence, string rest)
    {
        return $"[[dead_messages]]\nnode = \"{Cluster.Scope}/node/{node}\"\n"
            + $"message = \"{message}\"\nsequence = {sequence}\nlocation = \"orders\"\n"
            + $"received_unix_nanos = {sequence * 1_000_000_000L}\n{rest}";
    }

    private static (int Exit, string Out, string Error) Run(
        IOperatorSurface surface, Invocation invocation)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = DeadMessageCommand.Over(
            surface, invocation.DeadMessages ?? new DeadMessageQuery(), invocation.Replay,
            ScopeOperation.Who(invocation.Who), invocation.Json, output, error);

        return (exit, output.ToString(), error.ToString());
    }

    private static Invocation Line(params string[] args)
    {
        Invocation? parsed = Invocation.Parse(args, out string problem);
        Assert.True(parsed is not null, problem);
        return parsed;
    }

    [Fact]
    public void TheLineIsTheQueryAndReplayAnOption()
    {
        Invocation parsed = Line(
            "dead-messages", $"*/{First}", "--location", Cluster.NodeScope(0), "--message",
            "m-2", "--sort", "declines", "--order", "descending", "--replay", "--who", "ilian");

        Assert.Equal(Command.DeadMessages, parsed.Command);
        Assert.Equal(
            new DeadMessageQuery
            {
                Pattern = $"*/{First}",
                Location = Cluster.NodeScope(0),
                Message = "m-2",
                Sort = "declines",
                Order = "descending",
            },
            parsed.DeadMessages);
        Assert.Equal(DeadMessageAct.Replay, parsed.Replay);
        Assert.Null(parsed.Subscriptions);
        Assert.Equal("ilian", parsed.Who);
    }

    [Theory]
    [InlineData("dead-messages --pause", "--replay is its one act")]
    [InlineData("dead-messages --remove", "--replay is its one act")]
    [InlineData("dead-messages --replay --pause", "one act")]
    [InlineData("dead-messages --message", "--message needs")]
    [InlineData("subscriptions --replay", "--replay only applies to 'dead-messages'")]
    [InlineData("event-subscriptions --replay", "--replay only applies to 'dead-messages'")]
    [InlineData("subscriptions --message m-1", "--message only applies to 'dead-messages'")]
    [InlineData("dead-messages --name edi", "--name only applies to 'subscriptions'")]
    [InlineData("dead-messages --program xmip-cli", "--program only applies to 'audit'")]
    [InlineData("dead-messages --who ilian", "--who only applies")]
    public void ALineThatCannotBeObeyedIsSaidSo(string line, string said)
    {
        Assert.Null(Invocation.Parse(line.Split(' '), out string problem));
        Assert.Contains(said, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void TheListSaysMessageClusterNodeAndDeclinesAsTextAndAsADocument()
    {
        SnapshotOperator surface = new(Snapshot);

        (int exit, string text, _) = Run(surface, Line("dead-messages"));
        Assert.Equal(0, exit);
        Assert.Contains("3 Message(s) in the Dead Message Queue", text, StringComparison.Ordinal);
        Assert.Matches($@"m-2\s+{Cluster.Name}\s+{First}\s+orders\s+2", text);

        (_, string json, _) = Run(
            surface, Line("dead-messages", "--location", Cluster.NodeScope(0), "--json"));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement[] listed =
            [.. document.RootElement.GetProperty("dead_messages").EnumerateArray()];
        Assert.Equal(2, listed.Length);
        Assert.Equal("m-1", listed[0].GetProperty("message").GetString());
        JsonElement declines = listed[1].GetProperty("declines");
        Assert.Equal(2, declines.GetArrayLength());
        Assert.Equal("edi", declines[1].GetProperty("name").GetString());
        Assert.Equal(
            "Invoice", listed[1].GetProperty("promoted")[0].GetProperty("value").GetString());
    }

    [Fact]
    public void OneMessageNamedIsOpenedWithItsVerdictsPropertiesAndDeclines()
    {
        (int exit, string text, _) = Run(
            new SnapshotOperator(Snapshot),
            Line("dead-messages", "--location", Cluster.NodeScope(0), "--message", "m-2"));

        Assert.Equal(0, exit);
        Assert.Contains(
            $"Message m-2 in the Dead Message Queue of {Cluster.NodeScope(0)}", text,
            StringComparison.Ordinal);
        Assert.Matches(@"gate verdicts:\s+schema\s+valid", text);
        Assert.Matches(@"promoted properties:\s+MessageType\s+Invoice", text);
        Assert.Matches(@"declines:\s+structured\s+MessageType is Invoice\s+edi\s+paused", text);
    }

    [Fact]
    public void ThePatternDrillsAndTheSortOrders()
    {
        SnapshotOperator surface = new(Snapshot);

        (_, string named, _) = Run(
            surface, Line("dead-messages", "*/dead-message/m-3", "--json"));
        using JsonDocument one = JsonDocument.Parse(named);
        Assert.Equal(
            "m-3",
            Assert.Single(one.RootElement.GetProperty("dead_messages").EnumerateArray())
                .GetProperty("message").GetString());

        (_, string sorted, _) = Run(
            surface,
            Line("dead-messages", "--sort", "declines", "--order", "descending", "--json"));
        using JsonDocument all = JsonDocument.Parse(sorted);
        Assert.Equal(
            ["m-2", "m-1", "m-3"],
            all.RootElement.GetProperty("dead_messages").EnumerateArray()
                .Select(entry => entry.GetProperty("message").GetString()));

        (int exit, _, string error) = Run(surface, Line("dead-messages", "--sort", "held"));
        Assert.Equal(2, exit);
        Assert.StartsWith("REFUSED", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AReplayNamesOneMessageOrIsRefusedBeforeAnyNodeIsAsked()
    {
        SnapshotOperator surface = new(Snapshot);

        (int exit, _, string error) = Run(surface, Line("dead-messages", "--replay"));
        Assert.Equal(2, exit);
        Assert.StartsWith("REFUSED", error, StringComparison.Ordinal);

        (exit, _, error) = Run(
            surface,
            Line(
                "dead-messages", "--location", Cluster.NodeScope(1), "--message", "m-2",
                "--replay"));
        Assert.Equal(1, exit);
        Assert.Contains("no Message m-2", error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Orders));

        (exit, string said, _) = Run(
            surface,
            Line(
                "dead-messages", "--location", Cluster.NodeScope(0), "--message", "m-2",
                "--replay", "--who", "ilian"));
        Assert.Equal(0, exit);
        Assert.StartsWith(
            $"OK. replay of Message m-2 left for {First}", said, StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(Path.Combine(Orders, First), "*-replay.toml"));
    }
}
