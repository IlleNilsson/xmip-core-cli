namespace Xmip.Cli;

/// <summary>
/// What <c>xmip certificate</c> takes. A person's certificate (ADR-0034,
/// amendment 2026-10-15): <c>--operator &lt;person&gt;</c> makes a key and a
/// request, a request as the argument with <c>--sign
/// &lt;configuration&gt;</c> signs it. A node's: <c>--node &lt;node&gt;</c>
/// with <c>--cluster</c> and <c>--authority</c> issues it a key and
/// certificate from the cluster's authority (ADR-0015, amendment
/// 2026-10-21); without <c>--authority</c> it makes the node's request to
/// carry across an air gap, a carried request as the argument with
/// <c>--authority</c> signs it, and a carried chain as the argument with
/// <c>--import &lt;folder&gt;</c> imports it. The cluster's authority
/// (ADR-0034, amendment 2026-10-22): <c>--authority</c> with
/// <c>--cluster</c> and no node sets it up, <c>--publish</c> the address it
/// publishes at; <c>--inspect</c>, <c>--revoke</c>, <c>--export</c> and
/// <c>--backup</c> act on it. One act a line, and none of these options on
/// another command but <c>--out</c> on <c>database</c>
/// (<see cref="DatabaseArguments"/>).
/// </summary>
public static class CertificateArguments
{
    /// <summary>Every option, as typed.</summary>
    public static IReadOnlyList<string> Options { get; } =
    [
        "--operator", "--sign", "--out", "--node", "--cluster", "--authority", "--names",
        "--publish", "--inspect", "--revoke", "--reason", "--export", "--format", "--backup",
        "--import",
    ];

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
                "--publish" => "--publish needs the address, http://host:port.",
                "--revoke" => "--revoke needs the certificate's serial.",
                "--reason" => "--reason needs a word: keyCompromise, superseded, ...",
                "--format" => "--format needs pem, der or crl.",
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
            "--publish" => taken with { Publish = value },
            "--inspect" => taken with { Inspect = value },
            "--revoke" => taken with { Revoke = value },
            "--reason" => taken with { Reason = value },
            "--export" => taken with { Export = value },
            "--format" => taken with { Format = value },
            "--backup" => taken with { Backup = value },
            "--import" => taken with { Import = value },
            _ => taken with { Out = value },
        };
        return true;
    }

    /// <summary>
    /// Why the line cannot be obeyed, or null: an option of
    /// <c>certificate</c> on another command; <c>certificate</c> naming no
    /// act or two; an act without what it needs, or with what it does not
    /// take.
    /// </summary>
    public static string? Refusal(Command command, CertificateQuery? query, bool named)
    {
        if (command is not Command.Certificate)
        {
            // --out is database's too: the folder its scripts are written to.
            return query is null || (command is Command.Database
                && query with { Out = null } == new CertificateQuery())
                ? null
                : "The options of 'certificate' only apply to 'certificate', and --out to "
                    + "'certificate' and 'database'.";
        }

        CertificateQuery asked = query ?? new CertificateQuery();
        IReadOnlyList<string> acts = asked.Acts;

        if (acts.Count > 1)
        {
            return $"'certificate' takes one act a line; {string.Join(" and ", acts)} given.";
        }

        return (acts.Count > 0 ? acts[0] : null) switch
        {
            "--operator" when named =>
                "'certificate --operator <person>' takes no argument: the request is made.",
            "--sign" when !named =>
                "'certificate <request> --sign <configuration>' needs the request.",
            "--node" when named =>
                "'certificate --node <node>' takes no argument.",
            "--node" when asked.Authority is not null && asked.Cluster is null =>
                "'certificate --node <node> --authority <folder>' needs --cluster <cluster>.",
            "--revoke" or "--export" or "--backup" when asked.Authority is null =>
                $"'certificate {acts[0]}' needs --authority <folder>.",
            "--import" when !named =>
                "'certificate <chain> --import <folder>' needs the chain the authority signed.",
            "--inspect" or "--revoke" or "--export" or "--backup" when named =>
                $"'certificate {acts[0]}' takes no argument.",
            null when asked.Authority is null =>
                "'certificate' needs an act: --operator, --sign, --node, --inspect, --revoke, "
                    + "--export, --backup or --import, a carried request with --authority, or "
                    + "--authority with --cluster to set the authority up.",
            null when !named && asked.Cluster is null =>
                "'certificate --authority <folder>' needs --cluster <cluster> to set it up, "
                    + "or a carried request to sign.",
            null when named && asked.Cluster is not null =>
                "'certificate <request> --authority <folder>' signs a carried request and "
                    + "takes no --cluster.",
            _ => null,
        };
    }
}
