using System.Globalization;
using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>CLI navigation and actions over the same scopes PowerShell exposes.</summary>
public static class ScopeCommand
{
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
        }
        else
        {
            foreach (ScopeItem item in children)
            {
                output.WriteLine(Row(item));
            }
        }

        return 0;
    }

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
        }
        else
        {
            output.WriteLine(Row(item));
            if (!string.IsNullOrEmpty(item.Evidence))
            {
                output.WriteLine($"  {item.Evidence}");
            }
        }

        return 0;
    }

    public static int Apply(
        IOperatorSurface surface, string scope, string action, string who,
        bool json, TextWriter output, TextWriter error)
    {
        ScopeAction parsed = Enum.Parse<ScopeAction>(action, ignoreCase: true);
        ScopeOperation operation = surface.ControlScope(scope, parsed, who);

        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteString("scope", scope);
                writer.WriteString("action", action);
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

    private static string Row(ScopeItem item) =>
        $"{item.Health?.ToString() ?? "Unknown",-9}  {item.Scope}  " +
        $"Rcv {Figure(item.Received)}  Prc {Figure(item.Processed)}  " +
        $"Snt {Figure(item.Sent)}  Retry {Figure(item.Retrying)}  Fail {Figure(item.Failed)}";

    private static void WriteItem(Utf8JsonWriter writer, ScopeItem item)
    {
        writer.WriteString("name", item.Name);
        writer.WriteString("scope", item.Scope);
        writer.WriteBoolean("container", item.IsContainer);
        writer.WriteString("health", item.Health?.ToString().ToLowerInvariant());
        WriteNullable(writer, "severity", item.Severity);
        writer.WriteString("evidence", item.Evidence);
        WriteNullable(writer, "received", item.Received);
        WriteNullable(writer, "processed", item.Processed);
        WriteNullable(writer, "sent", item.Sent);
        WriteNullable(writer, "retrying", item.Retrying);
        WriteNullable(writer, "failed", item.Failed);
        if (item.Observed is { } observed) writer.WriteString("observed", observed);
        else writer.WriteNull("observed");
    }

    private static string Figure(ulong? value) =>
        value?.ToString("N0", CultureInfo.InvariantCulture) ?? "–";

    private static void WriteNullable(Utf8JsonWriter writer, string name, ulong? value)
    {
        if (value.HasValue) writer.WriteNumber(name, value.Value);
        else writer.WriteNull(name);
    }

    private static void WriteNullable(Utf8JsonWriter writer, string name, byte? value)
    {
        if (value.HasValue) writer.WriteNumber(name, value.Value);
        else writer.WriteNull(name);
    }
}
