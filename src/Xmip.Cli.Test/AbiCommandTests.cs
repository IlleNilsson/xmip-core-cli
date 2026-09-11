using System.Text.Json;
using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip abi</c> reports both boundaries from the binding's constants, so
/// what it prints and what the binding speaks cannot differ.
/// </summary>
public sealed class AbiCommandTests
{
    [Fact]
    public void ListsBothBoundariesAsAlignedText()
    {
        StringWriter output = new();

        int exit = AbiCommand.Run(json: false, output);

        Assert.Equal(0, exit);
        string[] lines = output.ToString().Split(Environment.NewLine);
        Assert.Equal($"module version      {ModuleAbi.AbiVersion}", lines[0]);
        Assert.Equal($"module entrypoint   {ModuleAbi.Entrypoint}", lines[1]);
        Assert.Equal($"operate version     {OperateAbi.Version}", lines[3]);
        Assert.Equal($"operate entrypoint  {OperateAbi.Entrypoint}", lines[4]);
    }

    [Fact]
    public void ListsBothBoundariesAsOneDocument()
    {
        StringWriter output = new();

        int exit = AbiCommand.Run(json: true, output);

        Assert.Equal(0, exit);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal(
            ModuleAbi.Entrypoint,
            root.GetProperty("module").GetProperty("entrypoint").GetString());
        Assert.Equal(
            OperateAbi.Version,
            root.GetProperty("operate").GetProperty("version").GetUInt32());
    }
}
