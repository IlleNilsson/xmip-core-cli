using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip-cli certificate</c>: a person's certificate (ADR-0034, amendment
/// 2026-10-15). <c>--operator &lt;person&gt;</c> makes a key and a request
/// into <c>--out</c>, the current folder unless said; <c>&lt;request&gt;
/// --sign &lt;configuration&gt;</c> signs the request by the authority that
/// node configuration names in <c>[provision.operator]</c>, the chain to
/// <c>--out</c> or to stdout. Running the signing is the approval: whoever
/// may read the authority's key decides, out of band. The files, the words
/// and the rules are <see cref="OperatorCertificate"/>'s and the runtime's;
/// this renders them.
/// </summary>
public static class CertificateCommand
{
    /// <summary>Answer <paramref name="invocation"/> through the runtime
    /// library it states (<see cref="SurfaceOpen.Runtime"/>), with no node
    /// behind it. Exit 1 when the library cannot be loaded, or is a remote
    /// build asked to sign.</summary>
    public static int Over(
        Invocation invocation, bool json, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(error);

        _ = SurfaceOpen.Runtime(invocation);

        try
        {
            return Run(
                RuntimeLibrary.Rules.Certificates, invocation.Certificate ?? new(),
                invocation.Argument, json, output, error);
        }
        catch (InvalidOperationException unanswered)
        {
            error.WriteLine(unanswered.Message);
            return 1;
        }
    }

    /// <summary>Answer <paramref name="query"/> with the runtime's section 19
    /// in <paramref name="certificates"/>. Exit 1 when the runtime refuses or
    /// a file is there already.</summary>
    public static int Run(
        RuntimeCertificates certificates, CertificateQuery query, string request, bool json,
        TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (query.Operator is { } person)
        {
            OperatorRequested made = OperatorCertificate.Request(
                certificates, person, Path.GetFullPath(query.Out ?? "."));

            return Said(made.Ok, made.Refused, json, output, error, writer =>
            {
                writer.WriteString("person", made.Person);
                writer.WriteString("key", made.Key);
                writer.WriteString("request", made.Request);
            }, $"Made {made.Key} and {made.Request} for {made.Person}. Keep the key; "
                + "send the request to whoever signs for the cluster's authority.");
        }

        OperatorSigned signed = OperatorCertificate.Sign(
            certificates,
            Path.GetFullPath(request),
            Path.GetFullPath(query.Sign ?? string.Empty),
            query.Out is { } file ? Path.GetFullPath(file) : null);

        if (signed.Ok && signed.Written is null && !json)
        {
            output.Write(signed.Chain);
            return 0;
        }

        return Said(signed.Ok, signed.Refused, json, output, error, writer =>
        {
            writer.WriteString("request", signed.Request);
            writer.WriteString("written", signed.Written);
            writer.WriteString("chain", signed.Chain);
        }, $"Signed {signed.Request}: the chain is {signed.Written}.");
    }

    // One answer: the document for --json, a sentence for a person, a
    // refusal on the error writer.
    private static int Said(
        bool ok, string refused, bool json, TextWriter output, TextWriter error,
        Action<System.Text.Json.Utf8JsonWriter> fields, string sentence)
    {
        if (!ok)
        {
            error.WriteLine(json
                ? JsonText.Document(writer => writer.WriteString("refused", refused))
                : refused);
            return 1;
        }

        output.WriteLine(json ? JsonText.Document(fields) : sentence);
        return 0;
    }
}
