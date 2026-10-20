using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip-cli certificate</c> (ADR-0034, amendment 2026-10-15): the line
/// read — one act, its options on no other command — and the act answered
/// through the runtime this estate built: a key and a request made into a
/// folder, the request signed by an authority a node configuration names,
/// the chain on stdout or in a file, and a refusal in the runtime's words.
/// </summary>
public sealed class CertificateCommandTests
{
    private const string Person = "alice@example.org";

    [Fact]
    public void TheLineNamesOneActAndItsOptionsApplyToNoOtherCommand()
    {
        Invocation made = Parse("certificate", "--operator", Person, "--out", "phone");
        Assert.Equal(Command.Certificate, made.Command);
        Assert.Equal(Person, made.Certificate?.Operator);
        Assert.Equal("phone", made.Certificate?.Out);

        Invocation signing = Parse("certificate", "operator.csr", "--sign", "node.toml");
        Assert.Equal("operator.csr", signing.Argument);
        Assert.Equal("node.toml", signing.Certificate?.Sign);

        Assert.Contains("needs --operator", Refused("certificate"), StringComparison.Ordinal);
        Assert.Contains(
            "not both",
            Refused("certificate", "x.csr", "--operator", Person, "--sign", "n.toml"),
            StringComparison.Ordinal);
        Assert.Contains(
            "takes no argument", Refused("certificate", "x.csr", "--operator", Person),
            StringComparison.Ordinal);
        Assert.Contains(
            "needs the request", Refused("certificate", "--sign", "n.toml"),
            StringComparison.Ordinal);
        Assert.Contains("needs a path", Refused("certificate", "--out"), StringComparison.Ordinal);
        Assert.Equal(
            "--operator and --sign only apply to 'certificate', and --out to "
                + "'certificate' and 'database'.",
            Refused("validate", "node.toml", "--operator", Person));
    }

    [Fact]
    public void ARequestIsMadeAndSignedByTheAuthorityTheConfigurationNames()
    {
        string place = Directory.CreateTempSubdirectory("xmip-cli-certificate-").FullName;

        try
        {
            string configuration = Authority(place);
            string phone = Path.Combine(place, "phone");
            StringWriter output = new();

            Assert.Equal(0, Answer(output, "certificate", "--operator", Person, "--out", phone));
            Assert.Contains("operator.csr", output.ToString(), StringComparison.Ordinal);

            string request = Path.Combine(phone, "operator.csr");
            output = new StringWriter();
            Assert.Equal(0, Answer(output, "certificate", request, "--sign", configuration));
            using X509Certificate2 leaf = X509Certificate2.CreateFromPem(output.ToString());
            Assert.Equal($"CN={Person}", leaf.Subject);

            string chain = Path.Combine(phone, "operator.pem");
            output = new StringWriter();
            Assert.Equal(0, Answer(
                output, "certificate", request, "--sign", configuration, "--out", chain,
                "--json"));
            using JsonDocument document = JsonDocument.Parse(output.ToString());
            Assert.Equal(chain, document.RootElement.GetProperty("written").GetString());

            StringWriter error = new();
            Assert.Equal(1, CertificateCommand.Over(
                Parse("certificate", "--operator", Person, "--out", phone), false,
                new StringWriter(), error));
            Assert.Contains("nothing is overwritten", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(place, recursive: true);
        }
    }

    private static Invocation Parse(params string[] line)
    {
        return Invocation.Parse(line, out string problem)
            ?? throw new InvalidOperationException(problem);
    }

    private static string Refused(params string[] line)
    {
        Assert.Null(Invocation.Parse(line, out string problem));
        return problem;
    }

    private static int Answer(StringWriter output, params string[] line)
    {
        StringWriter error = new();
        int exit = CertificateCommand.Over(Parse(line), Parse(line).Json, output, error);
        Assert.True(exit == 0, error.ToString());
        return exit;
    }

    // An authority kept as xmip-core-provision keeps one, and a node's
    // configuration naming it in [provision.operator]; the configuration.
    private static string Authority(string place)
    {
        string kept = Directory.CreateDirectory(Path.Combine(place, "authority")).FullName;
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest root = new("CN=test authority", key, HashAlgorithmName.SHA256);
        root.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using X509Certificate2 authority = root.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllText(Path.Combine(kept, "authority.pem"), authority.ExportCertificatePem());
        File.WriteAllText(Path.Combine(kept, "authority.key"), key.ExportPkcs8PrivateKeyPem());

        string configuration = Path.Combine(place, "node.toml");
        File.WriteAllText(
            configuration,
            "[service]\nname = \"n\"\ncluster_name = \"c\"\nnode_name = \"d\"\n\n"
                + "[provision.operator]\nauthority = \"authority\"\n");
        return configuration;
    }
}
