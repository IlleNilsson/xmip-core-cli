using System.Text.Json;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip-cli database</c> (open problem 41; ADR-0015, amendment
/// 2026-10-20): the line read — a server, <c>--from</c> required, versions as
/// whole numbers, <c>--out</c> shared with <c>certificate</c> and the act's
/// other options on no other command — and the act answered through the
/// runtime this estate built. Until the first release the schema is version
/// 1, so the runtime refuses every upgrade in its words; what an upgrade
/// writes is <c>xmip-core-persist</c>'s and tested there.
/// </summary>
public sealed class DatabaseCommandTests
{
    [Fact]
    public void TheLineNamesTheServerTheVersionsAndTheFolder()
    {
        Invocation asked = Parse("database", "postgresql", "--from", "1", "--out", "upgrade");
        Assert.Equal(Command.Database, asked.Command);
        Assert.Equal(new DatabaseQuery("postgresql", 1, 0, "upgrade"), asked.Database);
        Assert.Null(asked.Certificate);
        Assert.Null(asked.Audit);

        Invocation named = Parse("database", "mssql", "--from", "2", "--to", "3");
        Assert.Equal(new DatabaseQuery("mssql", 2, 3, null), named.Database);
    }

    [Fact]
    public void ALineThatCannotBeObeyedIsRefusedInWords()
    {
        Assert.Contains("needs --from", Refused("database", "mariadb"), StringComparison.Ordinal);
        Assert.Contains(
            "a whole number from 1, not 'one'",
            Refused("database", "mariadb", "--from", "one"),
            StringComparison.Ordinal);
        Assert.Contains(
            "--to needs a schema version",
            Refused("database", "mariadb", "--from", "1", "--to", "0"),
            StringComparison.Ordinal);
        Assert.Contains(
            "exactly one argument", Refused("database", "--from", "1"), StringComparison.Ordinal);
        Assert.Contains(
            "--operator and --sign only apply to 'certificate'",
            Refused("database", "mariadb", "--from", "1", "--operator", "alice@example.org"),
            StringComparison.Ordinal);
        Assert.Equal(
            "--severity only applies to 'audit'.",
            Refused("database", "mariadb", "--from", "1", "--severity", "Error"));
    }

    [Fact]
    public void TheRuntimeRefusesAnUpgradeThatIsNoneAndNothingIsWritten()
    {
        string place = Path.Combine(
            Path.GetTempPath(), $"xmip-cli-database-{Guid.NewGuid():n}");
        StringWriter output = new();
        StringWriter error = new();
        Invocation asked = Parse("database", "postgresql", "--from", "1", "--out", place, "--json");

        int exit = DatabaseCommand.Over(asked, asked.Json, output, error);

        Assert.Equal(1, exit);
        Assert.Empty(output.ToString());
        using JsonDocument refused = JsonDocument.Parse(error.ToString());
        string said = refused.RootElement.GetProperty("refused").GetString() ?? string.Empty;
        Assert.Contains("is none", said, StringComparison.Ordinal);
        Assert.False(Directory.Exists(place), "nothing written");
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
}
