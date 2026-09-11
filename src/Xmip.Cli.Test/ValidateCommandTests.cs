using System.Text.Json;
using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip validate</c> over a fake runtime answer: the shared sentence for a
/// person, the record for a program, and exit 1 for a document the runtime
/// refused.
/// </summary>
public sealed class ValidateCommandTests
{
    private const string Toml = "node.toml";

    [Fact]
    public void AValidDocumentIsSaidSoOnStdout()
    {
        StringWriter output = new();

        int exit = ValidateCommand.Run(
            Toml, "[node]", _ => new ValidationRecord(XmipStatus.Ok, []),
            json: false, output, new StringWriter());

        Assert.Equal(0, exit);
        Assert.Equal($"{Toml} is valid{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public void AnInvalidDocumentIsSaidSoOnStderrWithItsProblems()
    {
        StringWriter error = new();

        int exit = ValidateCommand.Run(
            Toml,
            "[node]",
            _ => new ValidationRecord(XmipStatus.Invalid, ["no name", "no receive location"]),
            json: false,
            new StringWriter(),
            error);

        Assert.Equal(1, exit);
        Assert.Equal(
            $"{Toml} is invalid: no name; no receive location{Environment.NewLine}",
            error.ToString());
    }

    [Fact]
    public void JsonCarriesTheRecord()
    {
        StringWriter output = new();

        int exit = ValidateCommand.Run(
            Toml,
            "[node]",
            _ => new ValidationRecord(XmipStatus.Invalid, ["no name"]),
            json: true,
            output,
            new StringWriter());

        Assert.Equal(1, exit);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("valid").GetBoolean());
        Assert.Equal("Invalid", root.GetProperty("status").GetString());
        Assert.Equal("no name", root.GetProperty("problems")[0].GetString());
    }

    [Fact]
    public void TheTextIsWhatWasHanded()
    {
        string? crossed = null;

        ValidateCommand.Run(
            Toml,
            "[node]\nname = \"edge-01\"",
            text =>
            {
                crossed = text;
                return new ValidationRecord(XmipStatus.Ok, []);
            },
            json: true,
            new StringWriter(),
            new StringWriter());

        Assert.Equal("[node]\nname = \"edge-01\"", crossed);
    }
}
