using Xmip.Abi.Module;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip validate &lt;toml&gt;</c>: the runtime's answer on a node
/// configuration file, without starting it. The file's text crosses, not its
/// path — the runtime validates a proposed document and publishes nothing
/// (ADR-0027 clause 9). The verdict is <see cref="NativeOperator.Validate(string)"/>'s,
/// the same record and sentence the desktop shows (ADR-0052 clause 4); the
/// document carries the record, and the exit code is decided from it.
/// </summary>
public static class ValidateCommand
{
    /// <summary>Render one verdict: text for a person, or the record as a
    /// document. Exit 0 when the document is valid, 1 when it is not.</summary>
    public static int Run(
        ConfigurationVerdict verdict, bool json, TextWriter output, TextWriter error)
    {
        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteString("path", verdict.Path);
                writer.WriteBoolean("valid", verdict.Ok);
                writer.WriteString("status", verdict.Status.ToString());
                writer.WriteString("meaning", verdict.Status.Explain());
                writer.WriteStartArray("problems");

                foreach (string problem in verdict.Problems)
                {
                    writer.WriteStringValue(problem);
                }

                writer.WriteEndArray();
            }));

            return verdict.Ok ? 0 : 1;
        }

        if (verdict.Ok)
        {
            output.WriteLine(verdict.Said);
            return 0;
        }

        error.WriteLine(verdict.Said);
        return 1;
    }
}
