using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MTC;

/// <summary>
/// Hosts an MCP (Model Context Protocol) Streamable HTTP endpoint at <c>/mcp</c> on the same
/// <see cref="System.Net.HttpListener"/> as the MTC JSON-RPC server. Agents connect directly to
/// <c>http://&lt;bind&gt;:&lt;port&gt;/mcp</c> with the existing bearer token. Every tool is backed by
/// a proven <c>mtc.*</c> JSON-RPC handler, so approval-level gating (read-only / approve-actions /
/// full-automation) applies identically to MCP calls.
/// </summary>
internal sealed class McpServer
{
    public const string Path = "/mcp";
    public const string ProtocolVersion = "2025-06-18";
    public const string ServerName = "twxproxy-mtc";
    public const string ServerVersion = "3.0.0-beta5";

    private readonly MtcJsonRpcServer _owner;
    private readonly object _sync = new();
    private readonly List<McpSseStream> _sseStreams = [];
    private string? _sessionId;

    /// <summary>Create an MCP endpoint that delegates authorization, dispatch, and approval gating to the JSON-RPC server.</summary>
    /// <param name="owner">The JSON-RPC server whose listener, auth, and method handlers host this endpoint</param>
    public McpServer(MtcJsonRpcServer owner)
    {
        _owner = owner;
    }

    /// <summary>Handles one HTTP request routed from the JSON-RPC server's listen loop.</summary>
    /// <param name="context">The HTTP request/response context</param>
    /// <param name="cancellationToken">Token cancelled when the RPC server stops</param>
    /// <returns>A task representing the asynchronous request</returns>
    public async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await HandlePostAsync(context, cancellationToken).ConfigureAwait(false);
                return;
            }

            string accept = context.Request.Headers["Accept"] ?? string.Empty;
            if (string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) &&
                accept.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
            {
                OpenSseStream(context, cancellationToken);
                return;
            }

            // Streamable HTTP allows a GET for server-initiated SSE streams; without an SSE
            // Accept header this server offers nothing, so report method-not-allowed per the spec.
            context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            await WriteJsonAsync(context.Response, new { error = "POST required" }, null, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            context.Response.Abort();
        }
        catch (Exception ex)
        {
            TWXProxy.Core.GlobalModules.DebugLog($"[MTC.Mcp] request failed: {ex}\n");
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await WriteJsonAsync(context.Response, new { error = ex.Message }, null, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Response is already gone; nothing further to do.
            }
        }
    }

    private async Task HandlePostAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8);
        string requestJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        JsonRpcResponseObject? response;
        try
        {
            using JsonDocument document = JsonDocument.Parse(requestJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new MtcRpcException(-32600, "MCP requests must be a single JSON-RPC message object.");

            string? sessionId = context.Request.Headers["Mcp-Session-Id"];
            response = await HandleMessageAsync(root, sessionId, cancellationToken).ConfigureAwait(false);
        }
        catch (MtcRpcException ex)
        {
            response = McpServerResponse(null, new JsonRpcErrorObject { Code = ex.Code, Message = ex.Message, Data = ex.DataObject });
        }
        catch (JsonException ex)
        {
            response = McpServerResponse(null, new JsonRpcErrorObject { Code = -32700, Message = "Parse error", Data = ex.Message });
        }

        if (response == null)
        {
            // Notifications (messages without an id) get an empty 202 per the Streamable HTTP transport.
            context.Response.StatusCode = (int)HttpStatusCode.Accepted;
            context.Response.ContentLength64 = 0;
            context.Response.Close();
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "application/json";
        context.Response.Headers["MCP-Protocol-Version"] = ProtocolVersion;
        await WriteJsonAsync(context.Response, response, _sessionId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonRpcResponseObject?> HandleMessageAsync(JsonElement root, string? sessionId, CancellationToken cancellationToken)
    {
        string? method = root.TryGetProperty("method", out JsonElement methodElement) &&
                         methodElement.ValueKind == JsonValueKind.String
            ? methodElement.GetString()
            : null;

        bool hasId = root.TryGetProperty("id", out JsonElement idElement) && idElement.ValueKind != JsonValueKind.Null;
        object? id = hasId ? idElement.Clone() : null;
        JsonElement? parameters = root.TryGetProperty("params", out JsonElement paramsElement) &&
                                  paramsElement.ValueKind == JsonValueKind.Object
            ? paramsElement
            : null;

        if (method == null)
        {
            if (!hasId)
                return null;
            return McpServerResponse(id, new JsonRpcErrorObject { Code = -32600, Message = "Missing method." });
        }

        bool knownNotification = method.StartsWith("notifications/", StringComparison.Ordinal);
        if (knownNotification)
        {
            if (!hasId)
                return null;
            return McpServerResponse(id, new JsonRpcErrorObject { Code = -32600, Message = "Notifications must not carry an id." });
        }

        switch (method)
        {
            case "initialize":
                lock (_sync)
                    _sessionId ??= $"mtc-{Guid.NewGuid():N}";
                return McpServerResponse(id, BuildInitializeResult());

            case "ping":
                return McpServerResponse(id, new { });

            case "tools/list":
                return McpServerResponse(id, new { tools = McpToolSchema.DescribeTools().Select(DescribeTool) });

            case "tools/call":
                return McpServerResponse(id, await CallToolAsync(parameters, cancellationToken).ConfigureAwait(false));

            default:
                return McpServerResponse(id, new JsonRpcErrorObject { Code = -32601, Message = $"Method not found: {method}" });
        }
    }

    /// <summary>Opens a long-lived SSE stream that pushes notifications to the agent.</summary>
    /// <param name="context">The HTTP context to stream on</param>
    /// <param name="cancellationToken">Token cancelled when the RPC server stops</param>
    private void OpenSseStream(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var stream = new McpSseStream(context.Response);
        lock (_sync)
            _sseStreams.Add(stream);

        cancellationToken.Register(() => RemoveSseStream(stream));
        TWXProxy.Core.GlobalModules.DebugLog($"[MTC.Mcp] SSE stream opened: {stream.Id}\n");
    }

    /// <summary>Removes and disposes one SSE stream after disconnect or shutdown.</summary>
    /// <param name="stream">The stream to remove</param>
    private void RemoveSseStream(McpSseStream stream)
    {
        lock (_sync)
            _ = _sseStreams.Remove(stream);
        stream.Dispose();
    }

    /// <summary>Fans one observed game event out to every connected MCP SSE stream.</summary>
    /// <param name="evt">The game event observed by the MTC game agent</param>
    public void PublishEvent(GameAgentEvent evt)
    {
        lock (_sync)
        {
            foreach (McpSseStream stream in _sseStreams)
                stream.PublishGameEvent(evt);
        }
    }

    /// <summary>Fans a script runtime state change out to every connected MCP SSE stream.</summary>
    /// <param name="change">What happened, e.g. "started" or "stopped"</param>
    /// <param name="script">The script name or id involved</param>
    /// <param name="result">The runtime action result</param>
    public void PublishScriptState(string change, string script, MtcRpcActionResult result)
    {
        lock (_sync)
        {
            foreach (McpSseStream stream in _sseStreams)
                stream.PublishScriptState(change, script, result);
        }
    }

    /// <summary>Closes every open SSE stream; called when the JSON-RPC server disposes.</summary>
    public void Dispose()
    {
        McpSseStream[] streams;
        lock (_sync)
        {
            streams = [.. _sseStreams];
            _sseStreams.Clear();
        }

        foreach (McpSseStream stream in streams)
            stream.Dispose();
    }

    private object BuildInitializeResult()
        => new
        {
            protocolVersion = ProtocolVersion,
            capabilities = new
            {
                tools = new { listChanged = false },
            },
            serverInfo = new { name = ServerName, version = ServerVersion },
            instructions = "MTC Trade Wars proxy MCP endpoint. Read tools inspect live game state and the local database. " +
                           "Mutating tools (send_command, run_mombot_command, run_script, stop_script) are gated by the " +
                           "approval level configured in the MTC client.",
        };

    private async Task<object> CallToolAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        string? toolName = parameters?.TryGetProperty("name", out JsonElement nameElement) == true &&
                           nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()
            : null;

        McpToolDescriptor? tool = toolName == null ? null : McpToolSchema.FindTool(toolName);
        if (tool == null)
            throw new MtcRpcException(-32602, $"Unknown tool: {toolName ?? "(missing name)"}");

        JsonElement? arguments = null;
        if (parameters is JsonElement parametersElement &&
            parametersElement.TryGetProperty("arguments", out JsonElement argumentsElement) &&
            argumentsElement.ValueKind == JsonValueKind.Object)
        {
            arguments = argumentsElement;
        }

        foreach (string required in tool.RequiredParameters)
        {
            if (arguments?.TryGetProperty(required, out JsonElement requiredElement) != true ||
                requiredElement.ValueKind == JsonValueKind.Null)
            {
                throw new MtcRpcException(-32602, $"Tool '{tool.Name}' requires argument '{required}'.");
            }
        }

        try
        {
            object? result = await _owner.InvokeForMcpAsync(tool.JsonRpcMethod, arguments, cancellationToken).ConfigureAwait(false);
            return McpToolSchema.BuildToolCallResult(tool, result);
        }
        catch (MtcRpcException ex)
        {
            // Approval rejections (-32002 read-only, -32003 rejected by player) surface as tool errors
            // so the agent can react; the request itself is still protocol-valid.
            return McpToolSchema.BuildToolCallError(ex.Message);
        }
    }

    private static object DescribeTool(McpToolDescriptor tool)
        => new
        {
            name = tool.Name,
            description = tool.Description,
            inputSchema = McpToolSchema.BuildInputSchema(tool),
            annotations = new { readOnlyHint = tool.ReadOnly },
        };

    private static JsonRpcResponseObject McpServerResponse(object? id, object result)
        => new()
        {
            Id = id,
            Result = result,
        };

    private static JsonRpcResponseObject McpServerResponse(object? id, JsonRpcErrorObject error)
        => new()
        {
            Id = id,
            Error = error,
        };

    private static async Task WriteJsonAsync(
        HttpListenerResponse response,
        object payload,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, McpJson.Options));
        if (sessionId != null)
            response.Headers["Mcp-Session-Id"] = sessionId;
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.OutputStream.Close();
    }
}
