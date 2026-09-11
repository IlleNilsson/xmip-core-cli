using System.Text.Json;
using Xmip.Abi.Operate;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip health</c> over a fake surface: worst first, Holding says why
/// beside the word, one document with <c>--json</c>, and JSON Lines only
/// when something changed with <c>--follow</c>.
/// </summary>
public sealed class HealthCommandTests
{
    private static FakeSurface Estate()
    {
        return new FakeSurface(
        [
            FakeSurface.Leaf("xmip:///edge-01/receive/orders", HealthState.Fine),
            FakeSurface.Leaf(
                "xmip:///edge-01/send/invoices", HealthState.Done, 90, "certificate expired"),
            FakeSurface.Leaf("xmip:///edge-02/receive/orders", HealthState.Working, 30),
        ]);
    }

    [Fact]
    public void TextSaysWhyAHoldingScopeIsHolding()
    {
        StringWriter output = new();

        int exit = HealthCommand.Run(Estate(), "xmip:///", json: false, output, new StringWriter());

        Assert.Equal(0, exit);
        string[] lines = output.ToString().Split(Environment.NewLine);
        Assert.Equal("Holding        xmip:///", lines[0]);
        Assert.Equal("               Done at xmip:///edge-01/send/invoices", lines[1]);
        Assert.Equal("               certificate expired", lines[2]);
    }

    [Fact]
    public void TextListsLeavesWorstFirstInAlignedColumns()
    {
        StringWriter output = new();

        HealthCommand.Run(Estate(), "xmip:///", json: false, output, new StringWriter());

        string[] lines = output.ToString().Split(Environment.NewLine);
        int first = Array.IndexOf(lines, string.Empty) + 1;
        Assert.Equal("Done       90  xmip:///edge-01/send/invoices", lines[first]);
        Assert.Equal("               certificate expired", lines[first + 1]);
        Assert.Equal(
            $"               observed {FakeSurface.Seen:O}", lines[first + 2]);
        Assert.Equal("Working    30  xmip:///edge-02/receive/orders", lines[first + 3]);
    }

    [Fact]
    public void AFineScopeSaysNothingMore()
    {
        StringWriter output = new();

        HealthCommand.Run(
            Estate(), "xmip:///edge-01/receive", json: false, output, new StringWriter());

        string[] lines = output.ToString().Split(Environment.NewLine);
        Assert.Equal("Fine           xmip:///edge-01/receive", lines[0]);
        Assert.StartsWith("               source ", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void JsonIsOneDocumentWithTheRollupTheWorstAndEveryLeaf()
    {
        StringWriter output = new();

        int exit = HealthCommand.Run(Estate(), "xmip:///", json: true, output, new StringWriter());

        Assert.Equal(0, exit);
        string text = output.ToString();
        Assert.Equal(1, text.Count(character => character == '\n'));
        using JsonDocument document = JsonDocument.Parse(text);
        JsonElement root = document.RootElement;
        Assert.Equal("holding", root.GetProperty("state").GetString());
        Assert.Equal(
            "certificate expired",
            root.GetProperty("worst").GetProperty("evidence").GetString());
        Assert.Equal(3, root.GetProperty("records").GetArrayLength());
        Assert.Equal("done", root.GetProperty("records")[0].GetProperty("state").GetString());
    }

    [Fact]
    public void NothingBeneathAScopeIsAComplaintOnStderr()
    {
        StringWriter error = new();

        int exit = HealthCommand.Run(
            Estate(), "xmip:///edge-09", json: false, new StringWriter(), error);

        Assert.Equal(1, exit);
        Assert.Contains("xmip:///edge-09", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FollowEmitsALineOnlyWhenHealthChanges()
    {
        using CancellationTokenSource stop = new();
        FakeSurface surface = Estate();

        // Five reads: the first emits, the second and third repeat and emit
        // nothing, the fourth sees the change, the fifth stops the follow.
        surface = new FakeSurface(surface.Records)
        {
            OnRead = reads =>
            {
                if (reads == 4)
                {
                    surface!.Records =
                    [
                        FakeSurface.Leaf("xmip:///edge-01/receive/orders", HealthState.Fine),
                    ];
                }

                if (reads == 5)
                {
                    stop.Cancel();
                }
            },
        };

        StringWriter output = new();

        int exit = await HealthCommand.FollowAsync(
            surface, "xmip:///", output, TimeSpan.Zero, stop.Token);

        Assert.Equal(0, exit);
        string[] lines = output.ToString().Split(
            Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"state\":\"holding\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"state\":\"fine\"", lines[1], StringComparison.Ordinal);
    }
}
