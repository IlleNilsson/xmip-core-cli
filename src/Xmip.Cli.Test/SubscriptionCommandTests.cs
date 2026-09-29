using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip-cli subscriptions</c> (ADR-0065, amendment 2026-09-29): one
/// command for the noun, the act an option on it. The line becomes the one
/// query every surface asks; what a snapshot of cluster CT lists is
/// rendered; an act names one subscription or is refused before any node is
/// asked; and one taken is left where the publication says.
/// </summary>
public sealed class SubscriptionCommandTests : IDisposable
{
    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-cli-subscriptions-{Guid.NewGuid():N}");

    public SubscriptionCommandTests()
    {
        Directory.CreateDirectory(_place);
        File.WriteAllText(
            Snapshot,
            "node = \"xmip:///CT\"\n"
            + $"orders = '{Orders}'\n"
            + "[[subscriptions]]\nnode = \"xmip:///CT/node/R1\"\nid = 1\n"
            + "subscriber = \"operations\"\nparty = \"0199a0a0-0000-7000-8000-000000000001\"\n"
            + "action = \"every Event\"\nstate = \"active\"\nqueued = 2\ncapacity = 64\n"
            + "[[subscriptions]]\nnode = \"xmip:///CT/node/S1\"\nid = 2\n"
            + "subscriber = \"on-call\"\nparty = \"0199a0a0-0000-7000-8000-000000000002\"\n"
            + "action = \"every Event ending failure\"\nstate = \"paused\"\nqueued = 7\n");
    }

    private string Snapshot => Path.Combine(_place, "CT-snapshot.toml");

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
            "subscriptions", "*/R1", "--location", "xmip:///CT/node/R1", "--id", "3",
            "--sort", "queued", "--order", "descending", "--pause", "--who", "ilian");

        Assert.Equal(Command.Subscriptions, parsed.Command);
        Assert.Equal(
            new SubscriptionQuery
            {
                Pattern = "*/R1",
                Location = "xmip:///CT/node/R1",
                Id = 3,
                Sort = "queued",
                Order = "descending",
            },
            parsed.Subscriptions);
        Assert.Equal(SubscriptionAct.Pause, parsed.Act);
        Assert.Equal("ilian", parsed.Who);
    }

    [Theory]
    [InlineData("subscriptions --pause --remove", "one act")]
    [InlineData("subscriptions --id x", "--id needs")]
    [InlineData("subscriptions --severity error", "--severity only applies to 'audit'")]
    [InlineData("list --id 3", "--id only applies to 'subscriptions'")]
    [InlineData("subscriptions --who ilian", "--who only applies")]
    public void ALineThatCannotBeObeyedIsSaidSo(string line, string said)
    {
        Assert.Null(Invocation.Parse(line.Split(' '), out string problem));
        Assert.Contains(said, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void TheListSaysSubscriberClusterNodeAndActionAsTextAndAsADocument()
    {
        SnapshotOperator surface = new(Snapshot);

        (int exit, string text, _) = Run(surface, Line("subscriptions"));
        Assert.Equal(0, exit);
        Assert.Contains("2 subscription(s), 1 paused", text, StringComparison.Ordinal);
        Assert.Matches(@"1\s+operations\s+CT\s+R1\s+every Event\s+active", text);
        Assert.DoesNotContain("0199a0a0", text, StringComparison.Ordinal);

        (_, string json, _) = Run(
            surface, Line("subscriptions", "--location", "xmip:///CT/node/S1", "--json"));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement one = Assert.Single(
            document.RootElement.GetProperty("subscriptions").EnumerateArray());
        Assert.Equal("S1", one.GetProperty("node").GetString()![^2..]);
        Assert.True(one.GetProperty("paused").GetBoolean());
        Assert.Equal("on-call", one.GetProperty("subscriber").GetString());
        Assert.Equal(
            "0199a0a0-0000-7000-8000-000000000002", one.GetProperty("party").GetString());
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
            Line("subscriptions", "--location", "xmip:///CT/node/R1", "--id", "9", "--pause"));
        Assert.Equal(1, exit);
        Assert.Contains("no subscription 9", error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Orders));

        (exit, string said, _) = Run(
            surface,
            Line("subscriptions", "--location", "xmip:///CT/node/S1", "--id", "2", "--resume"));
        Assert.Equal(0, exit);
        Assert.StartsWith(
            "OK. resume of subscription 2 left for S1", said, StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(Path.Combine(Orders, "S1"), "*-2-resume.toml"));
    }
}
