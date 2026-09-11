using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// Which runtime wins when several are named: <c>--runtime</c>, then the
/// configuration, then the environment, then the library beside the
/// executable. Every input is handed in, so no test touches the process
/// environment or the disk.
/// </summary>
public sealed class RuntimeChoiceTests
{
    private static readonly string Base = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "xmip-cli-test", "config"));

    private static readonly string Beside = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "xmip-cli-test", "bin"));

    [Fact]
    public void TheOverrideWinsOverEverything()
    {
        string chosen = RuntimeChoice.Choose(
            "override.dll", "configured.dll", "environment.dll", Base, Beside);

        Assert.Equal(Path.GetFullPath("override.dll"), chosen);
    }

    [Fact]
    public void TheConfigurationWinsOverTheEnvironment()
    {
        string chosen = RuntimeChoice.Choose(
            null, "configured.dll", "environment.dll", Base, Beside);

        Assert.Equal(Path.Combine(Base, "configured.dll"), chosen);
    }

    [Fact]
    public void TheEnvironmentWinsOverTheLibraryBesideTheExecutable()
    {
        string chosen = RuntimeChoice.Choose(null, null, "environment.dll", Base, Beside);

        Assert.Equal(Path.GetFullPath("environment.dll"), chosen);
    }

    [Fact]
    public void NothingNamedMeansBesideTheExecutable()
    {
        string chosen = RuntimeChoice.Choose(null, string.Empty, "  ", Base, Beside);

        Assert.Equal(Path.Combine(Beside, RuntimeLibrary.FileName), chosen);
    }

    [Fact]
    public void ABlankOverrideIsNoOverride()
    {
        string chosen = RuntimeChoice.Choose(" ", "configured.dll", null, Base, Beside);

        Assert.Equal(Path.Combine(Base, "configured.dll"), chosen);
    }
}
