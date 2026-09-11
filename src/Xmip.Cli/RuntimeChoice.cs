using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// Which runtime library <c>xmip</c> loads: <c>--runtime</c> when given, else
/// the one rule every surface uses (ADR-0052 clause 1, <see cref="RuntimeLibrary"/>)
/// — <c>Xmip:RuntimeLibrary</c> in <c>xmip.cli.toml</c> beside the executable,
/// else <c>XMIP_RUNTIME_LIBRARY</c>, else the library beside the executable.
/// No library path is typed as an argument (ADR-0052 clause 5).
/// </summary>
public static class RuntimeChoice
{
    /// <summary>The executable's own configuration, beside it: TOML, like
    /// every document on disk in the estate, with an <c>[Xmip]</c> table.</summary>
    public const string ConfigurationFile = "xmip.cli.toml";

    /// <summary>The rule with every input in hand, so it can be tested without
    /// a file, an environment or an executable.</summary>
    public static string Choose(
        string? overridden,
        string? configured,
        string? fromEnvironment,
        string basePath,
        string besideExecutable)
    {
        return string.IsNullOrWhiteSpace(overridden)
            ? RuntimeLibrary.Choose(configured, fromEnvironment, basePath, besideExecutable)
            : Path.GetFullPath(overridden);
    }

    /// <summary>The library this process should load, reading the
    /// configuration beside the executable and the environment.</summary>
    public static string Find(string? overridden)
    {
        string beside = AppContext.BaseDirectory;

        return Choose(
            overridden,
            TomlDocument.Read(Path.Combine(beside, ConfigurationFile))
                [RuntimeLibrary.ConfigurationKey],
            Environment.GetEnvironmentVariable(RuntimeLibrary.EnvironmentVariable),
            beside,
            beside);
    }
}
