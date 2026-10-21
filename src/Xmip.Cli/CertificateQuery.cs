namespace Xmip.Cli;

/// <summary>What <c>xmip-cli certificate</c> was asked
/// (<see cref="CertificateArguments"/>): the person a request is made for,
/// the node configuration whose authority signs one, and where the files
/// go; each null where the line names none.</summary>
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
}
