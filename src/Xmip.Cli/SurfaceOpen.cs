using Microsoft.Extensions.Configuration;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// Which surface a command reads. The line wins: a web host on another
/// machine when <c>--remote</c> names one, followed over its surface hub
/// (ADR-0052, amendment 2026-09-15), or the library <c>--runtime</c> names.
/// Else the document beside the executable, <c>xmip.cli.toml</c>, chooses
/// with the same <c>[Xmip]</c> keys as every other host's document — native,
/// snapshot or remote (ADR-0052 clause 3); until 2026-09-18 the executable
/// read that document for the runtime library alone and could not follow a
/// snapshot at all. Else the runtime library <see cref="RuntimeChoice"/>
/// finds. A surface that answers nothing is not returned; the reason is.
/// </summary>
public static class SurfaceOpen
{
    /// <summary>The surface the line and the document choose, not yet asked
    /// to answer, so the precedence can be tested without a runtime or a
    /// network. <paramref name="beside"/> is the directory the document's
    /// paths are written from and where the runtime library lies by default.</summary>
    /// <exception cref="InvalidOperationException">The document names a
    /// surface this build does not know, or one without its path.</exception>
    public static IOperatorSurface Choose(
        Invocation invocation, IConfiguration document, string beside)
    {
        return invocation.Remote is { } host && !string.IsNullOrWhiteSpace(host)
            ? new RemoteOperator(new Uri(host, UriKind.Absolute))
            : string.IsNullOrWhiteSpace(invocation.Runtime) && SurfaceChoice.IsChosen(document)
                ? SurfaceChoice.Open(document, beside)
                : Native(invocation.Runtime, document, beside);
    }

    private static NativeOperator Native(string? overridden, IConfiguration document, string beside)
    {
        return new NativeOperator(RuntimeChoice.Choose(
            overridden,
            document[RuntimeLibrary.ConfigurationKey],
            Environment.GetEnvironmentVariable(RuntimeLibrary.EnvironmentVariable),
            beside,
            beside));
    }

    /// <summary>The surface, or null with <paramref name="reason"/> saying why.
    /// The caller disposes what it gets.</summary>
    public static IOperatorSurface? Open(Invocation invocation, out string reason)
    {
        string beside = AppContext.BaseDirectory;
        IOperatorSurface surface;

        try
        {
            surface = Choose(
                invocation,
                TomlDocument.Read(Path.Combine(beside, RuntimeChoice.ConfigurationFile)),
                beside);
        }
        catch (InvalidOperationException misconfigured)
        {
            reason = $"{RuntimeChoice.ConfigurationFile}: {misconfigured.Message}";
            return null;
        }

        bool answers = surface switch
        {
            RemoteOperator remote => remote.Connect(),
            NativeOperator native => native.IsLoaded,
            SnapshotOperator snapshot => snapshot.Exists,
            _ => true,
        };

        if (answers)
        {
            reason = string.Empty;
            return surface;
        }

        reason = surface.Source;
        (surface as IDisposable)?.Dispose();
        return null;
    }
}
