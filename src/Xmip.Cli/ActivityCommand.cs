using System.Globalization;
using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>The compact operational summary shared with the PowerShell prompt.</summary>
public static class ActivityCommand
{
    public static int Run(
        IOperatorSurface surface, string scope, bool json, TextWriter output, TextWriter error)
    {
        ActivitySummary summary = surface.Activity(scope);

        if (!summary.HasValues)
        {
            error.WriteLine($"No activity at {scope} ({surface.Source}).");
            return 1;
        }

        output.WriteLine(json ? Document(surface, summary) : Text(summary));
        return 0;
    }

    public static async Task<int> FollowAsync(
        IOperatorSurface surface, string scope, TextWriter output, CancellationToken stop)
    {
        string? last = null;

        try
        {
            await foreach (SurfaceChange _ in surface.WatchAsync(stop).ConfigureAwait(false))
            {
                string document = Document(surface, surface.Activity(scope));

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

    public static string Text(ActivitySummary value) =>
        $"Received {Figure(value.Received)}  Processed {Figure(value.Processed)}  " +
        $"Sent {Figure(value.Sent)}  Retrying {Figure(value.Retrying)}  " +
        $"Failed {Figure(value.Failed)}";

    public static string Document(IOperatorSurface surface, ActivitySummary value)
    {
        return JsonText.Document(writer =>
        {
            writer.WriteString("scope", value.Scope);
            writer.WriteString("source", surface.Source);
            WriteNullable(writer, "received", value.Received);
            WriteNullable(writer, "processed", value.Processed);
            WriteNullable(writer, "sent", value.Sent);
            WriteNullable(writer, "retrying", value.Retrying);
            WriteNullable(writer, "failed", value.Failed);

            if (value.Observed is { } observed)
            {
                writer.WriteString("observed", observed);
            }
            else
            {
                writer.WriteNull("observed");
            }
        });
    }

    private static string Figure(ulong? value) =>
        value?.ToString("N0", CultureInfo.InvariantCulture) ?? "–";

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
