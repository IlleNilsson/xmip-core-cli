namespace Xmip.Cli;

/// <summary>
/// What <c>xmip secret</c> takes (ADR-0019, amendment 2026-10-29):
/// <c>--set</c> with the secret's name as the command's argument,
/// <c>--kind</c> password, key or certificate, and <c>--file</c> for the
/// value — standard input where it names none, never the command line;
/// with neither a name nor an act the command lists. The node it is set
/// through is the shared <c>--location</c>, the one the surface reaches
/// where it names none.
/// </summary>
public static class SecretArguments
{
    /// <summary>The options only <c>secret</c> takes.</summary>
    public static IReadOnlyList<string> Options { get; } = ["--kind", "--file"];

    /// <summary>The audit options <c>secret</c> shares: where it is set.</summary>
    public static IReadOnlyList<string> Shared { get; } = ["--location"];

    /// <summary>
    /// Take <c>--kind</c> or <c>--file</c> at <paramref name="index"/>, and its
    /// value; false where the option is another. A problem where its value
    /// is missing.
    /// </summary>
    public static bool Take(
        IReadOnlyList<string> args, ref int index, ref SecretAsk? asked, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(args);

        problem = null;
        string option = args[index];

        if (!Options.Contains(option, StringComparer.Ordinal))
        {
            return false;
        }

        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            problem = option == "--kind"
                ? "--kind needs a kind: password, key or certificate."
                : "--file needs the path of the file holding the value.";
            return true;
        }

        string value = args[++index];
        asked ??= new SecretAsk();
        asked = option == "--kind" ? asked with { Kind = value } : asked with { File = value };
        return true;
    }

    /// <summary>
    /// What the line asks of <c>secret</c>: a listing, or a set of the secret
    /// <paramref name="name"/>; null with <paramref name="problem"/> saying
    /// why for a set with no name or kind, a name with no act, or an act it
    /// does not take.
    /// </summary>
    public static SecretAsk? Ask(string? act, string name, SecretAsk? asked, out string? problem)
    {
        problem = null;
        SecretAsk ask = asked ?? new SecretAsk();

        if (act is null)
        {
            problem = name.Length > 0 || ask.Kind is not null || ask.File is not null
                ? "'secret <name>' needs --set, --kind and the value from --file or standard "
                    + "input; 'secret' alone lists."
                : null;
            return problem is null ? ask : null;
        }

        if (act != "set")
        {
            problem = $"REFUSED: --{act}: a secret is set, and --set is its act.";
            return null;
        }

        if (name.Length == 0 || ask.Kind is null)
        {
            problem = "REFUSED: to set a secret, name it and its kind: "
                + "'secret <name> --set --kind password|key|certificate'.";
            return null;
        }

        return ask with { Name = name, Set = true };
    }
}

/// <summary>What <c>xmip secret</c> was asked: to set the secret
/// <paramref name="Name"/> of <paramref name="Kind"/>, its value from
/// <paramref name="File"/> or standard input, or, where <paramref name="Set"/>
/// is false, to list.</summary>
public sealed record SecretAsk(
    string Name = "", string? Kind = null, string? File = null, bool Set = false)
{
    /// <summary>The value: the file's text, or what standard input holds,
    /// its line end taken off.</summary>
    public string Value(TextReader input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return File is { } path
            ? System.IO.File.ReadAllText(path)
            : input.ReadToEnd().TrimEnd('\r', '\n');
    }
}
