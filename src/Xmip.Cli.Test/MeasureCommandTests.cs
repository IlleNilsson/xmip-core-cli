using Xmip.Abi.Operate;

namespace Xmip.Cli.Test;

public sealed class MeasureCommandTests
{
    [Fact]
    public void TextSaysTheSixFiguresInOrderAndAnUnpublishedOneAsADash()
    {
        FakeSurface surface = new([]);
        surface.Measurements[Counted.Streams] = 12;
        surface.Measurements[Counted.Messages] = 9;
        surface.Measurements[Counted.Journeys] = 10;
        surface.Measurements[Counted.Retrying] = 1;
        StringWriter output = new();

        int exit = MeasureCommand.Run(surface, "xmip:///", false, output, TextWriter.Null);

        Assert.Equal(0, exit);
        Assert.Equal(
            "Streams 12  Messages 9  Journeys 10  Bytes –  Retrying 1  Failed –",
            output.ToString().Trim());
    }

    [Fact]
    public void JsonKeepsAnUnpublishedFigureNull()
    {
        FakeSurface surface = new([]);
        surface.Measurements[Counted.Failed] = 3;
        StringWriter output = new();

        _ = MeasureCommand.Run(surface, "xmip:///", true, output, TextWriter.Null);

        Assert.Contains("\"streams\":null", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"failed\":3", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NothingPublishedIsAComplaintAndExitOne()
    {
        FakeSurface surface = new([]);
        StringWriter error = new();

        int exit = MeasureCommand.Run(surface, "xmip:///", false, TextWriter.Null, error);

        Assert.Equal(1, exit);
        Assert.Contains("Nothing measured", error.ToString(), StringComparison.Ordinal);
    }
}
