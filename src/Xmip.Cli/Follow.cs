using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>--follow</c>, once for <c>health</c> and <c>measure</c>: each document
/// <see cref="SurfaceFollow.Changes"/> gives — the current snapshot's, then
/// one whenever the publication advanced and the document changed — as one
/// JSON Lines record, until the token is cancelled (ADR-0014 clause 10). The
/// loop, the rematch of a wildcard and the "changed" test are
/// <see cref="SurfaceFollow"/>'s, the ones <c>-Follow</c> on a cmdlet uses;
/// only the writing is here.
/// </summary>
public static class Follow
{
    /// <summary>Follow what <paramref name="chosen"/> names, each record the
    /// document <paramref name="document"/> writes for what it names
    /// now.</summary>
    public static async Task<int> RunAsync(
        IOperatorSurface surface,
        ScopeSelection chosen,
        Func<IOperatorSurface, ScopeSelection, string> document,
        TextWriter output,
        CancellationToken stop)
    {
        ArgumentNullException.ThrowIfNull(output);

        try
        {
            await foreach (string said in SurfaceFollow
                .Changes(surface, chosen, document, StringComparer.Ordinal, stop)
                .ConfigureAwait(false))
            {
                output.WriteLine(said);
                await output.FlushAsync(stop).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C is the normal end of --follow.
        }

        return 0;
    }
}
