using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip list</c>, <c>xmip show</c>, <c>xmip pause</c> and
/// <c>xmip resume</c>: the rows of the scope tree, and the two acts the
/// boundary carries (ADR-0027 clause 5). Every row is the one
/// <see cref="ScopeItem"/> shape the PowerShell module and the GUI read
/// (ADR-0052); only the rendering is here.
/// </summary>
public static class ScopeCommand
{
    /// <summary>
    /// The direct children of what the argument selected. A wildcard lists the
    /// children of every scope it named, in one list: every row already names
    /// its own scope, so nothing is lost by running them together and an
    /// operator reads one list rather than several.
    /// </summary>
    public static int ListOver(
        IOperatorSurface surface, ScopeSelection chosen, bool json, TextWriter output,
        TextWriter error)
    {
        if (!chosen.Patterned)
        {
            return List(surface, chosen.Scopes[0], json, output, error);
        }

        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteString("pattern", chosen.Argument);
                writer.WriteString("source", surface.Source);
                writer.WriteNumber("matched", chosen.Scopes.Count);
                writer.WriteStartArray("scopes");

                foreach (string scope in chosen.Scopes)
                {
                    writer.WriteStartObject();
                    writer.WriteString("scope", scope);
                    writer.WriteStartArray("items");

                    foreach (ScopeItem item in surface.Children(scope))
                    {
                        writer.WriteStartObject();
                        WriteItem(writer, item);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }));

            return 0;
        }

        int rows = 0;

        foreach (ScopeItem item in chosen.Scopes.SelectMany(surface.Children))
        {
            Write(output, item, why: item.Troubled);
            rows++;
        }

        if (rows == 0)
        {
            error.WriteLine($"Nothing beneath {chosen.Argument} ({surface.Source}).");
        }

        return rows == 0 ? 1 : 0;
    }

    /// <summary>One row per scope the argument selected.</summary>
    public static int ShowOver(
        IOperatorSurface surface, ScopeSelection chosen, bool json, TextWriter output,
        TextWriter error)
    {
        if (!chosen.Patterned)
        {
            return Show(surface, chosen.Scopes[0], json, output, error);
        }

        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteString("pattern", chosen.Argument);
                writer.WriteString("source", surface.Source);
                writer.WriteNumber("matched", chosen.Scopes.Count);
                writer.WriteStartArray("items");

                foreach (string scope in chosen.Scopes)
                {
                    writer.WriteStartObject();
                    WriteItem(writer, surface.Describe(scope));
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }));

            return 0;
        }

        foreach (ScopeItem item in chosen.Scopes.Select(surface.Describe))
        {
            Write(output, item, why: true);
        }

        return 0;
    }

    /// <summary>
    /// Pause or resume every scope the argument selected. A wildcard adds no
    /// reach an operator did not have: one act already reaches everything
    /// beneath the scope it names (ADR-0027), so a pattern names several
    /// subtrees rather than opening a wider one. The exit is non-zero unless
    /// every one of them was applied.
    /// </summary>
    public static int ApplyOver(
        IOperatorSurface surface, ScopeSelection chosen, ScopeAction action, string who,
        bool json, TextWriter output, TextWriter error)
    {
        if (!chosen.Patterned)
        {
            return Apply(surface, chosen.Scopes[0], action, who, json, output, error);
        }

        List<ScopeOperation> done =
            [.. chosen.Scopes.Select(scope => surface.Control(scope, action, who))];

        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteString("pattern", chosen.Argument);
                writer.WriteString("action", action.ToString().ToLowerInvariant());
                writer.WriteNumber("matched", done.Count);
                writer.WriteStartArray("scopes");

                foreach (ScopeOperation operation in done)
                {
                    writer.WriteStartObject();
                    writer.WriteString("scope", operation.Scope);
                    writer.WriteBoolean("applied", operation.Applied);
                    writer.WriteString("result", operation.Result);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }));
        }
        else
        {
            foreach (ScopeOperation operation in done)
            {
                TextWriter said = operation.Applied ? output : error;
                said.WriteLine(operation.Result);
            }
        }

        return done.TrueForAll(operation => operation.Applied) ? 0 : 1;
    }

    /// <summary>The direct children of a scope, one row each.</summary>
    public static int List(
        IOperatorSurface surface, string scope, bool json, TextWriter output, TextWriter error)
    {
        IReadOnlyList<ScopeItem> children = surface.Children(scope);

        if (children.Count == 0)
        {
            error.WriteLine($"Nothing beneath {scope} ({surface.Source}).");
            return 1;
        }

        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteString("scope", scope);
                writer.WriteString("source", surface.Source);
                writer.WriteStartArray("items");

                foreach (ScopeItem item in children)
                {
                    writer.WriteStartObject();
                    WriteItem(writer, item);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }));

            return 0;
        }

        foreach (ScopeItem item in children)
        {
            Write(output, item, why: item.Troubled);
        }

        return 0;
    }

    /// <summary>One scope as a row, with its evidence beneath.</summary>
    public static int Show(
        IOperatorSurface surface, string scope, bool json, TextWriter output, TextWriter error)
    {
        ScopeItem item = surface.Describe(scope);

        if (item.Health is null && item.Observed is null)
        {
            error.WriteLine($"Nothing at {scope} ({surface.Source}).");
            return 1;
        }

        if (json)
        {
            output.WriteLine(JsonText.Document(writer => WriteItem(writer, item)));
            return 0;
        }

        Write(output, item, why: true);

        return 0;
    }

    /// <summary>Pause or resume a scope and say what the runtime said. Exit 1
    /// when the runtime did not apply it.</summary>
    public static int Apply(
        IOperatorSurface surface, string scope, ScopeAction action, string who,
        bool json, TextWriter output, TextWriter error)
    {
        ScopeOperation operation = surface.Control(scope, action, who);

        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteString("scope", scope);
                writer.WriteString("action", action.ToString().ToLowerInvariant());
                writer.WriteBoolean("applied", operation.Applied);
                writer.WriteString("result", operation.Result);
            }));
        }
        else if (operation.Applied)
        {
            output.WriteLine(operation.Result);
        }
        else
        {
            error.WriteLine(operation.Result);
        }

        return operation.Applied ? 0 : 1;
    }

    /// <summary>
    /// A pattern that named nothing, for a program: <c>--json</c> promises
    /// structure, and the refusal <see cref="ScopeSelection.Of"/> gave is an
    /// answer like any other. It goes to stderr with the same non-zero exit
    /// the words do, so a script that reads stdout alone still learns nothing
    /// it could mistake for all clear.
    /// </summary>
    public static string Unmatched(string pattern, string refusal)
    {
        return JsonText.Document(writer =>
        {
            writer.WriteString("pattern", pattern);
            writer.WriteNumber("matched", 0);
            writer.WriteString("refused", refusal);
        });
    }

    // One row: the mood, the scope, the figures; and, where asked, why — for a
    // container the leaf that explains it and its evidence, which is the next
    // scope to drill to, and for a leaf its own evidence (ADR-0052 clause 2).
    // A list says why on the rows that are not fine; show always does.
    private static void Write(TextWriter output, ScopeItem item, bool why)
    {
        string mood = item.Health is { } health ? English.Mood(health) : "unknown";

        output.WriteLine($"{mood,-9}  {item.Scope}  {English.Figures(item.Figures)}");

        if (!why || string.IsNullOrEmpty(item.Evidence))
        {
            return;
        }

        output.WriteLine(item.IsContainer && item.Worst is { } worst
            ? $"  worst {worst}: {item.Evidence}"
            : $"  {item.Evidence}");
    }

    private static void WriteItem(Utf8JsonWriter writer, ScopeItem item)
    {
        writer.WriteString("name", item.Name);
        writer.WriteString("scope", item.Scope);
        writer.WriteBoolean("container", item.IsContainer);
        writer.WriteString("health", item.Health is { } health ? English.Mood(health) : null);

        if (item.Severity is { } severity)
        {
            writer.WriteNumber("severity", severity);
        }
        else
        {
            writer.WriteNull("severity");
        }

        writer.WriteString("evidence", item.Evidence);
        writer.WriteString("worst", item.Worst);
        MeasureCommand.Write(writer, item.Figures);
    }
}
