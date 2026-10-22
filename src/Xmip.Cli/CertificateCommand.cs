using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip certificate</c>: a person's certificate (ADR-0034, amendment
/// 2026-10-15), a node's from the cluster's authority (ADR-0015, amendment
/// 2026-10-21), the authority itself and a node's certificate carried
/// across an air gap (ADR-0034, amendment 2026-10-22). Running a signing is
/// the approval: whoever may read the authority's key decides, out of band.
/// The files, the words and the rules are <see cref="OperatorCertificate"/>'s,
/// <see cref="NodeCertificate"/>'s, <see cref="AuthorityCertificate"/>'s and
/// the runtime's; this renders them. The command is audited as every
/// command is (<see cref="CommandAudit"/>).
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

    /// <summary>Answer <paramref name="query"/> with the runtime's sections
    /// 19, 22 and 23 in <paramref name="certificates"/>. Exit 1 when the
    /// runtime refuses or a file is there already.</summary>
    public static int Run(
        RuntimeCertificates certificates, CertificateQuery query, string request, bool json,
        TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        CertificateAnswer answer = new(json, output, error);
        string? Full(string? path) => path is null ? null : Path.GetFullPath(path);
        string authority = Full(query.Authority) ?? string.Empty;
        string[] names = (query.Names ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return query switch
        {
            { Inspect: { } inspected } => Inspected(certificates, Full(inspected)!, answer),
            { Revoke: { } serial } => AuthorityAct.Revoked(
                AuthorityCertificate.Revoke(
                    certificates, authority, serial, query.Reason ?? "unspecified"),
                answer),
            { Export: { } path } => AuthorityAct.Written(
                AuthorityCertificate.Export(
                    certificates, authority, Full(path)!, query.Format ?? "pem"),
                answer),
            { Backup: { } folder } => AuthorityAct.Written(
                AuthorityCertificate.BackUp(certificates, authority, Full(folder)!), answer),
            { Import: { } folder } => AuthorityAct.Imported(
                AuthorityCertificate.Import(certificates, Full(request)!, Full(folder)!),
                answer),
            { Node: { } node, Authority: null } => AuthorityAct.Requested(
                AuthorityCertificate.Request(certificates, node, names, Full(query.Out ?? ".")!),
                answer),
            { Node: { } node } => Issued(certificates, query, node, names, answer),
            { Operator: { } person } => Requested(certificates, query, person, answer),
            { Sign: { } configuration } => Signed(certificates, query, request, configuration,
                answer),
            { Authority: not null } when request.Length > 0 => AuthorityAct.Signed(
                AuthorityCertificate.Sign(
                    certificates, authority, Full(request)!, names, Full(query.Out)),
                answer),
            _ => AuthorityAct.Inspected(
                AuthorityCertificate.SetUp(
                    certificates, authority, query.Cluster ?? string.Empty, query.Publish),
                answer, "Set up"),
        };
    }

    // An authority's folder, or a certificate file.
    private static int Inspected(
        RuntimeCertificates certificates, string path, CertificateAnswer answer)
    {
        return File.Exists(path)
            ? AuthorityAct.Read(AuthorityCertificate.Read(certificates, path), answer)
            : AuthorityAct.Inspected(
                AuthorityCertificate.Inspect(certificates, path), answer, "Inspected");
    }

    private static int Issued(
        RuntimeCertificates certificates, CertificateQuery query, string node, string[] names,
        CertificateAnswer answer)
    {
        NodeCertified issued = NodeCertificate.Issue(
            certificates, Path.GetFullPath(query.Authority ?? string.Empty),
            query.Cluster ?? string.Empty, node, names, Path.GetFullPath(query.Out ?? "."));

        return answer.Said(issued.Ok, issued.Refused, writer =>
        {
            writer.WriteString("node", issued.Node);
            writer.WriteString("certificate", issued.Certificate);
            writer.WriteString("key", issued.Key);
            writer.WriteString("anchors", issued.Anchors);
        }, $"Issued {issued.Node} {issued.Certificate}, {issued.Key} and {issued.Anchors}. "
            + "Give the node the three as its [operate] names them; the key is its alone.");
    }

    private static int Requested(
        RuntimeCertificates certificates, CertificateQuery query, string person,
        CertificateAnswer answer)
    {
        OperatorRequested made = OperatorCertificate.Request(
            certificates, person, Path.GetFullPath(query.Out ?? "."));

        return answer.Said(made.Ok, made.Refused, writer =>
        {
            writer.WriteString("person", made.Person);
            writer.WriteString("key", made.Key);
            writer.WriteString("request", made.Request);
        }, $"Made {made.Key} and {made.Request} for {made.Person}. Keep the key; "
            + "send the request to whoever signs for the cluster's authority.");
    }

    private static int Signed(
        RuntimeCertificates certificates, CertificateQuery query, string request,
        string configuration, CertificateAnswer answer)
    {
        OperatorSigned signed = OperatorCertificate.Sign(
            certificates,
            Path.GetFullPath(request),
            Path.GetFullPath(configuration),
            query.Out is { } file ? Path.GetFullPath(file) : null);

        if (signed.Ok && signed.Written is null && !answer.Json)
        {
            answer.Output.Write(signed.Chain);
            return 0;
        }

        return answer.Said(signed.Ok, signed.Refused, writer =>
        {
            writer.WriteString("request", signed.Request);
            writer.WriteString("written", signed.Written);
            writer.WriteString("chain", signed.Chain);
        }, $"Signed {signed.Request}: the chain is {signed.Written}.");
    }
}
