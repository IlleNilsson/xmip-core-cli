namespace Xmip.Cli;

/// <summary>
/// What <c>xmip-cli certificate</c> takes (ADR-0034, amendment 2026-10-15):
/// <c>--operator &lt;person&gt;</c> to make a key and a request, or a
/// request as the argument and <c>--sign &lt;configuration&gt;</c> to sign
/// it by the authority that node configuration names; <c>--out</c> where
/// the files go. One of the two acts, never both, and none of the three
/// options on another command.
/// </summary>
public static class CertificateArguments
{
    /// <summary>Every option, as typed.</summary>
    public static IReadOnlyList<string> Options { get; } = ["--operator", "--sign", "--out"];

    /// <summary>
    /// Take the option at <paramref name="index"/> and its value. False when
    /// it is none of <see cref="Options"/>; true when it is, with
    /// <paramref name="problem"/> saying why the line cannot be obeyed where
    /// it has no value.
    /// </summary>
    public static bool Take(
        IReadOnlyList<string> args, ref int index, ref CertificateQuery? query,
        out string? problem)
    {
        ArgumentNullException.ThrowIfNull(args);

        string option = args[index];
        problem = null;

        if (!Options.Contains(option, StringComparer.Ordinal))
        {
            return false;
        }

        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            problem = option == "--operator"
                ? "--operator needs the person's name."
                : $"{option} needs a path.";
            return true;
        }

        string value = args[++index];
        CertificateQuery taken = query ?? new CertificateQuery();
        query = option switch
        {
            "--operator" => taken with { Operator = value },
            "--sign" => taken with { Sign = value },
            _ => taken with { Out = value },
        };
        return true;
    }

    /// <summary>
    /// Why the line cannot be obeyed, or null: an option of
    /// <c>certificate</c> on another command; <c>certificate</c> without one
    /// act, or with both; a person with a request named, or a signing with
    /// none.
    /// </summary>
    public static string? Refusal(Command command, CertificateQuery? query, bool named)
    {
        if (command is not Command.Certificate)
        {
            return query is null
                ? null
                : "--operator, --sign and --out only apply to 'certificate'.";
        }

        return query switch
        {
            { Operator: not null, Sign: not null } =>
                "'certificate' takes --operator or --sign, not both.",
            { Operator: not null } when named =>
                "'certificate --operator <person>' takes no argument: the request is made.",
            { Sign: not null } when !named =>
                "'certificate <request> --sign <configuration>' needs the request.",
            { Operator: not null } or { Sign: not null } => null,
            _ => "'certificate' needs --operator <person> to make a request, or "
                + "<request> --sign <configuration> to sign one.",
        };
    }
}
