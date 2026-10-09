using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip-cli event-subscriptions</c> (ADR-0065, amendment 2026-09-29): one
/// command for the noun, the act an option on it. The line becomes the one
/// query every surface asks; what a snapshot of the test cluster lists is
/// rendered; an act names one Event subscription or is refused before any
/// node is asked; and one taken is left where the publication says.
/// </summary>
public sealed class EventSubscriptionCommandTests : IDisposable
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's first two nodes, by place, each holding an Event
    // subscription; the first does not hear the third.
    private static readonly string First = Cluster.Nodes[0];
    private static readonly string Second = Cluster.Nodes[1];

    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-cli-event-subscriptions-{Guid.NewGuid():N}");

    public EventSubscriptionCommandTests()
    {
        Directory.CreateDirectory(_place);
        File.WriteAllText(
            Snapshot,
            $"node = \"{Cluster.Scope}\"\n"
            + $"orders = '{Orders}'\n"
            + $"[[event_subscriptions]]\nnode = \"{Cluster.NodeScope(0)}\"\nid = 1\n"
            + "subscriber = \"operations\"\nparty = \"0199a0a0-0000-7000-8000-000000000001\"\n"
            + "action = \"every Event\"\nstate = \"active\"\nqueued = 2\ncapacity = 64\n"
            + $"[[event_subscriptions]]\nnode = \"{Cluster.NodeScope(1)}\"\nid = 2\n"
            + "subscriber = \"on-call\"\nparty = \"0199a0a0-0000-7000-8000-000000000002\"\n"
            + "action = \"every Event ending failure\"\nstate = \"paused\"\nqueued = 7\n"
            + $"[[unheard]]\nby = \"{Cluster.NodeScope(0)}\"\n"
            + $"node = \"{Cluster.NodeScope(2)}\"\n"
            + "since_unix_nanos = 1790000000000000000\nwhy = \"connection refused\"\n");
    }

    private string Snapshot => Path.Combine(_place, $"{Cluster.Name}-snapshot.toml");

    private string Orders => Path.Combine(_place, "orders");

    public void Dispose()
    {
        Directory.Delete(_place, recursive: true);
    }

    private static (int Exit, string Out, string Error) Run(
        IOperatorSurface surface, Invocation invocation)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = EventSubscriptionCommand.Over(
            surface, invocation.EventSubscriptions ?? new EventSubscriptionQuery(),
            invocation.EventAct,
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
    public void TheLineIsTheQueryAndTheActAnOption()
    {
        Invocation parsed = Line(
            "event-subscriptions", $"*/{First}", "--location", Cluster.NodeScope(0), "--id", "3",
            "--sort", "queued", "--order", "descending", "--pause", "--who", "ilian");

        Assert.Equal(Command.EventSubscriptions, parsed.Command);
        Assert.Equal(
            new EventSubscriptionQuery
            {
                Pattern = $"*/{First}",
                Location = Cluster.NodeScope(0),
                Id = 3,
                Sort = "queued",
                Order = "descending",
            },
            parsed.EventSubscriptions);
        Assert.Equal(EventSubscriptionAct.Pause, parsed.EventAct);
        Assert.Null(parsed.Subscriptions);
        Assert.Equal("ilian", parsed.Who);
    }

    [Theory]
    [InlineData("event-subscriptions --pause --remove", "one act")]
    [InlineData("event-subscriptions --id x", "--id needs")]
    [InlineData("event-subscriptions --severity Error", "--severity only applies to 'audit'")]
    [InlineData("list --id 3", "--id only applies to 'event-subscriptions'")]
    [InlineData("event-subscriptions --who ilian", "--who only applies")]
    public void ALineThatCannotBeObeyedIsSaidSo(string line, string said)
    {
        Assert.Null(Invocation.Parse(line.Split(' '), out string problem));
        Assert.Contains(said, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void TheListSaysSubscriberClusterNodeAndActionAsTextAndAsADocument()
    {
        SnapshotOperator surface = new(Snapshot);

        (int exit, string text, _) = Run(surface, Line("event-subscriptions"));
        Assert.Equal(0, exit);
        Assert.Contains("2 Event subscription(s), 1 paused", text, StringComparison.Ordinal);
        Assert.Matches(
            $@"1\s+operations\s+{Cluster.Name}\s+{First}\s+every Event\s+active", text);
        Assert.DoesNotContain("0199a0a0", text, StringComparison.Ordinal);
        Assert.Contains(
            $"{First}: not hearing {Cluster.NodeScope(2)} since 2026-09-21T14:13:20Z: "
            + "connection refused",
            text,
            StringComparison.Ordinal);

        (_, string json, _) = Run(
            surface, Line("event-subscriptions", "--location", Cluster.NodeScope(1), "--json"));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement one = Assert.Single(
            document.RootElement.GetProperty("event_subscriptions").EnumerateArray());
        Assert.EndsWith(
            $"/{Second}", one.GetProperty("node").GetString(), StringComparison.Ordinal);
        Assert.True(one.GetProperty("paused").GetBoolean());
        Assert.Equal("on-call", one.GetProperty("subscriber").GetString());
        Assert.Equal(
            "0199a0a0-0000-7000-8000-000000000002", one.GetProperty("party").GetString());
        Assert.Empty(document.RootElement.GetProperty("unheard").EnumerateArray());
    }

    [Fact]
    public void AnActNamesOneSubscriptionOrIsRefusedBeforeAnyNodeIsAsked()
    {
        SnapshotOperator surface = new(Snapshot);

        (int exit, _, string error) = Run(surface, Line("event-subscriptions", "--pause"));
        Assert.Equal(2, exit);
        Assert.StartsWith("REFUSED", error, StringComparison.Ordinal);

        (exit, _, error) = Run(
            surface,
            Line(
                "event-subscriptions", "--location", Cluster.NodeScope(0), "--id", "9",
                "--pause"));
        Assert.Equal(1, exit);
        Assert.Contains("no Event subscription 9", error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Orders));

        (exit, string said, _) = Run(
            surface,
            Line(
                "event-subscriptions", "--location", Cluster.NodeScope(1), "--id", "2",
                "--resume"));
        Assert.Equal(0, exit);
        Assert.StartsWith(
            $"OK. resume of Event subscription 2 left for {Second}", said,
            StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(Path.Combine(Orders, Second), "*-resume.toml"));
    }
}
