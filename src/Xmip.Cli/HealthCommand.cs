using System.Text.Json;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip health &lt;scope&gt;</c>: every leaf at and beneath a scope, worst
/// first, with the rollup for the scope itself (ADR-0041). A scope that is
/// Holding says why on the spot — the worst leaf beneath it and that leaf's
/// evidence (ADR-0052 clause 2). Text for a person, one document with
/// <c>--json</c>, and with <c>--follow</c> (<see cref="Follow"/> over
/// <see cref="Answer"/>) one JSON Lines record each time
/// the health changes, until the token is cancelled (ADR-0014 clause 10).
/// </summary>
public static class HealthCommand
{
    /// <summary>
    /// Read once and render what the argument selected. One scope is what it
    /// always was; a wildcard answers for every scope it named, one banner and
    /// one list each, because a rollup over several scopes would be a rollup of
    /// a scope that is not in the tree (ADR-0041), and inventing one is what no
    /// surface does.
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

        foreach (string scope in chosen.Scopes)
        {
            IReadOnlyList<HealthRecord> records = surface.Health(scope);

            // A publication that advanced between the match and the read can
            // have lost a scope; what is gone is not said, and what is left is.
            if (records.Count == 0)
            {
                continue;
            }

            WriteText(surface, scope, records, output);
            output.WriteLine();
        }

        return 0;
    }

    /// <summary>Read once and render.</summary>
    public static int Run(
        IOperatorSurface surface, string scope, bool json, TextWriter output, TextWriter error)
    {
        IReadOnlyList<HealthRecord> records = surface.Health(scope);

        if (records.Count == 0)
        {
            error.WriteLine(English.NothingAt(scope, surface.Source));
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

    /// <summary>What the argument selected, as one document: a single scope's
    /// <see cref="Document"/>, or for a wildcard <see cref="Documents"/>.</summary>
    public static string Answer(IOperatorSurface surface, ScopeSelection chosen)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(chosen);

        return chosen.Patterned
            ? Documents(surface, chosen)
            : Document(surface, chosen.Scopes[0], surface.Health(chosen.Scopes[0]));
    }

    /// <summary>
    /// What a wildcard answered, as one document
    /// (<see cref="JsonText.Selection{T}"/>), with the same object per scope that
    /// a single read emits. A pattern that named nothing is a document saying
    /// nothing was named — never an empty line.
    /// </summary>
    public static string Documents(IOperatorSurface surface, ScopeSelection chosen)
    {
        return JsonText.Selection(
            surface,
            chosen,
            chosen.Scopes,
            (writer, scope) => Body(writer, surface, scope, surface.Health(scope)));
    }

    /// <summary>The scope, its rollup, the worst leaf and every leaf, as one
    /// document: the same shape a single read and a follow record share.</summary>
    public static string Document(
        IOperatorSurface surface, string scope, IReadOnlyList<HealthRecord> records)
    {
        return JsonText.Document(writer => Body(writer, surface, scope, records));
    }

    private static void Body(
        Utf8JsonWriter writer,
        IOperatorSurface surface,
        string scope,
        IReadOnlyList<HealthRecord> records)
    {
        HealthRecord? worst = ScopeTree.Worst(records);

        writer.WriteString("scope", scope);
        writer.WriteString("source", surface.Source);
        writer.WriteString("state", English.Rollup(records));

        // What the run was started with, where its publisher says — the same
        // line the GUI puts at the top of every view, including what each node
        // declared it can do (ADR-0056). A surface that says nothing of a run
        // writes no key at all.
        if (surface.Run() is { Said: true } run)
        {
            writer.WriteString("run", run.Line());
        }

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
        output.WriteLine($"{English.Mood(rollup),-9} {"",3}  {scope}");

        if (rollup == HealthState.Holding)
        {
            output.WriteLine($"{"",14} {English.Mood(worst.State)} at {worst.Scope}");

            if (worst.Evidence.Length > 0)
            {
                output.WriteLine($"{"",14} {worst.Evidence}");
            }
        }

        output.WriteLine($"{"",14} source {surface.Source}");

        if (surface.Run() is { Said: true } run)
        {
            output.WriteLine($"{"",14} run {run.Line()}");
        }

        output.WriteLine();

        foreach (HealthRecord record in records)
        {
            string mood = English.Mood(record.State);
            output.WriteLine($"{mood,-9} {record.Severity,3}  {record.Scope}");

            if (record.Evidence.Length > 0)
            {
                output.WriteLine($"{"",14} {record.Evidence}");
            }

            output.WriteLine($"{"",14} observed {record.Observed:O}");
        }
    }
}
