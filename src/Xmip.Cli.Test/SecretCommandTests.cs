using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip secret</c> (ADR-0019, amendment 2026-10-29): listing with no
/// name, <c>--set</c> on the secret the argument names with its
/// <c>--kind</c> and the value from <c>--file</c> or standard input — never
/// the command line — and no other noun takes <c>--set</c>, nor a secret
/// another noun's act. A surface that reaches no node refuses both, in
/// words.
/// </summary>
public sealed class SecretCommandTests : IDisposable
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's first node, by its place.
    private static readonly string Node = $"{Cluster.Scope}/node/{Cluster.Nodes[0]}";

    private readonly string _place = Path.Combine(
        Path.GetTempPath(), $"xmip-secret-{Guid.NewGuid():N}");

    public SecretCommandTests()
    {
        Directory.CreateDirectory(_place);
    }

    public void Dispose()
    {
        Directory.Delete(_place, recursive: true);
    }

    private static Invocation Line(params string[] args)
    {
        Invocation? parsed = Invocation.Parse(args, out string problem);
        Assert.True(parsed is not null, problem);
        return parsed;
    }

    private static string Refused(params string[] args)
    {
        Assert.Null(Invocation.Parse(args, out string problem));
        return problem;
    }

    [Fact]
    public void TheSecretIsTheArgumentItsKindAndFileOptionsAndSetTheAct()
    {
        Invocation parsed = Line(
            "secret", "partner-x", "--set", "--kind", "key", "--file", "key.pem",
            "--location", Node, "--who", "ilian");

        Assert.Equal(Command.Secret, parsed.Command);
        Assert.Equal(new SecretAsk("partner-x", "key", "key.pem", true), parsed.Secret);
        Assert.Equal(Node, parsed.Location);
        Assert.Equal(new SecretAsk(), Line("secret").Secret);
    }

    [Fact]
    public void ALineThatWouldSetNothingOrPutAValueOnTheCommandLineIsRefused()
    {
        Assert.Contains("needs --set", Refused("secret", "partner-x"));
        Assert.Contains("name it and its kind", Refused("secret", "--set"));
        Assert.Contains("name it and its kind", Refused("secret", "partner-x", "--set"));
        Assert.Contains("--set only applies to 'secret'", Refused("message", "j-1", "--set"));
        Assert.Contains("only applies to 'secret'", Refused("health", "x", "--kind", "key"));
        Assert.Contains("--set is its act", Refused("secret", "partner-x", "--pause"));
        Assert.Contains("only applies to 'message'", Refused("secret", "partner-x", "--retry"));
        Assert.Contains("not an xmip option", Refused("secret", "partner-x", "--value", "x"));
    }

    [Fact]
    public void TheValueIsReadFromTheFileOrStandardInputAndNeverSaid()
    {
        string drawn = Guid.NewGuid().ToString("N");
        string file = Path.Combine(_place, "value.txt");
        File.WriteAllText(file, drawn);

        Assert.Equal(drawn, new SecretAsk("partner-x", "password", file, true)
            .Value(TextReader.Null));
        Assert.Equal(drawn, new SecretAsk("partner-x", "password", null, true)
            .Value(new StringReader(drawn + "\n")));

        FakeSurface surface = new([]);
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = SecretCommand.Set(
            surface, string.Empty, new SecretAsk("partner-x", "password", null, true),
            new StringReader(drawn), "ilian", false, output, error);

        Assert.Equal(1, exit);
        Assert.Contains("reaches no node to set a secret", error.ToString());
        Assert.DoesNotContain(drawn, error.ToString() + output.ToString());
        Assert.Equal(1, SecretCommand.List(surface, string.Empty, true, output, error));
        Assert.Contains("lists no secrets", error.ToString());
    }

    [Fact]
    public void AListingSaysEachSecretByNameKindAndWhenNeverAValue()
    {
        SecretRecord one = new("partner-x", "key", DateTimeOffset.UnixEpoch, "CN=ilian");
        string document = SecretCommand.Document("FAKE", [one]);

        Assert.Contains("\"name\":\"partner-x\"", document, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"key\"", document, StringComparison.Ordinal);
        Assert.DoesNotContain("value", document, StringComparison.Ordinal);
    }
}
