namespace Xmip.Cli;

/// <summary>What <c>xmip certificate</c> was asked
/// (<see cref="CertificateArguments"/>): the person a request is made for,
/// the node configuration whose authority signs one, the cluster's
/// authority and what is done with it, and where the files go; each null
/// where the line names none.</summary>
public sealed record CertificateQuery
{
    /// <summary>The person a key and request are made for:
    /// <c>--operator</c>.</summary>
    public string? Operator { get; init; }

    /// <summary>The node configuration whose <c>[provision.operator]</c>
    /// names the authority that signs: <c>--sign</c>.</summary>
    public string? Sign { get; init; }

    /// <summary>The folder a request is made into, the file a chain is
    /// written to, or the folder a node's files are written into:
    /// <c>--out</c>.</summary>
    public string? Out { get; init; }

    /// <summary>The node a key and certificate are issued to:
    /// <c>--node</c>.</summary>
    public string? Node { get; init; }

    /// <summary>The cluster that node is of: <c>--cluster</c>.</summary>
    public string? Cluster { get; init; }

    /// <summary>The folder the cluster's authority is kept in:
    /// <c>--authority</c>.</summary>
    public string? Authority { get; init; }

    /// <summary>Every name the node is reached by besides its own, comma
    /// separated: <c>--names</c>.</summary>
    public string? Names { get; init; }
    /// <summary>The plain-HTTP address the authority publishes its list
    /// and certificate at, set up with <c>--authority</c> and
    /// <c>--cluster</c>: <c>--publish</c>.</summary>
    public string? Publish { get; init; }

    /// <summary>The authority's folder, or a certificate file, to inspect:
    /// <c>--inspect</c>.</summary>
    public string? Inspect { get; init; }

    /// <summary>The serial of a certificate the authority signed, to
    /// revoke: <c>--revoke</c>.</summary>
    public string? Revoke { get; init; }

    /// <summary>Why, an RFC 5280 word: <c>--reason</c>.</summary>
    public string? Reason { get; init; }

    /// <summary>The file the authority's certificate or list is written
    /// to: <c>--export</c>.</summary>
    public string? Export { get; init; }

    /// <summary>pem, der or crl: <c>--format</c>.</summary>
    public string? Format { get; init; }

    /// <summary>The empty folder the whole authority is copied into:
    /// <c>--backup</c>.</summary>
    public string? Backup { get; init; }

    /// <summary>The node's folder a carried chain is imported into:
    /// <c>--import</c>.</summary>
    public string? Import { get; init; }

    /// <summary>The acts the line names, each by its option; exactly one
    /// is obeyed.</summary>
    public IReadOnlyList<string> Acts =>
        new (string? Value, string Option)[]
        {
            (Operator, "--operator"), (Sign, "--sign"), (Node, "--node"),
            (Inspect, "--inspect"), (Revoke, "--revoke"), (Export, "--export"),
            (Backup, "--backup"), (Import, "--import"),
        }.Where(one => one.Value is not null).Select(one => one.Option).ToArray();
}
