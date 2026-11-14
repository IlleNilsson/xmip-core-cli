using System.Text.Json;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>--local</c> at a node that gathers its cluster's view — a node
/// declaring the monitoring role — answers for the whole cluster, and
/// <c>health</c> says so after where the node is reached, in the node's own
/// words; at a node that gathers none it answers for that node (the design
/// approved 2026-11-12, section 3.5). Which of the two is the node's answer,
/// never its name: both are served under random names.
/// </summary>
public sealed class GatheredClusterTests
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    private static readonly string Words =
        $"{Cluster.Name}, gathered by a monitoring node: 1 of 2 nodes reached";

    [Fact]
    public void HealthAtAGatheringNodeAnswersForTheClusterAndSaysWhoGathered()
    {
        using ServedNode served = Serve(gathers: true);
        IOperatorSurface surface = Opened("--local", served.Address);

        try
        {
            StringWriter text = new();
            Assert.Equal(
                0,
                HealthCommand.Run(surface, Cluster.Scope, json: false, text, new StringWriter()));

            string said = text.ToString();
            Assert.Contains(
                $"source LOCAL — {served.Address} — {Words}", said, StringComparison.Ordinal);
            Assert.Contains($"{Cluster.NodeScope(0)}/reached", said, StringComparison.Ordinal);
            Assert.Contains($"{Cluster.NodeScope(1)}/reached", said, StringComparison.Ordinal);

            StringWriter json = new();
            Assert.Equal(
                0, HealthCommand.Run(surface, Cluster.Scope, json: true, json, new StringWriter()));

            using JsonDocument document = JsonDocument.Parse(json.ToString());
            Assert.Equal(
                $"LOCAL — {served.Address} — {Words}",
                document.RootElement.GetProperty("source").GetString());
        }
        finally
        {
            (surface as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public void HealthAtANodeGatheringNoneAnswersForThatNode()
    {
        using ServedNode served = Serve(gathers: false);
        IOperatorSurface surface = Opened("--local", served.Address);

        try
        {
            StringWriter text = new();
            Assert.Equal(
                0,
                HealthCommand.Run(
                    surface, Cluster.NodeScope(0), json: false, text, new StringWriter()));

            string said = text.ToString();
            Assert.Contains($"source LOCAL — {served.Address}{Environment.NewLine}", said,
                StringComparison.Ordinal);
            Assert.DoesNotContain(Cluster.NodeScope(1), said, StringComparison.Ordinal);
        }
        finally
        {
            (surface as IDisposable)?.Dispose();
        }
    }

    // The surface the executable opens for a health line stating these.
    private static IOperatorSurface Opened(params string[] stated)
    {
        Invocation invocation = Invocation.Parse(
            ["health", Cluster.Scope, .. stated], out string problem)
            ?? throw new InvalidOperationException(problem);

        return SurfaceOpen.Open(invocation, out string reason)
            ?? throw new InvalidOperationException(reason);
    }

    // A local listener this account observes, posting one node's own
    // publication and, where it gathers, the cluster's view: one member
    // reached, the other not.
    private static ServedNode Serve(bool gathers)
    {
        using HeldCaller me = RuntimeLibrary.Rules.SecurityRoles.Process(out string refused)
            ?? throw new InvalidOperationException(refused);
        ServedNode served = RuntimeLibrary.Rules.Remotes.ServeLocal(
            $"xmip-gathered-cli-test-{Guid.NewGuid():n}", [me.Name], [], [], "gathered-cli-test",
            out string reason) ?? throw new InvalidOperationException(reason);

        served.Post(Publication("own", Record($"{Cluster.NodeScope(0)}/receive/orders", "fine")));

        if (gathers)
        {
            served.PostCluster(Publication(
                Words,
                Record($"{Cluster.NodeScope(0)}/reached", "fine"),
                Record($"{Cluster.NodeScope(1)}/reached", "exhausted")));
        }

        return served;
    }

    private static string Publication(string source, params string[] records)
    {
        return $"source = \"{source}\"\nnode = \"{Cluster.Scope}\"\n\n"
            + string.Join('\n', records);
    }

    private static string Record(string scope, string state)
    {
        return $"[[records]]\nscope = \"{scope}\"\nstate = \"{state}\"\nseverity = 0\n"
            + "evidence = \"\"\nobserved_unix_nanos = 1789111688000000000\n";
    }
}
