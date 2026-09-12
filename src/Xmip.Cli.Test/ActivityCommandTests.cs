using Xmip.Abi.Operate;

namespace Xmip.Cli.Test;

public sealed class ActivityCommandTests
{
    [Fact]
    public void TextUsesTheFiveXmipFiguresInOrderAndShowsMissingAsADash()
    {
        FakeSurface surface = new([]);
        surface.Measurements[Counted.Streams] = 12;
        surface.Measurements[Counted.Journeys] = 10;
        surface.Measurements[Counted.Messages] = 9;
        surface.Measurements[Counted.Retrying] = 1;

        StringWriter output = new();

        int exit = ActivityCommand.Run(
            surface, "xmip:///", false, output, TextWriter.Null);

        Assert.Equal(0, exit);
        Assert.Equal(
            "Received 12  Processed 10  Sent 9  Retrying 1  Failed –",
            output.ToString().Trim());
    }

    [Fact]
    public void JsonKeepsAbsentMeasurementsNull()
    {
        FakeSurface surface = new([]);
        surface.Measurements[Counted.Failed] = 3;
        StringWriter output = new();

        _ = ActivityCommand.Run(surface, "xmip:///", true, output, TextWriter.Null);

        Assert.Contains("\"received\":null", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"failed\":3", output.ToString(), StringComparison.Ordinal);
    }
}
