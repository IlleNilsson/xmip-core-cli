using System.Text.Json;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// What each command does with a wildcard. What an argument selects is
/// <see cref="ScopeSelection"/>'s, shared with the cmdlets and held in
/// <c>Xmip.Surface.Test</c> (ADR-0059 clause 7; ADR-0052 clause 1); what a
/// command says over several scopes, and the refusal as a document, is the
/// executable's own and held here.
/// </summary>
public sealed class WildcardCommandTest
{
    private static FakeSurface Cluster()
    {
        return new FakeSurface(
        [
            FakeSurface.Leaf("xmip:///C1/node/alpha/receive/tcp", HealthState.Fine),
            FakeSurface.Leaf(
                "xmip:///C1/node/alpha/receive/file", HealthState.Stressed, 55, "slow"),
            FakeSurface.Leaf("xmip:///C1/node/alpha2/receive/tcp", HealthState.Fine),
            FakeSurface.Leaf("xmip:///C1/node/beta/process/json", HealthState.Done, 90, "refused"),
            FakeSurface.Leaf("xmip:///C1/node/gamma/send/tcp", HealthState.Fine),
        ]);
    }

    /// <summary>With <c>--json</c> the refusal is a document too, because a
    /// program that is promised structure must not be handed prose.</summary>
    [Fact]
    public void TheRefusalIsADocumentWhenAProgramIsAsking()
    {
        ScopeSelection.Of(Cluster(), "xmip:///C1/node/Q*", out string refusal);

        using JsonDocument document = JsonDocument.Parse(
            ScopeCommand.Unmatched("xmip:///C1/node/Q*", refusal));

        Assert.Equal("xmip:///C1/node/Q*", document.RootElement.GetProperty("pattern").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("matched").GetInt32());
        Assert.StartsWith(
            "REFUSED",
            document.RootElement.GetProperty("refused").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void HealthOverAPatternSaysEachScopeInTurnAndRollsUpNoneOfThemTogether()
    {
        FakeSurface surface = Cluster();
        ScopeSelection chosen = ScopeSelection.Of(surface, "xmip:///C1/node/alpha*", out _)!;
        StringWriter output = new();

        int exit = HealthCommand.Over(surface, chosen, json: false, output, new StringWriter());
        string[] lines = output.ToString().Split(
            Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(0, exit);
        Assert.Equal("holding        xmip:///C1/node/alpha", lines[0]);
        Assert.Contains(lines, line => line == "fine           xmip:///C1/node/alpha2");
    }

    [Fact]
    public void HealthOverAPatternInJsonIsOneDocumentWithOneObjectPerScope()
    {
        FakeSurface surface = Cluster();
        ScopeSelection chosen = ScopeSelection.Of(surface, "xmip:///C1/node/alpha*", out _)!;
        StringWriter output = new();

        HealthCommand.Over(surface, chosen, json: true, output, new StringWriter());

        string text = output.ToString();
        Assert.Equal(1, text.Count(character => character == '\n'));

        using JsonDocument document = JsonDocument.Parse(text);
        JsonElement root = document.RootElement;
        Assert.Equal("xmip:///C1/node/alpha*", root.GetProperty("pattern").GetString());
        Assert.Equal(2, root.GetProperty("matched").GetInt32());

        JsonElement scopes = root.GetProperty("scopes");
        Assert.Equal("xmip:///C1/node/alpha", scopes[0].GetProperty("scope").GetString());
        Assert.Equal("holding", scopes[0].GetProperty("state").GetString());
        Assert.Equal("fine", scopes[1].GetProperty("state").GetString());
    }

    [Fact]
    public void MeasureOverAPatternSaysOneLinePerScopeAndAddsNothingUp()
    {
        FakeSurface surface = Cluster();
        surface.Measurements[Counted.Streams] = 7;
        ScopeSelection chosen = ScopeSelection.Of(surface, "xmip:///C1/node/alpha*", out _)!;
        StringWriter output = new();

        int exit = MeasureCommand.Over(surface, chosen, json: false, output, new StringWriter());
        string[] lines = output.ToString().Split(
            Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(0, exit);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("xmip:///C1/node/alpha  Streams 7", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("xmip:///C1/node/alpha2  Streams 7", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void MeasureOverAPatternInJsonNamesEachScopeAndItsFigures()
    {
        FakeSurface surface = Cluster();
        surface.Measurements[Counted.Streams] = 7;
        ScopeSelection chosen = ScopeSelection.Of(surface, "xmip:///C1/node/alpha*", out _)!;
        StringWriter output = new();

        MeasureCommand.Over(surface, chosen, json: true, output, new StringWriter());

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal(2, root.GetProperty("matched").GetInt32());
        Assert.Equal(
            "xmip:///C1/node/alpha2",
            root.GetProperty("scopes")[1].GetProperty("scope").GetString());
        Assert.Equal(7UL, root.GetProperty("scopes")[0].GetProperty("streams").GetUInt64());
    }

    [Fact]
    public void ListOverAPatternIsOneListOfRowsThatNameTheirOwnScopes()
    {
        FakeSurface surface = Cluster();
        ScopeSelection chosen = ScopeSelection.Of(surface, "xmip:///C1/node/alpha*", out _)!;
        StringWriter output = new();

        int exit = ScopeCommand.ListOver(surface, chosen, json: false, output, new StringWriter());
        string text = output.ToString();

        Assert.Equal(0, exit);
        Assert.Contains("xmip:///C1/node/alpha/receive", text, StringComparison.Ordinal);
        Assert.Contains("xmip:///C1/node/alpha2/receive", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowOverAPatternIsOneRowPerScopeAndInJsonOneItemEach()
    {
        FakeSurface surface = Cluster();
        ScopeSelection chosen = ScopeSelection.Of(surface, "xmip:///C1/node/alpha*", out _)!;
        StringWriter output = new();

        ScopeCommand.ShowOver(surface, chosen, json: false, output, new StringWriter());
        Assert.Contains("xmip:///C1/node/alpha", output.ToString(), StringComparison.Ordinal);

        StringWriter asJson = new();
        ScopeCommand.ShowOver(surface, chosen, json: true, asJson, new StringWriter());

        using JsonDocument document = JsonDocument.Parse(asJson.ToString());
        Assert.Equal(2, document.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(
            "xmip:///C1/node/alpha2",
            document.RootElement.GetProperty("items")[1].GetProperty("scope").GetString());
    }

    /// <summary>
    /// A pattern adds no reach: one act already reaches everything beneath the
    /// scope it names (ADR-0027). What it adds is several subtrees at once, and
    /// the exit says whether every one of them was applied.
    /// </summary>
    [Fact]
    public void PauseOverAPatternActsOnEachAndSaysSoWhenOneWasNotApplied()
    {
        FakeSurface surface = Cluster();
        ScopeSelection chosen = ScopeSelection.Of(surface, "xmip:///C1/node/alpha*", out _)!;
        StringWriter error = new();

        int exit = ScopeCommand.ApplyOver(
            surface, chosen, ScopeAction.Pause, "ilian", json: false, new StringWriter(), error);

        Assert.Equal(1, exit);
        Assert.Equal(
            2,
            error.ToString().Split(
                Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void PauseOverAPatternInJsonIsOneDocumentAndNotOneLinePerScope()
    {
        FakeSurface surface = Cluster();
        ScopeSelection chosen = ScopeSelection.Of(surface, "xmip:///C1/node/alpha*", out _)!;
        StringWriter output = new();

        ScopeCommand.ApplyOver(
            surface, chosen, ScopeAction.Pause, "ilian", json: true, output, new StringWriter());

        string text = output.ToString();
        Assert.Equal(1, text.Count(character => character == '\n'));

        using JsonDocument document = JsonDocument.Parse(text);
        Assert.Equal("pause", document.RootElement.GetProperty("action").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("scopes").GetArrayLength());
        Assert.False(
            document.RootElement.GetProperty("scopes")[0].GetProperty("applied").GetBoolean());
    }

    /// <summary>The help says the rule, because a convention nobody is told
    /// about is a convention nobody uses.</summary>
    [Fact]
    public void TheUsageSaysThatAScopeMayBeAWildcard()
    {
        Assert.Contains("wildcard", Usage.Text, StringComparison.Ordinal);
        Assert.Contains("REFUSED", Usage.Text, StringComparison.Ordinal);
        Assert.Contains("xmip-cli health \"xmip:///orders/node/edge-*\"", Usage.Text,
            StringComparison.Ordinal);
    }
}
