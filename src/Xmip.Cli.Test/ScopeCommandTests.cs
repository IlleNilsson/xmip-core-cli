using Xmip.Abi.Operate;

namespace Xmip.Cli.Test;

public sealed class ScopeCommandTests
{
    [Fact]
    public void ListUsesTheSharedScopeTreeAndActivityShape()
    {
        FakeSurface surface = new(
        [
            FakeSurface.Leaf("xmip:///edge-01/receive/orders", HealthState.Fine),
            FakeSurface.Leaf("xmip:///edge-02/send/billing", HealthState.Working),
        ]);
        StringWriter output = new();

        int exit = ScopeCommand.List(
            surface, "xmip:///", false, output, TextWriter.Null);

        Assert.Equal(0, exit);
        Assert.Contains("xmip:///edge-01", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("xmip:///edge-02", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Retry –", output.ToString(), StringComparison.Ordinal);
    }
}
