using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli.Test;

public sealed class ScopeCommandTests
{
    [Fact]
    public void ListShowsOneRowPerChildWithItsRolledMoodAndFigures()
    {
        FakeSurface surface = new(
        [
            FakeSurface.Leaf("xmip:///edge-01/receive/orders", HealthState.Fine),
            FakeSurface.Leaf("xmip:///edge-02/send/billing", HealthState.Working),
        ]);
        StringWriter output = new();

        int exit = ScopeCommand.List(surface, "xmip:///", false, output, TextWriter.Null);

        Assert.Equal(0, exit);
        Assert.Contains("fine       xmip:///edge-01", output.ToString(), StringComparison.Ordinal);
        // A node is a container: a Working leaf beneath it rolls up as Holding
        // (ADR-0041), and the row says so.
        Assert.Contains("holding    xmip:///edge-02", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Retrying –", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ShowSaysTheEvidenceBeneathTheRow()
    {
        FakeSurface surface = new(
        [
            FakeSurface.Leaf(
                "xmip:///edge-01/receive/orders", HealthState.Holding, 70, "disk full"),
        ]);
        StringWriter output = new();

        int exit = ScopeCommand.Show(
            surface, "xmip:///edge-01/receive/orders", false, output, TextWriter.Null);

        Assert.Equal(0, exit);
        Assert.Contains(
            "holding    xmip:///edge-01/receive/orders",
            output.ToString(),
            StringComparison.Ordinal);
        Assert.Contains("  disk full", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusedPauseGoesToStderrWithExitOne()
    {
        FakeSurface surface = new([]);
        StringWriter error = new();

        int exit = ScopeCommand.Apply(
            surface, "xmip:///edge-01", ScopeAction.Pause, "test",
            false, TextWriter.Null, error);

        Assert.Equal(1, exit);
        Assert.Contains("a fake cannot be paused", error.ToString(), StringComparison.Ordinal);
    }
}
