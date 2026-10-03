using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip-cli subscriptions</c> (ADR-0013, amendment 2026-09-30): one
/// command for the noun, the act an option on it. The line becomes the one
/// query every surface asks; what a snapshot of the test cluster lists is
/// rendered, drilled and sorted; an act names one Subscription or is refused
/// before any node is asked; one taken is left where the publication says;
/// and remove is refused in words, because a Subscription is configuration.
/// </summary>
public sealed class SubscriptionCommandTests : IDisposable
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's first two nodes, by place: the first routes by two
    // Subscriptions, the second by one.
    private static readonly string First = Cluster.Nodes[0];
    private static readonly string Second = Cluster.Nodes[1];

    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-cli-subscriptions-{Guid.NewGuid():N}");

    public SubscriptionCommandTests()
    {
        Directory.CreateDirectory(_place);
        File.WriteAllText(
            Snapshot,
            $"node = \"{Cluster.Scope}\"\n"
            + $"orders = '{Orders}'\n"
            + Entry(First, "structured", "active", 12, 0)
            + Entry(First, "edi", "paused", 3, 9)
            + Entry(Second, "flat", "active", 40, 0));
    }

    private string Snapshot => Path.Combine(_place, $"{Cluster.Name}-snapshot.toml");

    private string Orders => Path.Combine(_place, "orders");

    public void Dispose()
    {
        Directory.Delete(_place, recursive: true);
    }

    private static string Entry(string node, string name, string state, int picked, int held)
    {
        return $"[[subscriptions]]\nnode = \"{Cluster.Scope}/node/{node}\"\nname = \"{name}\"\n"
            + "application = \"RoundTrip\"\n"
            + $"filter = \"MessageType = '{name}'\"\n"
            + "destination = \"the Send Port 'RoundTripOut'\"\n"
            + $"state = \"{state}\"\npicked_up = {picked}\nheld = {held}\n";
    }

    private static (int Exit, string Out, string Error) Run(
        IOperatorSurface surface, Invocation invocation)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = SubscriptionCommand.Over(
            surface, invocation.Subscriptions ?? new SubscriptionQuery(), invocation.Act,
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
            "subscriptions", $"*/{First}", "--location", Cluster.NodeScope(0), "--name", "edi",
            "--sort", "held", "--order", "descending", "--resume", "--who", "ilian");

        Assert.Equal(Command.Subscriptions, parsed.Command);
        Assert.Equal(
            new SubscriptionQuery
            {
                Pattern = $"*/{First}",
                Location = Cluster.NodeScope(0),
                Name = "edi",
                Sort = "held",
                Order = "descending",
            },
            parsed.Subscriptions);
        Assert.Equal(SubscriptionAct.Resume, parsed.Act);
        Assert.Null(parsed.EventSubscriptions);
        Assert.Equal("ilian", parsed.Who);
    }

    [Theory]
    [InlineData("subscriptions --pause --resume", "one act")]
    [InlineData("subscriptions --name", "--name needs")]
    [InlineData("subscriptions --id 3", "--id only applies to 'event-subscriptions'")]
    [InlineData("event-subscriptions --name edi", "--name only applies to 'subscriptions'")]
    [InlineData("list --pause", "--pause only applies to 'event-subscriptions',")]
    [InlineData("subscriptions --program xmip-cli", "--program only applies to 'audit'")]
    [InlineData("subscriptions --who ilian", "--who only applies")]
    public void ALineThatCannotBeObeyedIsSaidSo(string line, string said)
    {
        Assert.Null(Invocation.Parse(line.Split(' '), out string problem));
        Assert.Contains(said, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveIsRefusedInWordsBecauseTheConfigurationAddsAndRemovesIt()
    {
        Assert.Null(Invocation.Parse(
            ["subscriptions", "--location", Cluster.NodeScope(0), "--name", "edi", "--remove"],
            out string problem));

        Assert.StartsWith("REFUSED", problem, StringComparison.Ordinal);
        Assert.Contains(SubscriptionOperation.Configured, problem, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Orders));
    }

    [Fact]
    public void TheListSaysNameClusterNodeFilterAndWhatIsHeldAsTextAndAsADocument()
    {
        SnapshotOperator surface = new(Snapshot);

        (int exit, string text, _) = Run(surface, Line("subscriptions"));
        Assert.Equal(0, exit);
        Assert.Contains("3 Subscription(s), 1 paused, 9 held", text, StringComparison.Ordinal);
        Assert.Matches(
            $@"edi\s+{Cluster.Name}\s+{First}\s+MessageType = 'edi'\s+the Send Port", text);

        (_, string json, _) = Run(
            surface, Line("subscriptions", "--location", Cluster.NodeScope(0), "--json"));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement[] listed =
            [.. document.RootElement.GetProperty("subscriptions").EnumerateArray()];
        Assert.Equal(2, listed.Length);
        Assert.Equal("edi", listed[0].GetProperty("name").GetString());
        Assert.True(listed[0].GetProperty("paused").GetBoolean());
        Assert.Equal(9ul, listed[0].GetProperty("held").GetUInt64());
        Assert.Equal("RoundTrip", listed[0].GetProperty("application").GetString());
    }

    [Fact]
    public void ThePatternDrillsAndTheSortOrders()
    {
        SnapshotOperator surface = new(Snapshot);

        (_, string named, _) = Run(
            surface, Line("subscriptions", "*/subscription/fl*", "--json"));
        using JsonDocument one = JsonDocument.Parse(named);
        Assert.Equal(
            "flat",
            Assert.Single(one.RootElement.GetProperty("subscriptions").EnumerateArray())
                .GetProperty("name").GetString());

        (_, string sorted, _) = Run(
            surface,
            Line("subscriptions", "--sort", "picked-up", "--order", "descending", "--json"));
        using JsonDocument all = JsonDocument.Parse(sorted);
        Assert.Equal(
            ["flat", "structured", "edi"],
            all.RootElement.GetProperty("subscriptions").EnumerateArray()
                .Select(entry => entry.GetProperty("name").GetString()));

        (int exit, _, string error) = Run(surface, Line("subscriptions", "--sort", "queued"));
        Assert.Equal(2, exit);
        Assert.StartsWith("REFUSED", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AnActNamesOneSubscriptionOrIsRefusedBeforeAnyNodeIsAsked()
    {
        SnapshotOperator surface = new(Snapshot);

        (int exit, _, string error) = Run(surface, Line("subscriptions", "--pause"));
        Assert.Equal(2, exit);
        Assert.StartsWith("REFUSED", error, StringComparison.Ordinal);

        (exit, _, error) = Run(
            surface,
            Line(
                "subscriptions", "--location", Cluster.NodeScope(1), "--name", "edi",
                "--pause"));
        Assert.Equal(1, exit);
        Assert.Contains("no Subscription 'edi'", error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Orders));

        (exit, string said, _) = Run(
            surface,
            Line(
                "subscriptions", "--location", Cluster.NodeScope(0), "--name", "edi",
                "--resume", "--who", "ilian"));
        Assert.Equal(0, exit);
        Assert.StartsWith(
            $"OK. resume of Subscription 'edi' left for {First}", said,
            StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(Path.Combine(Orders, First), "*-resume.toml"));
    }
}
