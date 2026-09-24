using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip measure [scope]</c>: the six figures at a scope, the cluster when
/// none is named — Streams, Messages, Journeys, bytes, Retrying, Failed, in
/// the order every surface says them (ADR-0027 clause 5). A figure the
/// runtime has not published is a dash, never a zero. With <c>--follow</c>,
/// one JSON Lines record each time the figures change.
/// </summary>
/// <remarks>
/// <b>Totals here, a rate in the prompt, and that is deliberate</b> (ADR-0052,
/// amendment 2026-09-20; ADR-0014's amendment asks that a change either reach
/// every surface or name the one it did not). An invocation is one sample: it
/// has no interval to divide by, and a rate taken from a single reading would
/// be the average over the whole of the publisher's uptime — the very number
/// the owner retired from the prompt. The prompt is a line that is always
/// there and reads two publications; a command is a question asked once. With
/// <c>--follow</c> there are samples, and the JSON Lines records already carry
/// the figures and the scope for each one, so a program computes the rate
/// exactly from two documents rather than being handed a rounded one (ADR-0014
/// clause 10: <c>--json</c> is for a program). The web board already says a
/// rate in words beside each tile (<c>English.Flow</c>), so the executable is
/// the one surface showing a total alone, and it says so here.
/// </remarks>
public static class MeasureCommand
{
    /// <summary>
    /// Read once and render what the argument selected. A wildcard says one
    /// line per scope it named, each line naming its own scope; the figures are
    /// not added together, because a sum over several scopes is a figure at a
    /// scope that does not exist, and no surface invents one.
    /// </summary>
    public static int Over(
        IOperatorSurface surface, ScopeSelection chosen, bool json, TextWriter output,
        TextWriter error)
    {
        if (!chosen.Patterned)
        {
            return Run(surface, chosen.Scopes[0], json, output, error);
        }

        if (json)
        {
            output.WriteLine(Documents(surface, chosen));
            return 0;
        }

        bool measured = false;

        foreach (string scope in chosen.Scopes)
        {
            Figures figures = surface.Figures(scope);
            measured |= figures.HasValues;
            output.WriteLine($"{scope}  {Text(figures)}");
        }

        if (!measured)
        {
            error.WriteLine($"Nothing measured at {chosen.Argument} ({surface.Source}).");
        }

        return measured ? 0 : 1;
    }

    /// <summary>Read once and render.</summary>
    public static int Run(
        IOperatorSurface surface, string scope, bool json, TextWriter output, TextWriter error)
    {
        Figures figures = surface.Figures(scope);

        if (!figures.HasValues)
        {
            error.WriteLine($"Nothing measured at {scope} ({surface.Source}).");
            return 1;
        }

        output.WriteLine(json ? Document(surface, figures) : Text(figures));
        return 0;
    }

    /// <summary>Emit a record now and whenever the published figures change.
    /// A wildcard is matched again at every notice, as health's follow is.</summary>
    public static async Task<int> FollowAsync(
        IOperatorSurface surface, ScopeSelection chosen, TextWriter output, CancellationToken stop)
    {
        string? last = null;

        try
        {
            await foreach (SurfaceChange _ in surface.WatchAsync(stop).ConfigureAwait(false))
            {
                ScopeSelection now = ScopeSelection.Of(surface, chosen.Argument, out string gone)
                    ?? chosen with { Scopes = [] };
                string document = now.Patterned
                    ? Documents(surface, now)
                    : Document(surface, surface.Figures(now.Scopes[0]));

                if (string.Equals(document, last, StringComparison.Ordinal))
                {
                    continue;
                }

                output.WriteLine(document);
                await output.FlushAsync(stop).ConfigureAwait(false);
                last = document;
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C is the normal end of --follow.
        }

        return 0;
    }

    /// <summary>Follow one scope, the shape every caller had before a scope
    /// could be a pattern.</summary>
    public static Task<int> FollowAsync(
        IOperatorSurface surface, string scope, TextWriter output, CancellationToken stop)
    {
        return FollowAsync(surface, ScopeSelection.Exactly(scope), output, stop);
    }

    /// <summary>What a wildcard measured, as one document: the pattern, how
    /// many scopes it named, and the six figures at each of them.</summary>
    public static string Documents(IOperatorSurface surface, ScopeSelection chosen)
    {
        return JsonText.Document(writer =>
        {
            writer.WriteString("pattern", chosen.Argument);
            writer.WriteString("source", surface.Source);
            writer.WriteNumber("matched", chosen.Scopes.Count);
            writer.WriteStartArray("scopes");

            foreach (string scope in chosen.Scopes)
            {
                writer.WriteStartObject();
                writer.WriteString("scope", scope);
                Write(writer, surface.Figures(scope));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }

    /// <summary>The six figures on one line, for a person.</summary>
    public static string Text(Figures figures)
    {
        ArgumentNullException.ThrowIfNull(figures);

        return $"Streams {English.Figure(figures.Streams)}  " +
            $"Messages {English.Figure(figures.Messages)}  " +
            $"Journeys {English.Figure(figures.Journeys)}  " +
            $"Bytes {English.Figure(figures.Bytes)}  " +
            $"Retrying {English.Figure(figures.Retrying)}  " +
            $"Failed {English.Figure(figures.Failed)}";
    }

    /// <summary>The six figures as one JSON document, for a program.</summary>
    public static string Document(IOperatorSurface surface, Figures figures)
    {
        return JsonText.Document(writer =>
        {
            writer.WriteString("scope", figures.Scope);
            writer.WriteString("source", surface.Source);
            Write(writer, figures);
        });
    }

    /// <summary>The six figures and when they were observed, into an open object.</summary>
    public static void Write(Utf8JsonWriter writer, Figures figures)
    {
        WriteNullable(writer, "streams", figures.Streams);
        WriteNullable(writer, "messages", figures.Messages);
        WriteNullable(writer, "journeys", figures.Journeys);
        WriteNullable(writer, "bytes", figures.Bytes);
        WriteNullable(writer, "retrying", figures.Retrying);
        WriteNullable(writer, "failed", figures.Failed);

        if (figures.Observed is { } observed)
        {
            writer.WriteString("observed", observed);
        }
        else
        {
            writer.WriteNull("observed");
        }
    }

    private static void WriteNullable(Utf8JsonWriter writer, string name, ulong? value)
    {
        if (value.HasValue)
        {
            writer.WriteNumber(name, value.Value);
        }
        else
        {
            writer.WriteNull(name);
        }
    }
}
