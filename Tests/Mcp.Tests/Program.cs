using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MTC;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var tests = new (string Name, Func<Task> Body)[]
{
    ("MCP initialize responds with protocol version and server info", McpInitializeReturnsProtocolVersionAndServerInfo),
    ("MCP initialized notification returns 202 accepted", McpInitializedNotificationReturnsAccepted),
    ("MCP tools/list advertises all tools with schemas", McpToolsListAdvertisesAllTools),
    ("MCP get_context tool call returns content from the bridge", McpGetContextToolCallReturnsContent),
    ("MCP query_sector tool call returns database snapshot", McpQuerySectorToolCallReturnsSnapshot),
    ("MCP read-only approval level rejects send_command as tool error", McpReadOnlyRejectsSendCommand),
    ("MCP approve-actions level honors player approval", McpApproveActionsHonorsPlayerApproval),
    ("MCP full-automation level sends commands without approval", McpFullAutomationSendsWithoutApproval),
    ("MCP unknown tool reports invalid params", McpUnknownToolReportsInvalidParams),
    ("MCP missing required argument reports invalid params", McpMissingRequiredArgumentReportsInvalidParams),
    ("MCP packet sniffing mtc.ping round-trips", McpPingRoundTrips),
    ("JSON-RPC HTTP POST keeps working next to MCP", JsonRpcHttpPostStillWorks),
    ("McpToolSchema maps every tool onto an mtc.* handler", ToolSchemaMapsEveryToolOntoJsonRpc),
    ("mtc.runScript and mtc.stopScript handlers work over plain HTTP", RunAndStopScriptHandlersWork),
    ("MCP GET without SSE accept returns 405", McpGetWithoutSseAcceptReturns405),
    ("MCP SSE stream pushes game events to the agent", McpSseStreamPushesGameEvents),
    ("MCP SSE stream pushes script state changes", McpSseStreamPushesScriptState),
    ("MCP SSE stream pushes script runtime errors as scriptState error", McpSseStreamPushesScriptRuntimeErrors),
    ("ScriptPathGuard rejects paths escaping the scripts root", () => { ScriptPathGuardRejectsEscapeAttempts(); return Task.CompletedTask; }),
    ("ScriptPathGuard accepts contained relative paths", () => { ScriptPathGuardAcceptsRelativePaths(); return Task.CompletedTask; }),
    ("ScriptPathGuard IsInsideRoot boundaries", () => { ScriptPathGuardInsideRootBoundaries(); return Task.CompletedTask; }),
};

int failed = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try
    {
        await body();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  {name}: {ex.Message}");
    }
}

Console.WriteLine(failed == 0 ? $"OK ({tests.Length} tests)" : $"FAILED ({failed}/{tests.Length} failing)");
return failed == 0 ? 0 : 1;

// ---------------------------------------------------------------------------
// Harness helpers
// ---------------------------------------------------------------------------

static async Task<McpHarness> StartServer(MtcRpcApprovalLevel approvalLevel)
{
    var stub = new StubBridge();
    var server = new MtcJsonRpcServer(stub.BuildBridge());
    server.ApplyOptions(new MtcJsonRpcServerOptions
    {
        Enabled = true,
        BindAddress = "127.0.0.1",
        Port = FreePort(),
        ApprovalLevel = approvalLevel,
    });

    var client = new HttpClient { BaseAddress = new Uri(server.Endpoint) };
    await Task.Delay(100);
    return new McpHarness(server, client, stub);
}

static int FreePort()
{
    var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static string ToolsCall(string name, string arguments)
    => """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"@name","arguments":@args}}"""
        .Replace("@name", name)
        .Replace("@args", arguments);

static JsonElement Select(JsonElement document, params string[] path)
{
    JsonElement current = document;
    foreach (string segment in path)
    {
        if (current.TryGetProperty(segment, out JsonElement next))
        {
            current = next;
        }
        else if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out int index) &&
                 index >= 0 && index < current.GetArrayLength())
        {
            current = current[index];
        }
        else
        {
            throw new InvalidOperationException($"Missing path '{string.Join('.', path)}' in response: {document.GetRawText()}");
        }
    }

    return current;
}

static JsonElement Parse(string body)
    => JsonDocument.Parse(body).RootElement.Clone();

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException($"Assertion failed: {message}");
}

async Task McpInitializeReturnsProtocolVersionAndServerInfo()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    try
    {
        JsonElement document = Parse(await harness.PostMcpAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"probe","version":"0"}}}"""));

        Assert(McpServer.ProtocolVersion == Select(document, "result", "protocolVersion").GetString(), "protocol version mismatch");
        Assert(McpServer.ServerName == Select(document, "result", "serverInfo", "name").GetString(), "server name mismatch");
        Assert(Select(document, "result", "capabilities", "tools").ValueKind == JsonValueKind.Object, "tools capability missing");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpInitializedNotificationReturnsAccepted()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    try
    {
        await harness.PostMcpAsync("""{"jsonrpc":"2.0","method":"initialize","params":{}}""");
        string notificationBody = await harness.PostMcpAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        Assert(string.IsNullOrEmpty(notificationBody), "notifications return an empty 202 body");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpToolsListAdvertisesAllTools()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    try
    {
        JsonElement document = Parse(await harness.PostMcpAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""));
        JsonElement tools = Select(document, "result", "tools");

        string[] names = tools.EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)
            .ToArray();
        string[] expected =
        [
            "get_context",
            "get_recent_events",
            "get_copilot_recommendation",
            "query_sector",
            "list_scripts",
            "propose_command",
            "send_command",
            "run_mombot_command",
            "run_script",
            "connect_server",
            "write_script",
            "edit_script",
            "read_script",
            "stop_script",
        ];
        Assert(names.SequenceEqual(expected), $"tool names mismatch: {string.Join(',', names)}");

        JsonElement sendCommand = tools.EnumerateArray()
            .First(tool => tool.GetProperty("name").GetString() == "send_command");
        Assert(sendCommand.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean() == false, "send_command readOnlyHint");
        Assert(Select(sendCommand, "inputSchema", "required").EnumerateArray().Any(), "send_command required args missing");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpGetContextToolCallReturnsContent()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    try
    {
        JsonElement document = Parse(await harness.PostMcpAsync(ToolsCall("get_context", "{}")));
        string content = Select(document, "result", "content").EnumerateArray().First().GetProperty("text").GetString()!;
        Assert(content.Contains("Test Game"), $"context content missing game name: {content}");
        Assert(Select(document, "result", "isError").GetBoolean() == false, "isError should be false");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpQuerySectorToolCallReturnsSnapshot()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    try
    {
        JsonElement document = Parse(await harness.PostMcpAsync(ToolsCall("query_sector", """{"sector":3}""")));
        string content = Select(document, "result", "content").EnumerateArray().First().GetProperty("text").GetString()!;
        Assert(content.Contains("\"number\":3"), $"sector snapshot missing number: {content}");
        Assert(Select(document, "result", "isError").GetBoolean() == false, "isError should be false");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpReadOnlyRejectsSendCommand()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    try
    {
        JsonElement document = Parse(await harness.PostMcpAsync(ToolsCall("send_command", """{"command":"P"}""")));
        Assert(Select(document, "result", "isError").GetBoolean() == true, "send_command should error in read-only mode");
        string text = Select(document, "result", "content").EnumerateArray().First().GetProperty("text").GetString()!;
        Assert(text.Contains("approval level"), $"unexpected error text: {text}");
        Assert(harness.Stub.SentCommands.Count == 0, "no command should reach the game in read-only mode");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpApproveActionsHonorsPlayerApproval()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ApproveActions);
    try
    {
        harness.Stub.ApprovalDecision = true;
        JsonElement approved = Parse(await harness.PostMcpAsync(ToolsCall("send_command", """{"command":"P"}""")));
        Assert(Select(approved, "result", "isError").GetBoolean() == false, "approved call should succeed");
        Assert(harness.Stub.SentCommands.Count == 1 && harness.Stub.SentCommands[0] == "P", "approved command should reach the game");

        harness.Stub.ApprovalDecision = false;
        JsonElement rejected = Parse(await harness.PostMcpAsync(ToolsCall("send_command", """{"command":"Q"}""")));
        Assert(Select(rejected, "result", "isError").GetBoolean() == true, "rejected call should error");
        Assert(harness.Stub.SentCommands.Count == 1, "rejected command must not reach the game");
        Assert(harness.Stub.ApprovalRequests == 2, $"expected two approval prompts, got {harness.Stub.ApprovalRequests}");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpFullAutomationSendsWithoutApproval()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.FullAutomation);
    try
    {
        JsonElement document = Parse(await harness.PostMcpAsync(ToolsCall("send_command", """{"command":"D"}""")));
        Assert(Select(document, "result", "isError").GetBoolean() == false, "full-automation call should succeed");
        Assert(harness.Stub.SentCommands.Count == 1 && harness.Stub.SentCommands[0] == "D", "command should reach the game");
        Assert(harness.Stub.ApprovalRequests == 0, "full automation must not prompt for approval");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpUnknownToolReportsInvalidParams()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    try
    {
        JsonElement document = Parse(await harness.PostMcpAsync(ToolsCall("does_not_exist", "{}")));
        Assert(Select(document, "error", "code").GetInt32() == -32602, "unknown tool error code");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpMissingRequiredArgumentReportsInvalidParams()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    try
    {
        JsonElement document = Parse(await harness.PostMcpAsync(ToolsCall("query_sector", "{}")));
        Assert(Select(document, "error", "code").GetInt32() == -32602, "missing argument error code");
        Assert(Select(document, "error", "message").GetString()!.Contains("sector"), "missing argument names the parameter");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpPingRoundTrips()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    try
    {
        JsonElement document = Parse(await harness.PostMcpAsync("""{"jsonrpc":"2.0","id":5,"method":"ping"}"""));
        Assert(document.TryGetProperty("result", out _), "ping should return an empty result");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task JsonRpcHttpPostStillWorks()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    try
    {
        JsonElement document = await harness.PostRpcAsync(
            """{"jsonrpc":"2.0","id":9,"method":"mtc.getContext","params":{"recentEventCount":10}}""");
        Assert(!document.TryGetProperty("error", out _), "JSON-RPC getContext must not error");
        Assert("Test Game" == Select(document, "result", "gameName").GetString(), "JSON-RPC context still served");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task ToolSchemaMapsEveryToolOntoJsonRpc()
{
    McpToolDescriptor[] tools = McpToolSchema.DescribeTools().ToArray();
    Assert(tools.Length == 14, $"expected 14 tools, got {tools.Length}");
    foreach (McpToolDescriptor tool in tools)
    {
        Assert(tool.JsonRpcMethod.StartsWith("mtc.", StringComparison.Ordinal), $"{tool.Name} must map onto an mtc.* method");
        Assert(!string.IsNullOrWhiteSpace(tool.Description), $"{tool.Name} needs a description");
        Assert(McpToolSchema.BuildInputSchema(tool) != null, $"{tool.Name} needs an input schema");
    }

    Assert(tools.Count(tool => !tool.ReadOnly) == 7, "exactly the seven mutating tools are marked non-readonly");
}

async Task RunAndStopScriptHandlersWork()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.FullAutomation);
    try
    {
        JsonElement started = await harness.PostRpcAsync(
            """{"jsonrpc":"2.0","id":1,"method":"mtc.runScript","params":{"script":"trade.cts"}}""");
        Assert(Select(started, "result", "message").GetString()!.Contains("trade.cts"), "runScript handler works");

        JsonElement stopped = await harness.PostRpcAsync(
            """{"jsonrpc":"2.0","id":2,"method":"mtc.stopScript","params":{"name":"trade.cts"}}""");
        Assert(Select(stopped, "result", "message").GetString()!.Contains("stopped"), "stopScript handler works");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpGetWithoutSseAcceptReturns405()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    try
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        HttpResponseMessage response = await harness.Client.SendAsync(request).ConfigureAwait(false);
        Assert(response.StatusCode == HttpStatusCode.MethodNotAllowed, $"expected 405, got {response.StatusCode}");
    }
    finally
    {
        harness.Dispose();
    }
}

async Task McpSseStreamPushesGameEvents()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    System.IO.Stream? stream = null;
    try
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        HttpResponseMessage response = await harness.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        Assert(response.StatusCode == HttpStatusCode.OK, $"expected 200, got {response.StatusCode}");
        Assert((response.Content.Headers.ContentType?.MediaType ?? string.Empty).StartsWith("text/event-stream"), "content type must be text/event-stream");

        stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        string openMarker = await ReadSseUntil(stream, "mtc mcp stream open");
        Assert(openMarker.Contains("mtc mcp stream open"), $"expected open marker, got: {openMarker}");

        harness.Server.PublishGameAgentEvent(new GameAgentEvent
        {
            Kind = GameAgentEventKind.ServerPrompt,
            PlainText = "Command [TL=00:00:00]:[1]",
        });
        string notification = await ReadSseUntil(stream, "notifications/mtc/gameEvent");
        Assert(notification.Contains("\"params\""), $"expected notification params, got: {notification}");
        Assert(notification.Contains("Command [TL"), $"expected prompt text, got: {notification}");
    }
    finally
    {
        if (stream != null)
            await stream.DisposeAsync();
        harness.Dispose();
    }
}

async Task McpSseStreamPushesScriptState()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.FullAutomation);
    System.IO.Stream? stream = null;
    try
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        HttpResponseMessage response = await harness.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await ReadSseUntil(stream, "mtc mcp stream open");

        JsonElement started = await harness.PostRpcAsync(
            """{"jsonrpc":"2.0","id":3,"method":"mtc.runScript","params":{"script":"trade.cts"}}""");
        Assert(started.TryGetProperty("error", out _) == false, "runScript should succeed");

        string notification = await ReadSseUntil(stream, "notifications/mtc/scriptState");
        Assert(notification.Contains("\"change\":\"started\""), $"expected script started notification, got: {notification}");
        Assert(notification.Contains("trade.cts"), $"expected script name, got: {notification}");
    }
    finally
    {
        if (stream != null)
            await stream.DisposeAsync();
        harness.Dispose();
    }
}

async Task McpSseStreamPushesScriptRuntimeErrors()
{
    McpHarness harness = await StartServer(MtcRpcApprovalLevel.ReadOnly);
    System.IO.Stream? stream = null;
    try
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        HttpResponseMessage response = await harness.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await ReadSseUntil(stream, "mtc mcp stream open");

        // Simulates the MtcJsonRpcIntegration wiring: an interpreter error becomes a
        // System GameAgentEvent with scriptEvent=error metadata and reaches the agent
        // as both a gameEvent and a scriptState error notification.
        harness.Server.PublishGameAgentEvent(new GameAgentEvent
        {
            Kind = GameAgentEventKind.System,
            PlainText = "[Script error] trade.cts (main.ts) line 42: Sector 5 not found.",
            Metadata = new Dictionary<string, string>
            {
                ["scriptEvent"] = "error",
                ["script"] = "trade.cts",
            },
        });

        string notification = await ReadSseUntil(stream, "notifications/mtc/scriptState");
        Assert(notification.Contains("\"change\":\"error\""), $"expected error change, got: {notification}");
        Assert(notification.Contains("trade.cts"), $"expected script name, got: {notification}");
        Assert(notification.Contains("isError") == false && notification.Contains("\"success\":false"), $"expected success false, got: {notification}");
    }
    finally
    {
        if (stream != null)
            await stream.DisposeAsync();
        harness.Dispose();
    }
}

static async Task<string> ReadSseUntil(System.IO.Stream stream, string needle)
{
    var buffer = new byte[4096];
    var builder = new StringBuilder();
    using var timeoutCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (builder.ToString().Contains(needle, StringComparison.Ordinal) == false)
    {
        int read = await stream.ReadAsync(buffer, timeoutCts.Token).ConfigureAwait(false);
        if (read == 0)
            throw new InvalidOperationException($"SSE stream closed before '{needle}': {builder}");
        builder.Append(Encoding.UTF8.GetString(buffer, 0, read));
    }

    return builder.ToString();
}


// ---------------------------------------------------------------------------
// ScriptPathGuard tests
// ---------------------------------------------------------------------------

static ScriptPathGuardHarness NewGuardHarness()
{
    string root = Path.Combine(Path.GetTempPath(), "twx-guard-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    return new ScriptPathGuardHarness(root);
}

static void ScriptPathGuardRejectsEscapeAttempts()
{
    var harness = NewGuardHarness();
    string[] rejected = ["../../evil.ts", "..\\..\\evil.ts", "/etc/passwd", "C:\\x.ts", "//unc/share/x", "..", 
        "a/../../b.ts", "Subdir/../../../outside.ts", "sub\0null.ts"];

    foreach (string candidate in rejected)
        Assert(!ScriptPathGuard.TryResolve(candidate, harness.Root, out _), $"must reject '{candidate}'");

    Assert(!ScriptPathGuard.TryResolve("   ", harness.Root, out _), "must reject whitespace-only path");
}

static void ScriptPathGuardAcceptsRelativePaths()
{
    var harness = NewGuardHarness();
    var expected = new Dictionary<string, string>
    {
        ["Pack2/2_Find.ts"] = Path.Combine(harness.Root, "Pack2", "2_Find.ts"),
        ["include/header.ts"] = Path.Combine(harness.Root, "include", "header.ts"),
        ["a/b/c.ts"] = Path.Combine(harness.Root, "a", "b", "c.ts"),
        ["./x.ts"] = Path.Combine(harness.Root, "x.ts"),
    };

    foreach ((string candidate, string wanted) in expected)
    {
        bool resolved = ScriptPathGuard.TryResolve(candidate, harness.Root, out string fullPath);
        Assert(resolved, $"must accept '{candidate}'");
        Assert(string.Equals(fullPath, wanted, StringComparison.Ordinal), 
            $"'{candidate}' resolved to '{fullPath}', expected '{wanted}'");
    }
}

static void ScriptPathGuardInsideRootBoundaries()
{
    var harness = NewGuardHarness();
    Assert(ScriptPathGuard.IsInsideRoot(harness.Root, harness.Root), "root itself counts as inside");
    Assert(ScriptPathGuard.IsInsideRoot(Path.Combine(harness.Root, "x.ts"), harness.Root), "root/x.ts is inside");
    string sibling = harness.Root + "2";
    Assert(!ScriptPathGuard.IsInsideRoot(sibling, harness.Root), "prefix-only sibling root must not count as inside");
    Assert(!ScriptPathGuard.IsInsideRoot(Path.Combine(sibling, "x.ts"), harness.Root), "sibling contents must not count as inside");
    Assert(!ScriptPathGuard.IsInsideRoot(string.Empty, harness.Root), "empty path is not inside");
    Assert(!ScriptPathGuard.IsInsideRoot(Path.Combine(harness.Root, "x.ts"), string.Empty), "empty root rejects everything");
}

internal sealed record McpHarness(MtcJsonRpcServer Server, HttpClient Client, StubBridge Stub)
{
    private static JsonElement ParseBody(string body)
        => JsonDocument.Parse(body).RootElement.Clone();

    public async Task<string> PostMcpAsync(string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        HttpResponseMessage response = await Client.SendAsync(request).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    public async Task<JsonElement> PostRpcAsync(string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        HttpResponseMessage response = await Client.SendAsync(request).ConfigureAwait(false);
        return ParseBody(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    public void Dispose()
    {
        Client.Dispose();
        Server.Stop();
        Server.Dispose();
    }
}

// ---------------------------------------------------------------------------
// Bridge stub
// ---------------------------------------------------------------------------

internal sealed class StubBridge
{
    public List<string> SentCommands { get; } = [];
    public bool ApprovalDecision { get; set; }
    public int ApprovalRequests;

    public MtcRpcBridge BuildBridge()
        => new()
        {
            GetContextAsync = _ => Task.FromResult(new GameAgentContextSnapshot
            {
                GameName = "Test Game",
                Connected = true,
                CurrentSector = 1,
                CurrentPrompt = "Command [TL=00:00:00]:[1]",
                OnlinePlayers = ["reaper"],
                RunningScripts = [new GameAgentRunningScriptSnapshot { Id = 4, Name = "trade.cts", Paused = true }],
            }),
            GetRecentEventsAsync = (limit, _) => Task.FromResult<IReadOnlyList<GameAgentEvent>>(
                Enumerable.Range(0, Math.Max(limit, 0)).Select(i => new GameAgentEvent { PlainText = $"event {i}" }).ToList()),
            QuerySectorAsync = sector => Task.FromResult<GameAgentSectorSnapshot?>(new GameAgentSectorSnapshot
            {
                Number = sector,
                Explored = "explored",
                WarpsOut = [2, 3],
            }),
            ListScriptsAsync = () => Task.FromResult<IReadOnlyList<GameAgentRunningScriptSnapshot>>(
                [new GameAgentRunningScriptSnapshot { Id = 4, Name = "trade.cts", Paused = true }]),
            SendCommandAsync = (command, _) =>
            {
                SentCommands.Add(command);
                return Task.FromResult(MtcRpcActionResult.Ok($"sent: {command}"));
            },
            RunMombotCommandAsync = command => Task.FromResult(MtcRpcActionResult.Ok($"mombot: {command}")),
            RunScriptAsync = script => Task.FromResult(MtcRpcActionResult.Ok($"script started: {script}")),
            StopScriptAsync = (_, _) => Task.FromResult(MtcRpcActionResult.Ok("script stopped")),
            ApproveActionAsync = (_, _) =>
            {
                ApprovalRequests++;
                return Task.FromResult(ApprovalDecision);
            },
            ConnectServerAsync = () => Task.FromResult(MtcRpcActionResult.Ok("connected: test")),
            WriteScriptAsync = (path, content) => Task.FromResult(MtcRpcActionResult.Ok($"write: {path}")),
            EditScriptAsync = (path, _, _, _) => Task.FromResult(MtcRpcActionResult.Ok($"edit: {path}")),
            ReadScriptAsync = (path, offset, limit) => Task.FromResult(new MtcScriptReadResult
            {
                Path = path,
                TotalLines = 1,
                Offset = offset,
                Content = $"read: {path} offset={offset} limit={limit}",
            }),
        };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

// <summary>Holds a freshly created temporary scripts root and cleans it up.</summary>
internal sealed class ScriptPathGuardHarness
{
    public ScriptPathGuardHarness(string root)
    {
        Root = root;
    }

    public string Root { get; }
}

