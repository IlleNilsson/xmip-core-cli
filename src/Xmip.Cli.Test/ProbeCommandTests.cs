namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip probe</c> refuses a path that names nothing before touching the
/// loader, and says which path. Probing a real module is the binding's test;
/// this project loads no native library.
/// </summary>
public sealed class ProbeCommandTests
{
    [Fact]
    public void AMissingLibraryIsAUsageErrorOnStderr()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"no-such-module-{Guid.NewGuid():n}.dll");
        StringWriter error = new();

        int exit = ProbeCommand.Run(missing, json: false, new StringWriter(), error);

        Assert.Equal(2, exit);
        Assert.Contains(missing, error.ToString(), StringComparison.Ordinal);
    }
}
