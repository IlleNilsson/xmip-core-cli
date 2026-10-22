using System.Text.Json;

namespace Xmip.Cli;

/// <summary>One answer of <c>xmip certificate</c>: the document for
/// <c>--json</c>, a sentence for a person, a refusal on the error writer.
/// </summary>
internal sealed record CertificateAnswer(bool Json, TextWriter Output, TextWriter Error)
{
    /// <summary>Say it, and <paramref name="more"/> after the sentence for a
    /// person; exit 0 where it was done, 1 where it was refused.</summary>
    public int Said(
        bool ok, string refused, Action<Utf8JsonWriter> fields, string sentence,
        Action<TextWriter>? more = null)
    {
        if (!ok)
        {
            Error.WriteLine(Json
                ? JsonText.Document(writer => writer.WriteString("refused", refused))
                : refused);
            return 1;
        }

        Output.WriteLine(Json ? JsonText.Document(fields) : sentence);

        if (!Json)
        {
            more?.Invoke(Output);
        }

        return 0;
    }
}
