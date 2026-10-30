using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip mcp</c>: a Model Context Protocol server over standard input
/// and output (ADR-0071) — JSON-RPC 2.0, one message per line, the fifth way
/// into the operator boundary. Its tools are the command line's read
/// commands (<see cref="McpTools"/>) and its resources the scope tree, each
/// scope an Xmip URI (<see cref="McpResources"/>); both answer through
/// <see cref="CommandRun"/>, as the command typed would, and each answer is
/// audited as that command (<see cref="CommandAudit"/>).
/// </summary>
/// <remarks>
/// The output writer carries protocol messages and nothing else; what the
/// server says of itself goes to <c>diagnostics</c>, stderr. Read only: no
/// act is reachable, so the caller acts as no more than an Observer
/// (ADR-0009, amendment 2026-09-06), whatever the role it holds.
/// </remarks>
/// <param name="session">The <c>mcp</c> line: the surface it states is the
/// one every tool and resource reads.</param>
/// <param name="audit">The program's audit, where every answer is recorded.</param>
/// <param name="open">Opens the surface a line reads.</param>
/// <param name="diagnostics">What the server says of itself, never on the
/// protocol's stream.</param>
public sealed class McpServer(
    Invocation session, ProgramAudit audit, SurfaceOpener open, TextWriter diagnostics)
{
    /// <summary>The revision of the protocol this server speaks, offered when
    /// a client asks for one it does not understand.</summary>
    public const string ProtocolVersion = "2025-11-25";

    // The revisions whose messages this server's are, newest first.
    private static readonly string[] Understood = [ProtocolVersion, "2025-06-18", "2025-03-26"];

    /// <summary>JSON-RPC's codes, and MCP's for a resource that is not there.</summary>
    public static class Code
    {
        /// <summary>The line is not JSON.</summary>
        public const int Parse = -32700;

        /// <summary>The JSON is not a request.</summary>
        public const int InvalidRequest = -32600;

        /// <summary>No such method.</summary>
        public const int MethodNotFound = -32601;

        /// <summary>The method's parameters are wrong.</summary>
        public const int InvalidParams = -32602;

        /// <summary>The server could not answer.</summary>
        public const int Internal = -32603;

        /// <summary>No resource at that URI.</summary>
        public const int ResourceNotFound = -32002;
    }

    /// <summary>Answer every message on <paramref name="input"/>, one line
    /// each on <paramref name="output"/>, until the input ends. Exit 0.</summary>
    public async Task<int> RunAsync(TextReader input, TextWriter output, CancellationToken stop)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        diagnostics.WriteLine($"xmip mcp: speaking MCP {ProtocolVersion} on stdin and stdout.");

        while (await input.ReadLineAsync(stop).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (await AnswerAsync(line).ConfigureAwait(false) is { } answer)
            {
                await output.WriteLineAsync(answer.AsMemory(), stop).ConfigureAwait(false);
                await output.FlushAsync(stop).ConfigureAwait(false);
            }
        }

        return 0;
    }

    /// <summary>The answer to one message, or null for a notification, which
    /// is never answered.</summary>
    public async Task<string?> AnswerAsync(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        JsonDocument request;

        try
        {
            request = JsonDocument.Parse(line);
        }
        catch (JsonException unread)
        {
            return Error(null, Code.Parse, $"Parse error: {unread.Message}");
        }

        using (request)
        {
            JsonElement root = request.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("jsonrpc", out JsonElement version)
                || version.ValueKind != JsonValueKind.String
                || version.GetString() != "2.0"
                || !root.TryGetProperty("method", out JsonElement named)
                || named.ValueKind != JsonValueKind.String)
            {
                return Error(Id(root), Code.InvalidRequest, "Invalid Request: a JSON-RPC 2.0 "
                    + "request is an object with jsonrpc \"2.0\" and a method.");
            }

            string method = named.GetString()!;
            JsonElement? parameters = root.TryGetProperty("params", out JsonElement given)
                ? given
                : null;

            if (Id(root) is not { } id)
            {
                // A notification: notifications/initialized and the rest are
                // taken and never answered.
                diagnostics.WriteLine($"xmip mcp: {method}.");
                return null;
            }

            return method switch
            {
                "initialize" => Result(id, writer => Initialize(writer, parameters)),
                "ping" => Result(id, _ => { }),
                "tools/list" => Result(id, McpTools.WriteList),
                "tools/call" => await CallAsync(id, parameters).ConfigureAwait(false),
                "resources/list" => await McpResources.ListAsync(this, id).ConfigureAwait(false),
                "resources/templates/list" => Result(id, McpResources.WriteTemplates),
                "resources/read" => await McpResources.ReadAsync(this, id, parameters)
                    .ConfigureAwait(false),
                _ => Error(id, Code.MethodNotFound, $"Method not found: {method}."),
            };
        }
    }

    /// <summary>
    /// Answer a command line as <c>xmip</c> would, audited as that
    /// command: its exit, what it wrote on its output and on its error
    /// writer. A line that cannot be obeyed, or that names an act, is
    /// answered exit 2 with why.
    /// </summary>
    public async Task<(int Exit, string Output, string Error)> AnsweredAsync(
        IReadOnlyList<string> line)
    {
        Invocation? invocation = Invocation.Parse(line, out string problem);

        if (invocation is null || Acts(invocation))
        {
            string refused = invocation is null
                ? problem
                : $"REFUSED: '{string.Join(' ', line)}' acts, and mcp reads only.";
            CommandAudit.Refused(audit, refused);
            return (2, string.Empty, refused);
        }

        using StringWriter output = new();
        using StringWriter error = new();

        CommandAudit.Begun(audit, invocation);
        int exit = await CommandRun.AnswerAsync(invocation, audit, open, output, error)
            .ConfigureAwait(false);
        CommandAudit.Ended(audit, invocation, exit, error.ToString());

        return (exit, output.ToString(), error.ToString());
    }

    /// <summary>
    /// What follows every line a tool or a resource answers by: the surface
    /// the <c>mcp</c> line stated — <c>--runtime</c>, <c>--remote</c>,
    /// <c>--local</c>, <c>--snapshot</c> — and <c>--json</c>, since a client
    /// reads documents.
    /// </summary>
    public IReadOnlyList<string> SessionLine { get; } =
    [
        .. Stated("--runtime", session.Runtime),
        .. Stated("--remote", session.Remote),
        .. Stated("--local", session.Local),
        .. Stated("--snapshot", session.Snapshot),
        "--json",
    ];

    /// <summary>The surface the session's line states.</summary>
    public IOperatorSurface? Surface(out string reason)
    {
        return open(session, out reason);
    }

    /// <summary>A result, as one line.</summary>
    public static string Result(JsonElement id, Action<Utf8JsonWriter> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return Message(id, writer =>
        {
            writer.WriteStartObject("result");
            result(writer);
            writer.WriteEndObject();
        });
    }

    /// <summary>An error, as one line; a null id where the request's could
    /// not be read.</summary>
    public static string Error(JsonElement? id, int code, string message)
    {
        return Message(id, writer =>
        {
            writer.WriteStartObject("error");
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();
        });
    }

    // One JSON-RPC message: jsonrpc, the id as the request gave it, then what
    // the body writes.
    private static string Message(JsonElement? id, Action<Utf8JsonWriter> body)
    {
        return JsonText.Document(writer =>
        {
            writer.WriteString("jsonrpc", "2.0");
            writer.WritePropertyName("id");

            if (id is { } known)
            {
                known.WriteTo(writer);
            }
            else
            {
                writer.WriteNullValue();
            }

            body(writer);
        },
        Relaxed);
    }

    // What a message escapes: what JSON requires and no more, so a document
    // inside a text is read as written.
    private static readonly JsonWriterOptions Relaxed =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // An option and its value where the line stated one; nothing otherwise.
    private static string[] Stated(string option, string? value)
    {
        return value is null ? [] : [option, value];
    }

    // The request's id, a string or a number, as given; null where it has
    // none (a notification) or one of another kind.
    private static JsonElement? Id(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("id", out JsonElement id)
            && id.ValueKind is JsonValueKind.String or JsonValueKind.Number
                ? id.Clone()
                : null;
    }

    // Whether a parsed line acts rather than reads: pause, resume, probe, a
    // noun's act, or a who.
    private static bool Acts(Invocation invocation)
    {
        return invocation.Command is Command.Pause or Command.Resume or Command.Probe
                or Command.Mcp or Command.Help
            || invocation.Who is not null
            || invocation.Follow
            || invocation.EventAct is not null
            || invocation.Act is not null
            || invocation.Replay is not null
            || invocation.MessageAct is not null;
    }

    private static void Initialize(Utf8JsonWriter writer, JsonElement? parameters)
    {
        string asked = parameters is { ValueKind: JsonValueKind.Object } given
            && given.TryGetProperty("protocolVersion", out JsonElement version)
            && version.ValueKind == JsonValueKind.String
                ? version.GetString()!
                : ProtocolVersion;

        writer.WriteString(
            "protocolVersion", Understood.Contains(asked) ? asked : ProtocolVersion);
        writer.WriteStartObject("capabilities");
        writer.WriteStartObject("tools");
        writer.WriteBoolean("listChanged", false);
        writer.WriteEndObject();
        writer.WriteStartObject("resources");
        writer.WriteBoolean("listChanged", false);
        writer.WriteBoolean("subscribe", false);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteStartObject("serverInfo");
        writer.WriteString("name", CommandAudit.Program);
        writer.WriteString(
            "version", typeof(McpServer).Assembly.GetName().Version?.ToString() ?? "0");
        writer.WriteEndObject();
        writer.WriteString("instructions", Instructions);
    }

    private const string Instructions =
        "Xmip, read through its operator boundary, read only. Resources are Xmip URIs "
        + "(xmip:///<cluster>/node/<node>/...): read one for its row - mood, worst leaf "
        + "and why, figures - and the scopes beneath it, and read those to drill to a "
        + "cause. Tools are xmip's read commands; each answers one JSON document, "
        + "and every row in it names its scope.";

    private async Task<string> CallAsync(JsonElement id, JsonElement? parameters)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } given
            || !given.TryGetProperty("name", out JsonElement named)
            || named.ValueKind != JsonValueKind.String)
        {
            return Error(id, Code.InvalidParams, "tools/call needs the tool's name.");
        }

        JsonElement? arguments = given.TryGetProperty("arguments", out JsonElement said)
            ? said
            : null;
        IReadOnlyList<string>? line = McpTools.Line(
            named.GetString()!, arguments, SessionLine, out string problem);

        if (line is null)
        {
            return Error(id, Code.InvalidParams, problem);
        }

        (int exit, string output, string error) = await AnsweredAsync(line).ConfigureAwait(false);
        string scope = Scope(line);

        return Result(id, writer =>
        {
            writer.WriteStartArray("content");
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", Said(exit, output, error));
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteBoolean("isError", exit != 0);
            writer.WriteStartObject("_meta");
            writer.WriteString("xmip/scope", scope);
            writer.WriteNumber("xmip/exit", exit);
            writer.WriteEndObject();
        });
    }

    // The scope a line asks at: its argument where that is an Xmip URI, else
    // its --location, else the root.
    private static string Scope(IReadOnlyList<string> line)
    {
        if (line.Count > 1 && line[1].StartsWith("xmip:", StringComparison.Ordinal))
        {
            return line[1];
        }

        for (int at = 1; at + 1 < line.Count; at++)
        {
            if (line[at] == "--location")
            {
                return line[at + 1];
            }
        }

        return ScopeTree.Root;
    }

    // What a command said: its document; where it failed, what it said on
    // its error writer too, as the operator would read both.
    private static string Said(int exit, string output, string error)
    {
        StringBuilder said = new(output.TrimEnd());

        if (exit != 0 && error.Trim().Length > 0)
        {
            said.Append(said.Length > 0 ? "\n" : string.Empty).Append(error.TrimEnd());
        }

        return said.ToString();
    }
}
