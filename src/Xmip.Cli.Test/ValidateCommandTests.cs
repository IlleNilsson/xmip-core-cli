using System.Text.Json;
using Xmip.Abi.Module;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip validate</c> over a verdict the test wrote: the shared sentence for
/// a person, the record for a program, and exit 1 for a document the runtime
/// refused — decided from the record, never from the sentence.
/// </summary>
public sealed class ValidateCommandTests
{
    private const string Toml = "node.toml";

    [Fact]
    public void AValidDocumentIsSaidSoOnStdout()
    {
        StringWriter output = new();

        int exit = ValidateCommand.Run(
            ConfigurationVerdict.Validated(Toml, new ValidationRecord(XmipStatus.Ok, [])),
            json: false, output, new StringWriter());

        Assert.Equal(0, exit);
        Assert.Equal($"{Toml} is valid{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public void AnInvalidDocumentIsSaidSoOnStderrWithItsProblems()
    {
        StringWriter error = new();

        int exit = ValidateCommand.Run(
            ConfigurationVerdict.Validated(
                Toml, new ValidationRecord(XmipStatus.Invalid, ["no name", "no receive location"])),
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

        ValidationRecord answer = new(XmipStatus.Invalid, ["no name"]);

        int exit = ValidateCommand.Run(
            ConfigurationVerdict.Validated(Toml, answer), json: true, output, new StringWriter());

        Assert.Equal(1, exit);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("valid").GetBoolean());
        Assert.Equal("Invalid", root.GetProperty("status").GetString());
        Assert.Equal("no name", root.GetProperty("problems")[0].GetString());
    }

    [Fact]
    public void TheExitIsTheRecordsNotTheSentences()
    {
        // A verdict whose sentence happens to contain "is valid" but whose
        // status says otherwise exits 1: ADR-0052 clause 4.
        ConfigurationVerdict misleading = new(
            Toml, XmipStatus.Invalid, [], $"{Toml} is valid, said nobody");

        int exit = ValidateCommand.Run(
            misleading, json: false, new StringWriter(), new StringWriter());

        Assert.Equal(1, exit);
    }
}
