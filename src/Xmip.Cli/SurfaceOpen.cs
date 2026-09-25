using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// Which surface a command reads, and which runtime <c>validate</c> asks:
/// the line over <c>xmip.cli.toml</c> beside the executable, by the one
/// precedence every surface shares — <see cref="SurfaceChoice.Stated"/> and
/// <see cref="RuntimeLibrary.Stated"/> in <c>Xmip.Surface</c> (ADR-0052
/// clause 1 and its amendments of 2026-09-18 and 2026-09-20). What is the
/// executable's own is only where its document lies. No library path is
/// typed as an argument (ADR-0052 clause 5); <c>--runtime</c> is an option.
/// </summary>
/// <remarks>
/// One surface, never a set of them (ADR-0052, amendment 2026-09-20): an
/// invocation answers one question at one scope and ends, and a rollup or a
/// sum over two clusters would be a figure at a scope that is in neither
/// tree. <c>--snapshot</c> names which cluster; where the document names
/// several, the first is read.
/// </remarks>
public static class SurfaceOpen
{
    /// <summary>The executable's own configuration, beside it: TOML, like
    /// every document on disk in the estate, with an <c>[Xmip]</c> table.</summary>
    public const string ConfigurationFile = "xmip.cli.toml";

    /// <summary>The surface, or null with <paramref name="reason"/> saying why.
    /// The caller disposes what it gets.</summary>
    public static IOperatorSurface? Open(Invocation invocation, out string reason)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        string beside = AppContext.BaseDirectory;

        return SurfaceChoice.Answering(
            invocation.Line, Path.Combine(beside, ConfigurationFile), beside, out reason);
    }

    /// <summary>The runtime library <c>validate</c> loads: <c>--runtime</c>,
    /// else the document, the environment and beside the executable. The
    /// audit records through the same library, and a line that could not be
    /// parsed (null) states none.</summary>
    public static string Runtime(Invocation? invocation)
    {
        string beside = AppContext.BaseDirectory;

        return RuntimeLibrary.Stated(
            invocation?.Runtime,
            TomlDocument.Read(Path.Combine(beside, ConfigurationFile)),
            beside,
            beside);
    }
}
