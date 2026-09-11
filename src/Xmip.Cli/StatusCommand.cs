using System.Globalization;
using Xmip.Abi.Module;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip status &lt;code&gt;</c>: what a status code means, in the header's
/// name and the binding's one line of English. A code the header does not
/// define is reported as unknown and exits 1.
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

        XmipStatus status = (XmipStatus)value;
        bool known = Enum.IsDefined(status);

        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteNumber("code", value);
                writer.WriteBoolean("known", known);
                writer.WriteString("name", known ? status.ToString() : "unknown");
                writer.WriteString("meaning", status.Explain());
                writer.WriteBoolean("retryable", known && status.IsRetryable());
                writer.WriteBoolean("terminal", known && status.IsTerminal());
            }));

            return known ? 0 : 1;
        }

        output.WriteLine($"{value,5}  {(known ? status.ToString() : "unknown")}");
        output.WriteLine($"       {status.Explain()}");

        if (!known)
        {
            return 1;
        }

        string retryable = Yes(status.IsRetryable());
        string terminal = Yes(status.IsTerminal());
        output.WriteLine($"       retryable: {retryable}   terminal: {terminal}");

        return 0;
    }

    private static string Yes(bool value)
    {
        return value ? "yes" : "no";
    }
}
