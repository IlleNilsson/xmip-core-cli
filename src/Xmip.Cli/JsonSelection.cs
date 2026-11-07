using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// What a wildcard answered, as one document, the one shape every command
/// gives it, written through <see cref="JsonText"/>.
/// </summary>
public static class JsonSelection
{
    /// <summary>
    /// The <c>pattern</c>, the <c>source</c>, how many scopes it
    /// <c>matched</c>, whatever <paramref name="head"/> adds, and
    /// <c>scopes</c> — one object per answer, written by
    /// <paramref name="each"/>. A program reads a single scope's document or
    /// this one by the key it finds.
    /// </summary>
    public static string Of<T>(
        IOperatorSurface surface,
        ScopeSelection chosen,
        IEnumerable<T> answers,
        Action<Utf8JsonWriter, T> each,
        Action<Utf8JsonWriter>? head = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(each);

        return JsonText.Document(writer =>
        {
            writer.WriteString("pattern", chosen.Argument);
            writer.WriteString("source", surface.Source);
            writer.WriteNumber("matched", chosen.Scopes.Count);
            head?.Invoke(writer);
            writer.WriteStartArray("scopes");

            foreach (T answer in answers)
            {
                writer.WriteStartObject();
                each(writer, answer);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }
}
