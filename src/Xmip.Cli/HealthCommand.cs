using System.Text.Json;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip health &lt;scope&gt;</c>: every leaf at and beneath a scope, worst
/// first, with the rollup for the scope itself (ADR-0041). A scope that is
/// Holding says why on the spot — the worst leaf beneath it and that leaf's
/// evidence (ADR-0052 clause 2). Text for a person, one document with
/// <c>--json</c>, and with <c>--follow</c> one JSON Lines record each time
/// the health changes, until the token is cancelled (ADR-0014 clause 10).
/// </summary>
public static class HealthCommand
{
    /// <summary>How often <c>--follow</c> asks the surface again.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>Read once and render.</summary>
    public static int Run(
        IOperatorSurface surface, string scope, bool json, TextWriter output, TextWriter error)
    {
        IReadOnlyList<HealthRecord> records = surface.Health(scope);

        if (records.Count == 0)
        {
            error.WriteLine($"Nothing at {scope} ({surface.Source}).");
            return 1;
        }

        if (json)
        {
            output.WriteLine(Document(surface, scope, records));
        }
        else
        {
            WriteText(surface, scope, records, output);
        }

        return 0;
    }

    /// <summary>
    /// Read every <see cref="Interval"/> and emit one JSON Lines record each
    /// time what the surface says differs from the last time, the first read
    /// included, until <paramref name="stop"/> is cancelled. Observation is
    /// lossy by design (ADR-0014 clause 5): a change between two reads is not
    /// seen, and this says nothing about it.
    /// </summary>
    public static async Task<int> FollowAsync(
        IOperatorSurface surface,
        string scope,
        TextWriter output,
        TimeSpan interval,
        CancellationToken stop)
    {
        string? last = null;

        while (!stop.IsCancellationRequested)
        {
            IReadOnlyList<HealthRecord> records = surface.Health(scope);
            string document = Document(surface, scope, records);

            if (!string.Equals(document, last, StringComparison.Ordinal))
            {
                output.WriteLine(document);
                await output.FlushAsync(stop).ConfigureAwait(false);
                last = document;
            }

            try
            {
                await Task.Delay(interval, stop).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return 0;
    }

    /// <summary>The scope, its rollup, the worst leaf and every leaf, as one
    /// document: the same shape a single read and a follow record share.</summary>
    public static string Document(
        IOperatorSurface surface, string scope, IReadOnlyList<HealthRecord> records)
    {
        HealthRecord? worst = ScopeTree.Worst(records);

        return JsonText.Document(writer =>
        {
            writer.WriteString("scope", scope);
            writer.WriteString("source", surface.Source);
            writer.WriteString("state", English.Rollup(records));

            if (worst is not null)
            {
                writer.WriteStartObject("worst");
                WriteRecord(writer, worst);
                writer.WriteEndObject();
            }

            writer.WriteStartArray("records");

            foreach (HealthRecord record in records)
            {
                writer.WriteStartObject();
                WriteRecord(writer, record);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }

    private static void WriteRecord(Utf8JsonWriter writer, HealthRecord record)
    {
        writer.WriteString("scope", record.Scope);
        writer.WriteString("state", English.Mood(record.State));
        writer.WriteNumber("severity", record.Severity);
        writer.WriteString("evidence", record.Evidence);
        writer.WriteString("observed", record.Observed);
    }

    private static void WriteText(
        IOperatorSurface surface,
        string scope,
        IReadOnlyList<HealthRecord> records,
        TextWriter output)
    {
        HealthState rollup = ScopeTree.Rollup(records) ?? HealthState.Fine;
        HealthRecord worst = ScopeTree.Worst(records)!;

        // The banner: the scope, its rollup, and — when Holding — why, on the
        // spot. The word alone is not a state an operator can act on.
        output.WriteLine($"{rollup,-9} {"",3}  {scope}");

        if (rollup == HealthState.Holding)
        {
            output.WriteLine($"{"",14} {worst.State} at {worst.Scope}");

            if (worst.Evidence.Length > 0)
            {
                output.WriteLine($"{"",14} {worst.Evidence}");
            }
        }

        output.WriteLine($"{"",14} source {surface.Source}");
        output.WriteLine();

        foreach (HealthRecord record in records)
        {
            output.WriteLine($"{record.State,-9} {record.Severity,3}  {record.Scope}");

            if (record.Evidence.Length > 0)
            {
                output.WriteLine($"{"",14} {record.Evidence}");
            }

            output.WriteLine($"{"",14} observed {record.Observed:O}");
        }
    }
}
