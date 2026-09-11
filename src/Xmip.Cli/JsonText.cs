using System.Text;
using System.Text.Json;

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
        using MemoryStream buffer = new();

        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
