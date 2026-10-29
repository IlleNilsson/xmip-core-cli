using System.Globalization;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip secret</c>: the secrets a node's Xmip Storage keeps, by name,
/// kind and when and by whom set — never a value — and <c>--set</c>, which
/// sets one through the node (ADR-0019, amendment 2026-10-29). A Location
/// names the secret it presents with <c>secret = "&lt;name&gt;"</c>; the value
/// is never in the TOML and never on the command line, where every process
/// on the machine could read it: it is read from <c>--file</c>, or from
/// standard input. One command for the noun, the act an option on it, as
/// <c>Set-XmipSecret</c> takes <c>-List</c>. Setting is exit 0 when it was
/// set, 1 when it was not.
/// </summary>
public static class SecretCommand
{
    private static readonly string[] Headings = ["name", "kind", "set", "set by"];

    /// <summary>List what the node at <paramref name="node"/> keeps.</summary>
    public static int List(
        IOperatorSurface surface, string node, bool json, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        IReadOnlyList<SecretRecord> secrets;

        try
        {
            secrets = surface.Secrets(node);
        }
        catch (InvalidOperationException refused)
        {
            error.WriteLine(json
                ? JsonText.Document(writer => writer.WriteString("refused", refused.Message))
                : refused.Message);
            return 1;
        }

        if (json)
        {
            output.WriteLine(Document(surface.Source, secrets));
            return 0;
        }

        output.WriteLine($"{secrets.Count} secret(s) — {surface.Source}");

        if (secrets.Count > 0)
        {
            TextTable.Write(output, "  ", [Headings, .. secrets.Select(Row)]);
        }

        return 0;
    }

    /// <summary>Set the secret <paramref name="asked"/> names through the node
    /// at <paramref name="node"/>, by <paramref name="who"/>, its value from
    /// its file or <paramref name="input"/>.</summary>
    public static int Set(
        IOperatorSurface surface,
        string node,
        SecretAsk asked,
        TextReader input,
        string who,
        bool json,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(asked);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        string value;

        try
        {
            value = asked.Value(input);
        }
        catch (IOException unread)
        {
            error.WriteLine($"FAILED: the value could not be read: {unread.Message}");
            return 1;
        }

        SecretOperation done = surface.SetSecret(
            node, asked.Name, asked.Kind ?? string.Empty, value, who);
        string said = json
            ? JsonText.Document(writer =>
            {
                writer.WriteString("node", done.Node);
                writer.WriteString("name", done.Name);
                writer.WriteBoolean("applied", done.Applied);
                writer.WriteString("result", done.Result);
            })
            : done.Applied ? $"OK. {done.Result}" : done.Result;

        (done.Applied ? output : error).WriteLine(said);
        return done.Applied ? 0 : 1;
    }

    /// <summary>The secrets as one document, never a value.</summary>
    public static string Document(string source, IReadOnlyList<SecretRecord> secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);

        return JsonText.Document(writer =>
        {
            writer.WriteString("source", source);
            writer.WriteStartArray("secrets");

            foreach (SecretRecord secret in secrets)
            {
                writer.WriteStartObject();
                writer.WriteString("name", secret.Name);
                writer.WriteString("kind", secret.Kind);
                writer.WriteString("set", secret.Set);
                writer.WriteString("set_by", secret.SetBy);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }

    private static string[] Row(SecretRecord secret)
    {
        return
        [
            secret.Name,
            secret.Kind,
            secret.Set.ToString("u", CultureInfo.InvariantCulture),
            secret.SetBy,
        ];
    }
}
