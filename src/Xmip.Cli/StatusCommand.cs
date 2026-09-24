using System.Globalization;
using Xmip.Abi.Module;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip status &lt;code&gt;</c>: what a status code means, in the header's
/// name and the binding's one line of English — <see cref="StatusMeaning"/>,
/// the answer <c>ConvertFrom-XmipStatus</c> emits too; only the rendering is
/// here. A code the header does not define is reported as unknown and exits 1.
/// </summary>
public static class StatusCommand
{
    /// <summary>Explain one status code.</summary>
    public static int Run(string code, bool json, TextWriter output, TextWriter error)
    {
        if (!int.TryParse(code, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
            out int value))
        {
            error.WriteLine(
                $"'{code}' is not a number. Status codes are integers, zero or negative.");
            return 2;
        }

        StatusMeaning meaning = StatusMeaning.Of(value);

        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteNumber("code", meaning.Code);
                writer.WriteBoolean("known", meaning.Known);
                writer.WriteString("name", meaning.Name);
                writer.WriteString("meaning", meaning.Meaning);
                writer.WriteBoolean("retryable", meaning.Retryable);
                writer.WriteBoolean("terminal", meaning.Terminal);
            }));

            return meaning.Known ? 0 : 1;
        }

        output.WriteLine($"{meaning.Code,5}  {meaning.Name}");
        output.WriteLine($"       {meaning.Meaning}");

        if (!meaning.Known)
        {
            return 1;
        }

        string retryable = Yes(meaning.Retryable);
        string terminal = Yes(meaning.Terminal);
        output.WriteLine($"       retryable: {retryable}   terminal: {terminal}");

        return 0;
    }

    private static string Yes(bool value)
    {
        return value ? "yes" : "no";
    }
}
