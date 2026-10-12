using System.Text.Json;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// <c>xmip-cli mcp</c> (ADR-0071), one scripted session over in-memory
/// streams against the test cluster's names: initialize, the lists, a read of
/// an Xmip URI as an operator drills, a tool that is a read command, an act
/// refused, and what is not a method or not JSON answered with JSON-RPC's
/// error. The output carries protocol messages and nothing else.
/// </summary>
public sealed class McpServerTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    [Fact]
    public async Task ASessionReadsTheScopeTreeAndAnswersByTheCommands()
    {
        string orders = $"{Cluster.NodeScope(0)}/receive/orders";
        FakeSurface surface = new(
        [
            FakeSurface.Leaf(orders, HealthState.Holding, 70, "disk full"),
            FakeSurface.Leaf($"{Cluster.NodeScope(1)}/send/billing", HealthState.Fine),
        ]);
        string directory = Path.Combine(Path.GetTempPath(), $"xmip-mcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        ProgramAudit audit = new(CommandAudit.Program, directory);
        Invocation session = Invocation.Parse(["mcp"], out _)!;

        string[] script =
        [
            Request(1, "initialize", "{\"protocolVersion\":\"2025-11-25\","
                + "\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}"),
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            Request(2, "tools/list", "{}"),
            Request(3, "resources/templates/list", "{}"),
            Request(4, "resources/list", "{}"),
            Request(5, "resources/read", $"{{\"uri\":\"{Cluster.NodeScope(0)}\"}}"),
            Request(6, "tools/call", Call("health", $"\"scope\":\"{orders}\"")),
            Request(7, "tools/call", Call("show", $"\"scope\":\"{orders}\",\"who\":\"someone\"")),
            Request(8, "resources/subscribe", "{}"),
            "not json",
        ];

        using StringReader input = new(string.Join('\n', script) + "\n");
        using StringWriter output = new();
        using StringWriter diagnostics = new();
        McpServer server = new(
            session,
            audit,
            (_, out reason) =>
            {
                reason = string.Empty;
                return surface;
            },
            diagnostics);

        int exit = await server.RunAsync(input, output, CancellationToken.None);

        // One answer per request, the notification unanswered, and every line
        // a JSON-RPC 2.0 message.
        string[] lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(0, exit);
        Assert.Equal(9, lines.Length);
        List<JsonDocument> answers = [.. lines.Select(line => JsonDocument.Parse(line))];
        Assert.All(answers, answer =>
            Assert.Equal("2.0", answer.RootElement.GetProperty("jsonrpc").GetString()));

        JsonElement initialized = Result(answers[0]);
        Assert.Equal(
            McpServer.ProtocolVersion, initialized.GetProperty("protocolVersion").GetString());
        Assert.Equal(
            "xmip-cli", initialized.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.True(initialized.GetProperty("capabilities").TryGetProperty("resources", out _));

        // The tools are the read commands, and no act is one.
        string[] tools =
        [
            .. Result(answers[1]).GetProperty("tools").EnumerateArray()
                .Select(tool => tool.GetProperty("name").GetString()!),
        ];
        Assert.Contains("health", tools);
        Assert.Contains("dead-messages", tools);
        Assert.DoesNotContain("pause", tools);
        Assert.DoesNotContain("resume", tools);
        Assert.DoesNotContain("probe", tools);

        Assert.Contains(
            Result(answers[2]).GetProperty("resourceTemplates").EnumerateArray(),
            template => template.GetProperty("uriTemplate").GetString()
                == "xmip:///{cluster}/node/{node}");

        Assert.Contains(
            Result(answers[3]).GetProperty("resources").EnumerateArray(),
            resource => resource.GetProperty("uri").GetString() == orders);

        // A read is the drill: the node's row, then the rows beneath it.
        JsonElement[] contents =
            [.. Result(answers[4]).GetProperty("contents").EnumerateArray()];
        Assert.Equal(2, contents.Length);
        using JsonDocument row = JsonDocument.Parse(contents[0].GetProperty("text").GetString()!);
        Assert.Equal(Cluster.NodeScope(0), row.RootElement.GetProperty("scope").GetString());
        Assert.Equal("holding", row.RootElement.GetProperty("health").GetString());
        Assert.Equal(orders, row.RootElement.GetProperty("worst").GetString());
        Assert.Contains(
            orders, contents[1].GetProperty("text").GetString(), StringComparison.Ordinal);

        // A tool answers the command's document, with its scope.
        JsonElement called = Result(answers[5]);
        Assert.False(called.GetProperty("isError").GetBoolean());
        Assert.Equal(orders, called.GetProperty("_meta").GetProperty("xmip/scope").GetString());
        using JsonDocument health = JsonDocument.Parse(
            called.GetProperty("content")[0].GetProperty("text").GetString()!);
        Assert.Equal(orders, health.RootElement.GetProperty("scope").GetString());
        Assert.Equal("disk full", health.RootElement.GetProperty("worst")
            .GetProperty("evidence").GetString());

        // An act is not an argument of any tool; nor is a method MCP has and
        // this server does not answer; nor is a line that is not JSON.
        Assert.Equal(McpServer.Code.InvalidParams, ErrorCode(answers[6]));
        Assert.Equal(McpServer.Code.MethodNotFound, ErrorCode(answers[7]));
        Assert.Equal(McpServer.Code.Parse, ErrorCode(answers[8]));
        Assert.Equal(JsonValueKind.Null, answers[8].RootElement.GetProperty("id").ValueKind);

        // Each answer is audited as the command it is.
        string records = string.Concat(
            Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        Assert.Contains("action = \"health\"", records, StringComparison.Ordinal);
        Assert.Contains("action = \"show\"", records, StringComparison.Ordinal);

        answers.ForEach(answer => answer.Dispose());
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task ALineThatActsIsRefusedWhateverReachesIt()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xmip-mcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        McpServer server = new(
            Invocation.Parse(["mcp"], out _)!,
            new ProgramAudit(CommandAudit.Program, directory),
            (_, out reason) =>
            {
                reason = "no surface in this test";
                return null;
            },
            TextWriter.Null);

        (int exit, string output, string error) = await server.AnsweredAsync(
            ["dead-messages", "--location", Cluster.NodeScope(0), "--message", "m", "--replay"]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("mcp reads only", error, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    private static string Request(int id, string method, string parameters)
    {
        return $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\","
            + $"\"params\":{parameters}}}";
    }

    private static string Call(string tool, string arguments)
    {
        return $"{{\"name\":\"{tool}\",\"arguments\":{{{arguments}}}}}";
    }

    private static JsonElement Result(JsonDocument answer)
    {
        return answer.RootElement.GetProperty("result");
    }

    private static int ErrorCode(JsonDocument answer)
    {
        return answer.RootElement.GetProperty("error").GetProperty("code").GetInt32();
    }
}
