using System.Text;

namespace Xmip.Cli;

/// <summary>
/// The console's error stream, kept as it is written: what a command said
/// on stderr reaches the operator as before, and its audit record carries the
/// same words (ADR-0062 clause 4: a failure is never only on a screen).
/// </summary>
/// <param name="console">The writer the words go to.</param>
public sealed class EchoedWriter(TextWriter console) : TextWriter
{
    private readonly StringBuilder kept = new();

    /// <inheritdoc/>
    public override Encoding Encoding => console.Encoding;

    /// <summary>Everything written so far.</summary>
    public string Said
    {
        get
        {
            lock (kept)
            {
                return kept.ToString();
            }
        }
    }

    /// <inheritdoc/>
    public override void Write(char value)
    {
        console.Write(value);

        lock (kept)
        {
            kept.Append(value);
        }
    }

    /// <inheritdoc/>
    public override void Write(string? value)
    {
        console.Write(value);

        lock (kept)
        {
            kept.Append(value);
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        console.Flush();
    }
}
