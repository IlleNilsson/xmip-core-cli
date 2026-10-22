using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// The resources <c>xmip mcp</c> offers (ADR-0071): every scope is an
/// Xmip URI (ADR-0027 clause 3), and so a resource. The list is the scopes the
/// surface publishes; the templates are the scope tree's shape; and reading
/// one is how an operator drills — <c>show</c> for its row, <c>list</c> for
/// what is beneath it — answered by <see cref="CommandRun"/> through
/// <see cref="McpServer.AnsweredAsync"/>, so a read is the two commands, and
/// is audited as them.
/// </summary>
public static class McpResources
{
    private const string Json = "application/json";

    /// <summary>The answer to <c>resources/list</c>: every scope the surface
    /// the session states publishes, in ordinal order.</summary>
    public static Task<string> ListAsync(McpServer server, JsonElement id)
    {
        ArgumentNullException.ThrowIfNull(server);

        IOperatorSurface? surface = server.Surface(out string reason);
        using IDisposable? release = surface as IDisposable;

        if (surface is null)
        {
            return Task.FromResult(McpServer.Error(id, McpServer.Code.Internal, reason));
        }

        IReadOnlyList<string> scopes = surface.Index().Matching("*");

        return Task.FromResult(McpServer.Result(id, writer =>
        {
            writer.WriteStartArray("resources");

            foreach (string scope in scopes)
            {
                writer.WriteStartObject();
                writer.WriteString("uri", scope);
                writer.WriteString("name", scope);
                writer.WriteString("mimeType", Json);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }));
    }

    /// <summary>
    /// The <c>resourceTemplates</c> of <c>resources/templates/list</c>: the
    /// scope tree as it is built — a cluster, a node of it
    /// (<c>xmip:///&lt;cluster&gt;/node/&lt;node&gt;</c>, ADR-0053) and the
    /// path beneath a node, whose stages are the runtime's stage words
    /// (<see cref="ScopeTree.Stages"/>). The artifact segments of ADR-0027's
    /// amendment of 2026-10-06 are decided and not built; the templates follow
    /// the tree when they are.
    /// </summary>
    public static void WriteTemplates(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartArray("resourceTemplates");
        Template(writer, "xmip:///{cluster}", "cluster", "A cluster: every node beneath it.");
        Template(
            writer,
            "xmip:///{cluster}/node/{node}",
            "node",
            "A node of a cluster: everything it runs.");
        Template(
            writer,
            "xmip:///{cluster}/node/{node}/{+path}",
            "beneath a node",
            $"What a node runs, as its path beneath the node: a stage ({Stages()}) and "
                + "what runs there, and the rest the node publishes; list a node to see it.");
        writer.WriteEndArray();
    }

    /// <summary>
    /// The answer to <c>resources/read</c>: the scope's row, as <c>show</c>
    /// gives it, and the rows beneath it, as <c>list</c> does, each one JSON
    /// document under the scope's URI. A URI is one scope: a wildcard in it
    /// is not a resource. A scope with nothing at it is not found, in the
    /// words <c>show</c> says it with.
    /// </summary>
    public static async Task<string> ReadAsync(
        McpServer server, JsonElement id, JsonElement? parameters)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (parameters is not { ValueKind: JsonValueKind.Object } given
            || !given.TryGetProperty("uri", out JsonElement named)
            || named.ValueKind != JsonValueKind.String
            || named.GetString() is not { } uri
            || !uri.StartsWith("xmip:", StringComparison.Ordinal))
        {
            return McpServer.Error(
                id, McpServer.Code.InvalidParams, "resources/read needs an Xmip URI, xmip:///…");
        }

        if (ScopePattern.HasWildcard(uri))
        {
            return McpServer.Error(
                id,
                McpServer.Code.InvalidParams,
                $"{uri} is a wildcard, and a resource is one scope; the tools take wildcards.");
        }

        (int exit, string row, string why) =
            await server.AnsweredAsync(["show", uri, .. server.SessionLine]).ConfigureAwait(false);

        if (exit != 0)
        {
            return McpServer.Error(id, McpServer.Code.ResourceNotFound, why.Trim());
        }

        // A leaf has nothing beneath it, which list says as a failure; the
        // read is the row alone then.
        (int listed, string beneath, _) =
            await server.AnsweredAsync(["list", uri, .. server.SessionLine]).ConfigureAwait(false);

        return McpServer.Result(id, writer =>
        {
            writer.WriteStartArray("contents");
            Content(writer, uri, row);

            if (listed == 0)
            {
                Content(writer, uri, beneath);
            }

            writer.WriteEndArray();
        });
    }

    private static void Content(Utf8JsonWriter writer, string uri, string document)
    {
        writer.WriteStartObject();
        writer.WriteString("uri", uri);
        writer.WriteString("mimeType", Json);
        writer.WriteString("text", document.TrimEnd());
        writer.WriteEndObject();
    }

    private static void Template(
        Utf8JsonWriter writer, string template, string name, string description)
    {
        writer.WriteStartObject();
        writer.WriteString("uriTemplate", template);
        writer.WriteString("name", name);
        writer.WriteString("description", description);
        writer.WriteString("mimeType", Json);
        writer.WriteEndObject();
    }

    // The stage words, from the runtime; where its library cannot be loaded,
    // said so rather than guessed.
    private static string Stages()
    {
        try
        {
            return string.Join(", ", ScopeTree.Stages);
        }
        catch (Exception unloaded) when (unloaded is DllNotFoundException
            or EntryPointNotFoundException or InvalidOperationException)
        {
            return "the runtime's stage words, which its library, not loaded, would say";
        }
    }
}
