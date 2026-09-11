using System.Text.Json;
using Xmip.Abi.Module;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip status</c> says what the binding says, once as text and once as a
/// document, and exits 1 for a code the header does not define.
/// </summary>
public sealed class StatusCommandTests
{
    [Fact]
    public void ExplainsAKnownCodeAsText()
    {
        StringWriter output = new();

        int exit = StatusCommand.Run("-21", json: false, output, new StringWriter());

        Assert.Equal(0, exit);
        string[] lines = output.ToString().Split(Environment.NewLine);
        Assert.Equal("  -21  Timeout", lines[0]);
        Assert.Equal($"       {XmipStatus.Timeout.Explain()}", lines[1]);
        Assert.Equal("       retryable: yes   terminal: no", lines[2]);
    }

    [Fact]
    public void ExplainsAKnownCodeAsJson()
    {
        StringWriter output = new();

        int exit = StatusCommand.Run("-41", json: true, output, new StringWriter());

        Assert.Equal(0, exit);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal(-41, root.GetProperty("code").GetInt32());
        Assert.Equal("Panic", root.GetProperty("name").GetString());
        Assert.True(root.GetProperty("known").GetBoolean());
        Assert.True(root.GetProperty("terminal").GetBoolean());
        Assert.False(root.GetProperty("retryable").GetBoolean());
    }

    [Fact]
    public void AnUnknownCodeIsSaidSoAndExitsOne()
    {
        StringWriter output = new();

        int exit = StatusCommand.Run("-99", json: true, output, new StringWriter());

        Assert.Equal(1, exit);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.False(document.RootElement.GetProperty("known").GetBoolean());
        Assert.Equal("unknown", document.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void ANonNumberIsAUsageErrorOnStderr()
    {
        StringWriter error = new();

        int exit = StatusCommand.Run("ok", json: false, new StringWriter(), error);

        Assert.Equal(2, exit);
        Assert.Contains("'ok' is not a number", error.ToString(), StringComparison.Ordinal);
    }
}
