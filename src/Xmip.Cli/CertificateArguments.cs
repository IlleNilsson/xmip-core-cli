namespace Xmip.Cli;

/// <summary>
/// What <c>xmip-cli certificate</c> takes (ADR-0034, amendment 2026-10-15):
/// <c>--operator &lt;person&gt;</c> to make a key and a request, or a
/// request as the argument and <c>--sign &lt;configuration&gt;</c> to sign
/// it by the authority that node configuration names; or <c>--node
/// &lt;node&gt;</c> with <c>--cluster</c>, <c>--authority</c> and
/// <c>--names</c> to issue a node its key and certificate from the
/// cluster's authority; <c>--out</c> where the files go. One of the three
/// acts, and none of their options on another command but <c>--out</c> on
/// <c>database</c>, the folder its scripts are written to
/// (<see cref="DatabaseArguments"/>).
/// </summary>
public static class CertificateArguments
{
    /// <summary>Every option, as typed.</summary>
    public static IReadOnlyList<string> Options { get; } =
        ["--operator", "--sign", "--out", "--node", "--cluster", "--authority", "--names"];

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
            problem = option switch
            {
                "--operator" => "--operator needs the person's name.",
                "--node" or "--cluster" => $"{option} needs a name.",
                "--names" => "--names needs the names, comma separated.",
                _ => $"{option} needs a path.",
            };
            return true;
        }

        string value = args[++index];
        CertificateQuery taken = query ?? new CertificateQuery();
        query = option switch
        {
            "--operator" => taken with { Operator = value },
            "--sign" => taken with { Sign = value },
            "--node" => taken with { Node = value },
            "--cluster" => taken with { Cluster = value },
            "--authority" => taken with { Authority = value },
            "--names" => taken with { Names = value },
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
            // --out is database's too: the folder its scripts are written to.
            return query is null || (command is Command.Database
                && query is
                {
                    Operator: null, Sign: null, Node: null, Cluster: null, Authority: null,
                    Names: null,
                })
                ? null
                : "--operator, --sign, --node, --cluster, --authority and --names only apply "
                    + "to 'certificate', and --out to 'certificate' and 'database'.";
        }

        return query switch
        {
            { Node: not null, Operator: null, Sign: null } when named =>
                "'certificate --node <node>' takes no argument: the node is issued.",
            { Node: not null, Cluster: null } or { Node: not null, Authority: null } =>
                "'certificate --node <node>' needs --cluster <cluster> and --authority <folder>.",
            { Node: not null, Operator: null, Sign: null } => null,
            { Node: not null } =>
                "'certificate' takes --operator, --sign or --node, one of them.",
            { Cluster: not null } or { Authority: not null } or { Names: not null } =>
                "--cluster, --authority and --names go with 'certificate --node <node>'.",
            { Operator: not null, Sign: not null } =>
                "'certificate' takes --operator or --sign, not both.",
            { Operator: not null } when named =>
                "'certificate --operator <person>' takes no argument: the request is made.",
            { Sign: not null } when !named =>
                "'certificate <request> --sign <configuration>' needs the request.",
            { Operator: not null } or { Sign: not null } => null,
            _ => "'certificate' needs --operator <person> to make a request, "
                + "<request> --sign <configuration> to sign one, or --node <node> to issue "
                + "a node its certificate.",
        };
    }
}
