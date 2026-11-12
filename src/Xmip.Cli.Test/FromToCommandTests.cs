using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// From and to on every observation the command line says (the owner,
/// 2026-11-10; ADR-0052, amendment 2026-11-11): <c>xmip health</c> and
/// <c>xmip list</c> say where the work at each scope came from and went, in
/// the runtime's words, in text and in a document; <c>xmip subscriptions</c>
/// the Send Ports each Subscription reaches.
/// </summary>
public sealed class FromToCommandTests : IDisposable
{
    private static readonly TestCluster Cluster = TestCluster.Read();
    private static readonly string Processor = Cluster.Nodes[0];
    private static readonly string Node = $"{Cluster.Scope}/node/{Processor}";

    private readonly string _snapshot =
        Path.Combine(Path.GetTempPath(), $"xmip-from-to-{Guid.NewGuid():N}.xmip");

    public FromToCommandTests()
    {
        File.WriteAllText(_snapshot, $$"""
            node = "{{Cluster.Scope}}"

            [[records]]
            scope = "{{Node}}/receive/In"
            state = "fine"

            [[records]]
            scope = "{{Node}}/receive/In/delivery"
            state = "stressed"
            severity = 60
            evidence = "a document no Subscription takes was dropped"

            [[subscriptions]]
            node = "{{Node}}"
            name = "onward"
            send_ports = ["Out"]

            [topology]
            observed_unix_nanos = 5

            [[topology.nodes]]
            id = "node/{{Processor}}/receive/In"
            kind = "endpoint"
            scope = "{{Node}}/receive/In"

            [[topology.links]]
            id = "in"
            from = "node/{{Processor}}/receive/In"
            to = "node/{{Processor}}/process/onward"
            """);
    }

    private static string Said => $"from In [{Processor}] → to Out";

    public void Dispose()
    {
        File.Delete(_snapshot);
    }

    [Fact]
    public void HealthSaysFromAndToOnEveryRecordAsTextAndAsADocument()
    {
        SnapshotOperator surface = new(_snapshot);
        StringWriter text = new();

        HealthCommand.Run(surface, $"{Node}/receive", json: false, text, new StringWriter());
        Assert.Contains(Said, text.ToString(), StringComparison.Ordinal);

        StringWriter json = new();
        HealthCommand.Run(surface, $"{Node}/receive", json: true, json, new StringWriter());
        using JsonDocument document = JsonDocument.Parse(json.ToString());
        JsonElement record = document.RootElement.GetProperty("records")[0];
        Assert.Equal($"In [{Processor}]", record.GetProperty("from").GetString());
        Assert.Equal("Out", record.GetProperty("to").GetString());
    }

    [Fact]
    public void AListedRowSaysFromAndTo()
    {
        SnapshotOperator surface = new(_snapshot);
        StringWriter text = new();

        ScopeCommand.List(surface, $"{Node}/receive", json: false, text, new StringWriter());

        Assert.Contains(Said, text.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ASubscriptionSaysTheSendPortsItReaches()
    {
        SnapshotOperator surface = new(_snapshot);
        string json = SubscriptionCommand.Document(
            surface.Source, surface.Subscriptions().Subscriptions);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement listed = document.RootElement.GetProperty("subscriptions")[0];
        Assert.Equal(
            ["Out"],
            listed.GetProperty("send_ports").EnumerateArray().Select(port => port.GetString()));
    }
}
