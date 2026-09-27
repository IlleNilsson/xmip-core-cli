using System.Text;
using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// One JSON document as text, written by hand through
/// <see cref="Utf8JsonWriter"/>. No serializer: the executable publishes
/// ahead-of-time, and a reflection-based serializer is the first thing the
/// trimmer takes away. Every command's document is a few properties, so
/// writing them is shorter than describing them.
/// </summary>
public static class JsonText
{
    /// <summary>A document for a program: one line, no indentation, so it is
    /// one JSON Lines record when several follow each other (ADR-0014
    /// clause 10).</summary>
    public static string Document(Action<Utf8JsonWriter> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        using MemoryStream buffer = new();

        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// What a wildcard answered, as one document, the one shape every command
    /// gives it: the <c>pattern</c>, the <c>source</c>, how many scopes it
    /// <c>matched</c>, whatever <paramref name="head"/> adds, and
    /// <c>scopes</c> — one object per answer, written by
    /// <paramref name="each"/>. A program reads a single scope's document or
    /// this one by the key it finds.
    /// </summary>
    public static string Selection<T>(
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

        return Document(writer =>
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
