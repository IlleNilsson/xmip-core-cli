using System.Text.Json;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip database &lt;server&gt; --from &lt;version&gt;</c>: the scripts
/// that bring each database on a server — <c>postgresql</c>, <c>mssql</c> or
/// <c>mariadb</c> — from the schema version it holds to this build's, or to
/// <c>--to</c>, adding only (open problem 41; ADR-0015, amendment
/// 2026-10-20). Written into <c>--out</c>, or printed, each under its name.
/// The schema and the scripts are the runtime's
/// (<see cref="RuntimeDatabases"/>), and writing them
/// <see cref="DatabaseScripts"/>'s; this renders them.
/// </summary>
public static class DatabaseCommand
{
    /// <summary>Answer <paramref name="invocation"/> through the runtime
    /// library it states (<see cref="SurfaceOpen.Runtime"/>), with no node
    /// behind it. Exit 1 when the library cannot be loaded or is a remote
    /// build.</summary>
    public static int Over(
        Invocation invocation, bool json, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(error);

        _ = SurfaceOpen.Runtime(invocation);

        try
        {
            return Run(
                RuntimeLibrary.Rules.Databases,
                invocation.Database ?? new DatabaseQuery(invocation.Argument, 0, 0, null),
                json,
                output,
                error);
        }
        catch (InvalidOperationException unanswered)
        {
            error.WriteLine(unanswered.Message);
            return 1;
        }
    }

    /// <summary>Answer <paramref name="query"/> with the runtime's section 20
    /// in <paramref name="databases"/>. Exit 1 when the runtime refuses or a
    /// script is there already.</summary>
    public static int Run(
        RuntimeDatabases databases, DatabaseQuery query, bool json, TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(databases);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        DatabaseUpgrade upgrade = databases.Upgrade(query.Server, query.From, query.To);
        string refused = upgrade.Refused ?? string.Empty;
        IReadOnlyList<string>? written = upgrade.Ok && query.Out is { } folder
            ? DatabaseScripts.Write(upgrade, Path.GetFullPath(folder), out refused)
            : null;

        if (!upgrade.Ok || (query.Out is not null && written is null))
        {
            error.WriteLine(json
                ? JsonText.Document(writer => writer.WriteString("refused", refused))
                : refused);
            return 1;
        }

        if (json)
        {
            output.WriteLine(JsonText.Document(writer => Document(writer, upgrade, written)));
            return 0;
        }

        foreach ((DatabaseScript script, int at) in upgrade.Scripts.Select((s, i) => (s, i)))
        {
            output.WriteLine(written is null
                ? $"-- {script.Name}\n{script.Text}"
                : $"Wrote {written[at]}");
        }

        return 0;
    }

    private static void Document(
        Utf8JsonWriter writer, DatabaseUpgrade upgrade, IReadOnlyList<string>? written)
    {
        writer.WriteString("server", upgrade.Server);
        writer.WriteNumber("version", upgrade.Version);
        writer.WriteStartArray("scripts");

        foreach ((DatabaseScript script, int at) in upgrade.Scripts.Select((s, i) => (s, i)))
        {
            writer.WriteStartObject();
            writer.WriteString("name", script.Name);

            if (written is null)
            {
                writer.WriteString("text", script.Text);
            }
            else
            {
                writer.WriteString("written", written[at]);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
