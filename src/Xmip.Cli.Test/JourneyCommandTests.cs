using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip-cli journey</c> (runtime-model.md section 13; ADR-0013): the
/// Journeys that failed listed where no Journey is named, and Retry or
/// Dismiss on one, the act an option and required, the Journey the argument
/// and its node <c>--location</c> — the node, or the Send Port's scope. A
/// line without a node is refused before any node is asked; an act taken
/// through a snapshot is left where its publication says; and no other noun
/// takes a Journey's act, nor a Journey another noun's.
/// </summary>
public sealed class JourneyCommandTests : IDisposable
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's sending node, by what it declares, and a Send Port on it.
    private static readonly string Sender = Cluster.WithRole("sending");
    private static readonly string Sending = $"{Cluster.Scope}/node/{Sender}";
    private static readonly string Port = $"{Sending}/send/invoices";

    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-cli-journey-{Guid.NewGuid():N}");

    public JourneyCommandTests()
    {
        Directory.CreateDirectory(_place);
        File.WriteAllText(
            Snapshot,
            $"node = \"{Cluster.Scope}\"\norders = '{Orders}'\n\n"
                + $"[[failed_journeys]]\nnode = \"{Sending}\"\nsend_port = \"invoices\"\n"
                + "count = 2\n\n[[failed_journeys.journeys]]\njourney = \"j-1\"\n"
                + "sequence = 3\nreason = \"invoices: the far end refused it\"\n\n"
                + "[[failed_journeys.journeys]]\njourney = \"j-2\"\nsequence = 7\n"
                + "reason = \"invoices: the far end refused it again\"\n");
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
        int exit = JourneyCommand.Over(
            surface, invocation.Location, invocation.Argument,
            invocation.JourneyAct ?? throw new InvalidOperationException("no act"),
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
    public void TheJourneyIsTheArgumentItsNodeTheLocationAndTheActAnOption()
    {
        Invocation parsed = Line(
            "journey", "j-1", "--location", Port, "--dismiss", "--who", "ilian");

        Assert.Equal(Command.Journey, parsed.Command);
        Assert.Equal("j-1", parsed.Argument);
        Assert.Equal(Port, parsed.Location);
        Assert.Equal(JourneyAct.Dismiss, parsed.JourneyAct);
        Assert.Equal("ilian", parsed.Who);
        Assert.Null(parsed.DeadMessages);
        Assert.Equal(JourneyAct.Retry, Line("journey", "j-1", "--retry").JourneyAct);
    }

    [Theory]
    [InlineData("journey j-1", "needs an act: --retry or --dismiss")]
    [InlineData("journey --retry", "to retry a Journey, name it")]
    [InlineData("journey j-1 j-2 --retry", "takes at most one argument")]
    [InlineData("journey j-1 --replay", "--replay only applies to 'dead-messages'")]
    [InlineData("journey j-1 --pause", "--retry and --dismiss are its acts")]
    [InlineData("journey j-1 --retry --dismiss", "one act")]
    [InlineData("journey j-1 --retry --sort node", "--sort only applies to 'audit'")]
    [InlineData("journey j-1 --retry --message m-1", "--message only applies to 'dead-messages'")]
    [InlineData("dead-messages --retry", "--retry only applies to 'journey'")]
    [InlineData("subscriptions --dismiss", "--dismiss only applies to 'journey'")]
    [InlineData("event-subscriptions --retry", "--retry only applies to 'journey'")]
    [InlineData("health * --dismiss", "--dismiss only applies to 'journey'")]
    public void ALineThatCannotBeObeyedIsSaidSo(string line, string said)
    {
        Assert.Null(Invocation.Parse(line.Split(' '), out string problem));
        Assert.Contains(said, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void NoJourneyAndNoActListsTheJourneysThatFailedPagedByOffsetAndLimit()
    {
        Invocation parsed = Line(
            "journey", "--location", Port, "--offset", "4", "--limit", "1");

        Assert.Equal(Command.Journey, parsed.Command);
        Assert.Null(parsed.JourneyAct);
        Assert.Equal((4UL, 1U), (parsed.From, parsed.Most));

        SnapshotOperator surface = new(Snapshot);
        using StringWriter output = new();
        int exit = JourneyCommand.List(surface, Port, (0, 0), json: false, output);
        string said = output.ToString();

        Assert.Equal(0, exit);
        Assert.Contains("Send Port invoices: 2 failed in its queue", said, StringComparison.Ordinal);
        Assert.Contains("j-1  place 3  invoices: the far end refused it", said, StringComparison.Ordinal);
        Assert.Contains("j-2", said, StringComparison.Ordinal);

        using StringWriter paged = new();
        JourneyCommand.List(surface, Sending, (parsed.From, parsed.Most), json: true, paged);
        using JsonDocument document = JsonDocument.Parse(paged.ToString());
        JsonElement port = document.RootElement.GetProperty("failed_journeys")[0];
        Assert.Equal("invoices", port.GetProperty("send_port").GetString());
        Assert.Equal(2UL, port.GetProperty("count").GetUInt64());
        JsonElement journeys = port.GetProperty("journeys");
        Assert.Equal(1, journeys.GetArrayLength());
        Assert.Equal("j-2", journeys[0].GetProperty("journey").GetString());
    }

    [Fact]
    public void AnActWithoutItsNodeIsRefusedBeforeAnyNodeIsAsked()
    {
        SnapshotOperator surface = new(Snapshot);

        (int exit, _, string error) = Run(surface, Line("journey", "j-1", "--retry"));
        Assert.Equal(2, exit);
        Assert.StartsWith("REFUSED", error, StringComparison.Ordinal);

        (exit, _, error) = Run(
            surface, Line("journey", "j-1", "--location", Cluster.Scope, "--dismiss"));
        Assert.Equal(2, exit);
        Assert.StartsWith("REFUSED", error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Orders));
    }

    [Fact]
    public void AnActIsLeftWhereThePublicationSaysAsTextAndAsADocument()
    {
        SnapshotOperator surface = new(Snapshot);

        (int exit, string said, _) = Run(
            surface, Line("journey", "j-1", "--location", Port, "--retry", "--who", "ilian"));
        Assert.Equal(0, exit);
        Assert.StartsWith(
            $"OK. retry of the Journey j-1 left for {Sender}", said, StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(Path.Combine(Orders, Sender), "*-retry.toml"));

        (exit, string json, _) = Run(
            surface, Line("journey", "j-1", "--location", Sending, "--dismiss", "--json"));
        Assert.Equal(0, exit);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(Sending, document.RootElement.GetProperty("node").GetString());
        Assert.Equal("j-1", document.RootElement.GetProperty("journey").GetString());
        Assert.Equal("dismiss", document.RootElement.GetProperty("act").GetString());
        Assert.True(document.RootElement.GetProperty("applied").GetBoolean());
    }
}
