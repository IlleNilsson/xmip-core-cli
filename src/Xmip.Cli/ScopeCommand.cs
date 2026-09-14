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
            output.WriteLine(Row(item));
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

        output.WriteLine(Row(item));

        if (!string.IsNullOrEmpty(item.Evidence))
        {
            output.WriteLine($"  {item.Evidence}");
        }

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

    private static string Row(ScopeItem item)
    {
        string mood = item.Health?.ToString().ToLowerInvariant() ?? "unknown";

        return $"{mood,-9}  {item.Scope}  {MeasureCommand.Text(item.Figures)}";
    }

    private static void WriteItem(Utf8JsonWriter writer, ScopeItem item)
    {
        writer.WriteString("name", item.Name);
        writer.WriteString("scope", item.Scope);
        writer.WriteBoolean("container", item.IsContainer);
        writer.WriteString("health", item.Health?.ToString().ToLowerInvariant());

        if (item.Severity is { } severity)
        {
            writer.WriteNumber("severity", severity);
        }
        else
        {
            writer.WriteNull("severity");
        }

        writer.WriteString("evidence", item.Evidence);
        MeasureCommand.Write(writer, item.Figures);
    }
}
