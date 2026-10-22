using System.Globalization;
using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// What <c>xmip certificate</c> says of the cluster's authority and of a
/// node's certificate carried across an air gap (ADR-0034, amendment
/// 2026-10-22): each act's record, rendered once for a person and once for
/// <c>--json</c>. The records are <see cref="AuthorityCertificate"/>'s.
/// </summary>
internal static class AuthorityAct
{
    /// <summary>The authority, and every certificate it signed as a table.
    /// </summary>
    public static int Inspected(AuthorityState state, CertificateAnswer answer, string done)
    {
        return answer.Said(state.Ok, state.Refused, writer =>
        {
            writer.WriteString("authority", state.Authority);
            writer.WriteString("subject", state.Subject);
            writer.WriteString("notAfter", state.NotAfter);
            writer.WriteString("fingerprint", state.Fingerprint);
            writer.WriteString("published", state.Published);
            writer.WriteNumber("listNumber", state.ListNumber);
            writer.WriteString("listNextUpdate", state.ListNextUpdate);
            writer.WriteNumber("revoked", state.Revoked);
            writer.WriteStartArray("issued");

            foreach (AuthorityRecord record in state.Issued)
            {
                writer.WriteStartObject();
                Fields(writer, record);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }, $"{done} {state.Authority}: {state.Subject}, valid until {Day(state.NotAfter)}, "
            + $"SHA-256 {state.Fingerprint}. "
            + (state.Published.Length > 0
                ? $"Its list and certificate are published at {state.Published}. "
                : "It publishes at no address; give one with --publish http://host:port. ")
            + $"{state.Issued.Count} certificate(s) signed, {state.Revoked} revoked.",
            output =>
            {
                List<string[]> rows = [["SERIAL", "SUBJECT", "PURPOSE", "UNTIL", "STANDING"]];
                rows.AddRange(state.Issued.Select(record => new[]
                {
                    record.Serial, record.Subject, record.Purpose, Day(record.NotAfter),
                    record.Standing,
                }));
                TextTable.Write(output, "  ", rows);
            });
    }

    /// <summary>A certificate file's leaf and its standing.</summary>
    public static int Read(CertificateStanding read, CertificateAnswer answer)
    {
        return answer.Said(read.Ok, read.Refused, writer =>
        {
            writer.WriteString("certificate", read.Certificate);
            writer.WriteString("serial", read.Serial);
            writer.WriteString("subject", read.Subject);
            Names(writer, read.Names);
            writer.WriteString("notAfter", read.NotAfter);
            writer.WriteString("renewAt", read.RenewAt);
            writer.WriteString("standing", read.Standing);
        }, $"{read.Certificate}: {read.Subject} ({string.Join(", ", read.Names)}), serial "
            + $"{read.Serial}, valid until {Day(read.NotAfter)}, renewal due "
            + $"{Day(read.RenewAt)}: {read.Standing.ToUpperInvariant()}.");
    }

    /// <summary>A certificate revoked and listed.</summary>
    public static int Revoked(AuthorityRecord record, CertificateAnswer answer)
    {
        return answer.Said(record.Ok, record.Refused, writer => Fields(writer, record),
            $"Revoked {record.Serial} ({record.Subject}) for {record.Reason}; the authority's "
            + "list names it from now.");
    }

    /// <summary>The authority's certificate, its list, or the whole
    /// authority, written out.</summary>
    public static int Written(AuthorityExport written, CertificateAnswer answer)
    {
        return answer.Said(written.Ok, written.Refused, writer =>
        {
            writer.WriteString("written", written.Written);
            writer.WriteString("format", written.Format);
            writer.WriteStartArray("copied");

            foreach (string copied in written.Copied)
            {
                writer.WriteStringValue(copied);
            }

            writer.WriteEndArray();
        }, written.Format == "backup"
            ? $"Backed up the authority into {written.Written}: {written.Copied.Count} "
                + "file(s), its key among them. Keep the copy as the key is kept."
            : $"Wrote {written.Written} ({written.Format}).");
    }

    /// <summary>A node's request, to carry to the authority.</summary>
    public static int Requested(CarriedRequest asked, CertificateAnswer answer)
    {
        return answer.Said(asked.Ok, asked.Refused, writer =>
        {
            writer.WriteString("node", asked.Node);
            writer.WriteString("request", asked.Request);
            writer.WriteString("key", asked.Key);
            Names(writer, asked.Names);
        }, $"Made {asked.Request} for {string.Join(", ", asked.Names)}. Carry it to the "
            + $"machine that keeps the cluster's authority; {asked.Key} stays here.");
    }

    /// <summary>A carried request signed, to carry back.</summary>
    public static int Signed(CarriedSigned signed, CertificateAnswer answer)
    {
        return answer.Said(signed.Ok, signed.Refused, writer =>
        {
            writer.WriteString("request", signed.Request);
            writer.WriteString("serial", signed.Serial);
            Names(writer, signed.Names);
            writer.WriteString("notAfter", signed.NotAfter);
            writer.WriteString("written", signed.Written);
            writer.WriteString("chain", signed.Chain);
        }, $"Signed {signed.Request} for {string.Join(", ", signed.Names)}, serial "
            + $"{signed.Serial}, valid until {Day(signed.NotAfter)}"
            + (signed.Written is { } path
                ? $": carry {path} back to the node."
                : $":{Environment.NewLine}{signed.Chain.TrimEnd()}"));
    }

    /// <summary>A carried chain imported on its node.</summary>
    public static int Imported(CarriedImport imported, CertificateAnswer answer)
    {
        return answer.Said(imported.Ok, imported.Refused, writer =>
        {
            writer.WriteString("certificate", imported.Certificate);
            writer.WriteString("key", imported.Key);
            writer.WriteString("anchors", imported.Anchors);
            writer.WriteString("serial", imported.Serial);
            writer.WriteString("notAfter", imported.NotAfter);
            writer.WriteString("renewAt", imported.RenewAt);
            writer.WriteString("anchorFingerprint", imported.AnchorFingerprint);
            writer.WriteBoolean("anchorTaken", imported.AnchorTaken);
        }, $"Imported {imported.Serial} as {imported.Certificate} and {imported.Key}, valid "
            + $"until {Day(imported.NotAfter)}; its renewal is due {Day(imported.RenewAt)}."
            + (imported.AnchorTaken
                ? $" The node held no anchor: {imported.Anchors} is the authority with SHA-256 "
                    + $"{imported.AnchorFingerprint}; compare it by another way."
                : string.Empty));
    }

    private static void Fields(Utf8JsonWriter writer, AuthorityRecord record)
    {
        writer.WriteString("serial", record.Serial);
        writer.WriteString("subject", record.Subject);
        Names(writer, record.Names);
        writer.WriteString("purpose", record.Purpose);
        writer.WriteString("notBefore", record.NotBefore);
        writer.WriteString("notAfter", record.NotAfter);
        writer.WriteString("renewAt", record.RenewAt);
        writer.WriteString("standing", record.Standing);

        if (record.RevokedAt is { } at)
        {
            writer.WriteString("revokedAt", at);
            writer.WriteString("reason", record.Reason);
        }
    }

    private static void Names(Utf8JsonWriter writer, IReadOnlyList<string> names)
    {
        writer.WriteStartArray("names");

        foreach (string name in names)
        {
            writer.WriteStringValue(name);
        }

        writer.WriteEndArray();
    }

    private static string Day(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
