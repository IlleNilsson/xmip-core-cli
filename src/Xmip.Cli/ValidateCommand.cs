using Xmip.Abi.Module;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip validate &lt;toml&gt;</c>: the runtime's answer on a node
/// configuration file, without starting it. The file's text crosses, not its
/// path — the runtime validates a proposed document and publishes nothing
/// (ADR-0027 clause 9). The sentence is <see cref="English.Validated"/>, the
/// same one the desktop shows; the document carries the record itself.
/// </summary>
public static class ValidateCommand
{
    /// <summary>Validate one document through <paramref name="validate"/>,
    /// the runtime's answer on configuration text.</summary>
    public static int Run(
        string configurationPath,
        string text,
        Func<string, ValidationRecord> validate,
        bool json,
        TextWriter output,
        TextWriter error)
    {
        ValidationRecord answer = validate(text);

        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteString("path", configurationPath);
                writer.WriteBoolean("valid", answer.IsValid);
                writer.WriteString("status", answer.Status.ToString());
                writer.WriteString("meaning", answer.Status.Explain());
                writer.WriteStartArray("problems");

                foreach (string problem in answer.Problems)
                {
                    writer.WriteStringValue(problem);
                }

                writer.WriteEndArray();
            }));

            return answer.IsValid ? 0 : 1;
        }

        string said = English.Validated(configurationPath, answer);

        if (answer.IsValid)
        {
            output.WriteLine(said);
            return 0;
        }

        error.WriteLine(said);
        return 1;
    }
}
