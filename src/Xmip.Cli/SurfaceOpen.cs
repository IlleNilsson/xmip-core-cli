using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// Which surface a command reads: a web host on another machine when
/// <c>--remote</c> names one, followed over its surface hub (ADR-0052,
/// amendment 2026-09-15), else the runtime library <see cref="RuntimeChoice"/>
/// finds. A surface that answers nothing is not returned; the reason is.
/// </summary>
public static class SurfaceOpen
{
    /// <summary>The surface, or null with <paramref name="reason"/> saying why.
    /// The caller disposes what it gets.</summary>
    public static IOperatorSurface? Open(Invocation invocation, out string reason)
    {
        if (!string.IsNullOrWhiteSpace(invocation.Remote))
        {
            RemoteOperator remote = new(new Uri(invocation.Remote, UriKind.Absolute));

            if (remote.Connect())
            {
                reason = string.Empty;
                return remote;
            }

            reason = remote.Source;
            remote.Dispose();
            return null;
        }

        NativeOperator native = new(RuntimeChoice.Find(invocation.Runtime));

        if (native.IsLoaded)
        {
            reason = string.Empty;
            return native;
        }

        reason = native.Reason;
        native.Dispose();
        return null;
    }
}
