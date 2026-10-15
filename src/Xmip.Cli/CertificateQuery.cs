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

    /// <summary>The folder a request is made into, or the file a chain is
    /// written to: <c>--out</c>.</summary>
    public string? Out { get; init; }
}
