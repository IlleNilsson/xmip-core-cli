namespace Xmip.Cli.Test;

/// <summary>
/// <see cref="Invocation.Parse"/>: the first bare word is the command, the
/// second its argument, and the options sit anywhere. A line that cannot be
/// obeyed answers null with the reason, never a guess.
/// </summary>
public sealed class InvocationTests
{
    [Fact]
    public void NoArgumentsIsHelp()
    {
        Invocation? parsed = Invocation.Parse([], out string problem);

        Assert.NotNull(parsed);
        Assert.Equal(Command.Help, parsed.Command);
        Assert.Empty(problem);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void EachSpellingOfHelpIsHelp(string word)
    {
        Invocation? parsed = Invocation.Parse([word], out _);

        Assert.NotNull(parsed);
        Assert.Equal(Command.Help, parsed.Command);
    }

    [Fact]
    public void ACommandWithItsArgumentParses()
    {
        Invocation? parsed = Invocation.Parse(["health", "xmip:///"], out _);

        Assert.NotNull(parsed);
        Assert.Equal(Command.Health, parsed.Command);
        Assert.Equal("xmip:///", parsed.Argument);
        Assert.False(parsed.Json);
        Assert.False(parsed.Follow);
        Assert.Null(parsed.Runtime);
    }

    [Fact]
    public void OptionsSitAnywhere()
    {
        Invocation? parsed = Invocation.Parse(
            ["--json", "validate", "--runtime", "lib.dll", "node.toml"], out _);

        Assert.NotNull(parsed);
        Assert.Equal(Command.Validate, parsed.Command);
        Assert.Equal("node.toml", parsed.Argument);
        Assert.True(parsed.Json);
        Assert.Equal("lib.dll", parsed.Runtime);
    }

    [Fact]
    public void FollowImpliesJson()
    {
        Invocation? parsed = Invocation.Parse(["health", "xmip:///", "--follow"], out _);

        Assert.NotNull(parsed);
        Assert.True(parsed.Follow);
        Assert.True(parsed.Json);
    }

    [Fact]
    public void FollowAppliesToLiveCommandsOnly()
    {
        Invocation? parsed = Invocation.Parse(["abi", "--follow"], out string problem);

        Assert.Null(parsed);
        Assert.Contains("--follow", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void MeasureDefaultsToTheClusterAndMayFollow()
    {
        Invocation? parsed = Invocation.Parse(["measure", "--follow"], out _);

        Assert.NotNull(parsed);
        Assert.Equal(Command.Measure, parsed.Command);
        Assert.Empty(parsed.Argument);
        Assert.True(parsed.Follow);
    }

    [Fact]
    public void MeasureAcceptsOneScope()
    {
        Invocation? parsed = Invocation.Parse(["measure", "xmip:///edge-01"], out _);

        Assert.NotNull(parsed);
        Assert.Equal("xmip:///edge-01", parsed.Argument);
    }

    [Fact]
    public void ListDefaultsToTheCluster()
    {
        Invocation? parsed = Invocation.Parse(["list"], out _);

        Assert.NotNull(parsed);
        Assert.Equal(Command.List, parsed.Command);
        Assert.Empty(parsed.Argument);
    }

    [Fact]
    public void RemoteNamesAWebHost()
    {
        Invocation? parsed = Invocation.Parse(
            ["health", "xmip:///", "--remote", "http://host:5087", "--follow"], out _);

        Assert.NotNull(parsed);
        Assert.Equal("http://host:5087", parsed.Remote);
        Assert.Null(parsed.Runtime);
        Assert.True(parsed.Follow);
    }

    [Fact]
    public void RemoteNeedsAnAbsoluteUrl()
    {
        Assert.Null(Invocation.Parse(["health", "xmip:///", "--remote"], out string bare));
        Assert.Contains("--remote", bare, StringComparison.Ordinal);

        Assert.Null(Invocation.Parse(
            ["health", "xmip:///", "--remote", "host:5087"], out string relative));
        Assert.Contains("http://host:5087", relative, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeNeedsAPath()
    {
        Invocation? parsed = Invocation.Parse(
            ["health", "xmip:///", "--runtime"], out string problem);

        Assert.Null(parsed);
        Assert.Contains("--runtime", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ANegativeStatusCodeIsAnArgumentNotAnOption()
    {
        // Every status but Ok is negative; a parser that read '-22' as an
        // option would refuse the one argument 'status' exists to take.
        Invocation? parsed = Invocation.Parse(["status", "-22"], out _);

        Assert.NotNull(parsed);
        Assert.Equal(Command.Status, parsed.Command);
        Assert.Equal("-22", parsed.Argument);
    }

    [Fact]
    public void AnUnknownCommandIsRefusedByName()
    {
        Invocation? parsed = Invocation.Parse(["frobnicate"], out string problem);

        Assert.Null(parsed);
        Assert.Contains("'frobnicate'", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownOptionIsRefusedByName()
    {
        Invocation? parsed = Invocation.Parse(["abi", "--verbose"], out string problem);

        Assert.Null(parsed);
        Assert.Contains("'--verbose'", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("probe")]
    [InlineData("health")]
    [InlineData("validate")]
    [InlineData("show")]
    [InlineData("pause")]
    [InlineData("resume")]
    public void ACommandThatTakesAnArgumentRefusesToGoWithout(string word)
    {
        Invocation? parsed = Invocation.Parse([word], out string problem);

        Assert.Null(parsed);
        Assert.Contains("exactly one argument", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abi")]
    [InlineData("help")]
    public void ACommandWithoutAnArgumentRefusesOne(string word)
    {
        Invocation? parsed = Invocation.Parse([word, "extra"], out string problem);

        Assert.Null(parsed);
        Assert.Contains("no argument", problem, StringComparison.Ordinal);
    }
}
