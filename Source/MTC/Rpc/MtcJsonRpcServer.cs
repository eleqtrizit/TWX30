using System;
using System.Net;
using System.Net.WebSockets;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace MTC;

internal sealed class MtcJsonRpcServer : IDisposable
{
    private const int DefaultRecentEventLimit = 120;
    private const int MaxRecentEventLimit = 700;
    private const int SendQueueLimit = 512;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly MtcRpcBridge _bridge;
    private readonly object _sync = new();

    private readonly Dictionary<Guid, MtcRpcWebSocketClient> _clients = [];

    /// <summary>MCP endpoint sharing this server's listener, auth token, and method handlers.</summary>
    public McpServer McpServer { get; }

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private MtcJsonRpcServerOptions _options = new();
    private bool _disposed;

    public MtcJsonRpcServer(MtcRpcBridge bridge)
    {
        _bridge = bridge;
        McpServer = new McpServer(this);
    }

    /// <summary>Dispatches an MCP tool call onto the same JSON-RPC method handlers used by HTTP/WebSocket clients.</summary>
    /// <param name="method">The mtc.* method backing the tool</param>
    /// <param name="parameters">Tool arguments, matching the JSON-RPC parameter names</param>
    /// <param name="cancellationToken">Token cancelled when the server stops</param>
    /// <returns>The handler result, or throws <see cref="MtcRpcException"/> for approval/parameter failures</returns>
    internal Task<object?> InvokeForMcpAsync(string method, JsonElement? parameters, CancellationToken cancellationToken)
        => InvokeMethodAsync(method, parameters, client: null, cancellationToken);

    public bool IsRunning
    {
        get
        {
            lock (_sync)
                return _listener != null;
        }
    }

    public string Endpoint
    {
        get
        {
            lock (_sync)
                return _options.Endpoint;
        }
    }

    public bool HasSubscribedClients
    {
        get
        {
            lock (_sync)
                return _clients.Values.Any(client => client.Subscribed);
        }
    }

    public void ApplyOptions(MtcJsonRpcServerOptions options)
    {
        options = NormalizeOptions(options);
        lock (_sync)
        {
            if (_disposed)
                return;

            if (!options.Enabled)
            {
                StopUnderLock();
                _options = options;
                return;
            }

            bool needsRestart =
                _listener == null ||
                !string.Equals(_options.BindAddress, options.BindAddress, StringComparison.OrdinalIgnoreCase) ||
                _options.Port != options.Port;

            _options = options;
            if (!needsRestart)
                return;

            StopUnderLock();
            StartUnderLock(options);
        }
    }

    public void Stop()
    {
        lock (_sync)
            StopUnderLock();
    }

    private void StartUnderLock(MtcJsonRpcServerOptions options)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(options.Endpoint);
        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
            when (ex.ErrorCode == 48 ||
                  ex.ErrorCode == 98 ||
                  ex.Message.Contains("Address already in use", StringComparison.OrdinalIgnoreCase))
        {
            listener.Close();
            throw new InvalidOperationException(
                $"JSON-RPC endpoint {options.Endpoint} is already in use by another MTC instance or process.",
                ex);
        }
        catch
        {
            listener.Close();
            throw;
        }

        _listener = listener;
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoopAsync(listener, _cts.Token));
        TWXProxy.Core.GlobalModules.DebugLog($"[MTC.JsonRpc] listening on {options.Endpoint}\n");
    }

    private void StopUnderLock()
    {
        HttpListener? listener = _listener;
        CancellationTokenSource? cts = _cts;

        _listener = null;
        _cts = null;
        _listenTask = null;

        try { cts?.Cancel(); } catch { }
        try { listener?.Stop(); } catch { }
        try { listener?.Close(); } catch { }
        cts?.Dispose();

        foreach (MtcRpcWebSocketClient client in _clients.Values.ToArray())
            client.Dispose();
        _clients.Clear();
    }

    private async Task ListenLoopAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                TWXProxy.Core.GlobalModules.DebugLog($"[MTC.JsonRpc] accept failed: {ex}\n");
                continue;
            }

            _ = Task.Run(() => HandleContextAsync(context, cancellationToken), CancellationToken.None);
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (string.Equals(context.Request.Url?.AbsolutePath, "/health", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context.Response, new
                {
                    ok = true,
                    service = "MTC JSON-RPC",
                    endpoint = Endpoint,
                }, cancellationToken).ConfigureAwait(false);
                return;
            }
            if (string.Equals(context.Request.Url?.AbsolutePath, McpServer.Path, StringComparison.OrdinalIgnoreCase))
            {
                await McpServer.HandleAsync(context, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.IsWebSocketRequest)
            {
                await HandleWebSocketAsync(context, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                await WriteJsonAsync(context.Response, new { error = "POST or WebSocket required" }, cancellationToken).ConfigureAwait(false);
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8);
            string requestJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            string? responseJson = await DispatchJsonAsync(requestJson, client: null, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                context.Response.Close();
                return;
            }

            await WriteRawJsonAsync(context.Response, responseJson, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TWXProxy.Core.GlobalModules.DebugLog($"[MTC.JsonRpc] request failed: {ex}\n");
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await WriteJsonAsync(context.Response, new { error = ex.Message }, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
        var client = new MtcRpcWebSocketClient(this, wsContext.WebSocket);
        lock (_sync)
            _clients[client.Id] = client;

        try
        {
            await client.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
                _clients.Remove(client.Id);
            client.Dispose();
        }
    }

    private async Task<string?> DispatchJsonAsync(string requestJson, MtcRpcWebSocketClient? client, CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(requestJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                var responses = new List<JsonElement>();
                foreach (JsonElement request in root.EnumerateArray())
                {
                    JsonRpcResponseObject? response = await DispatchRequestAsync(request, client, cancellationToken).ConfigureAwait(false);
                    if (response == null)
                        continue;

                    responses.Add(JsonSerializer.SerializeToElement(response, JsonOptions));
                }

                return responses.Count == 0
                    ? null
                    : JsonSerializer.Serialize(responses, JsonOptions);
            }

            JsonRpcResponseObject? singleResponse = await DispatchRequestAsync(root, client, cancellationToken).ConfigureAwait(false);
            return singleResponse == null
                ? null
                : JsonSerializer.Serialize(singleResponse, JsonOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(ErrorResponse(null, -32700, "Parse error", ex.Message), JsonOptions);
        }
    }

    private async Task<JsonRpcResponseObject?> DispatchRequestAsync(JsonElement request, MtcRpcWebSocketClient? client, CancellationToken cancellationToken)
    {
        if (request.ValueKind != JsonValueKind.Object)
            return ErrorResponse(null, -32600, "Invalid Request", "Request must be a JSON object.");

        object? id = null;
        bool hasId = request.TryGetProperty("id", out JsonElement idElement);
        if (hasId)
            id = ReadJsonRpcId(idElement);

        try
        {
            if (!request.TryGetProperty("jsonrpc", out JsonElement jsonRpcElement) ||
                jsonRpcElement.GetString() != "2.0" ||
                !request.TryGetProperty("method", out JsonElement methodElement) ||
                methodElement.ValueKind != JsonValueKind.String)
            {
                return hasId ? ErrorResponse(id, -32600, "Invalid Request", null) : null;
            }

            string method = methodElement.GetString() ?? string.Empty;
            JsonElement? parameters = request.TryGetProperty("params", out JsonElement paramsElement)
                ? paramsElement
                : null;

            object? result = await InvokeMethodAsync(method, parameters, client, cancellationToken).ConfigureAwait(false);
            return hasId
                ? new JsonRpcResponseObject { Id = id, Result = result }
                : null;
        }
        catch (MtcRpcException ex)
        {
            return hasId ? ErrorResponse(id, ex.Code, ex.Message, ex.DataObject) : null;
        }
        catch (Exception ex)
        {
            TWXProxy.Core.GlobalModules.DebugLog($"[MTC.JsonRpc] dispatch failed: {ex}\n");
            return hasId ? ErrorResponse(id, -32603, "Internal error", ex.Message) : null;
        }
    }

    private async Task<object?> InvokeMethodAsync(string method, JsonElement? parameters, MtcRpcWebSocketClient? client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (method)
        {
            case "rpc.discover":
            case "mtc.getCapabilities":
                return BuildCapabilities();

            case "mtc.getContext":
            {
                int eventCount = ReadInt(parameters, "recentEventCount", 80, 0, MaxRecentEventLimit);
                return await _bridge.GetContextAsync(eventCount).ConfigureAwait(false);
            }

            case "mtc.getCopilotRecommendation":
            {
                int eventCount = ReadInt(parameters, "recentEventCount", 80, 0, MaxRecentEventLimit);
                GameAgentContextSnapshot context = await _bridge.GetContextAsync(eventCount).ConfigureAwait(false);
                return context.CopilotRecommendation;
            }

            case "mtc.getRecentEvents":
            {
                int limit = ReadInt(parameters, "limit", DefaultRecentEventLimit, 1, MaxRecentEventLimit);
                bool includeAnsi = ReadBool(parameters, "includeAnsi", false);
                return await _bridge.GetRecentEventsAsync(limit, includeAnsi).ConfigureAwait(false);
            }

            case "mtc.querySector":
            case "mtc.getSector":
            {
                int sector = ReadInt(parameters, "sector", 0, 1, int.MaxValue);
                GameAgentSectorSnapshot? snapshot = await _bridge.QuerySectorAsync(sector).ConfigureAwait(false);
                if (snapshot == null)
                    throw new MtcRpcException(-32005, $"Sector {sector} is not available in the compact MTC snapshot.");
                return snapshot;
            }

            case "mtc.listScripts":
                return await _bridge.ListScriptsAsync().ConfigureAwait(false);

            case "mtc.subscribe":
                if (client == null)
                    throw new MtcRpcException(-32006, "Subscriptions require a WebSocket connection.");
                client.Subscribed = true;
                return new { subscribed = true };

            case "mtc.unsubscribe":
                if (client == null)
                    throw new MtcRpcException(-32006, "Subscriptions require a WebSocket connection.");
                client.Subscribed = false;
                return new { subscribed = false };

            case "mtc.proposeCommand":
            {
                string command = ReadString(parameters, "command", required: true, allowEmpty: true);
                return GameAgentToolRegistry.ProposeCommand(command);
            }

            case "mtc.sendCommand":
            {
                string command = ReadString(parameters, "command", required: true, allowEmpty: true);
                bool appendEnter = ReadBool(parameters, "appendEnter", false);
                return await _bridge.SendCommandAsync(command, appendEnter).ConfigureAwait(false);
            }

            case "mtc.sendAndWait":
            {
                string command = ReadString(parameters, "command", required: true, allowEmpty: true);
                bool appendEnter = ReadBool(parameters, "appendEnter", false);
                double timeoutSeconds = ReadDouble(parameters, "timeoutSeconds", 8, 0.5, 90);
                return await _bridge.SendAndWaitAsync(command, appendEnter, timeoutSeconds).ConfigureAwait(false);
            }

            case "mtc.getMombotStatus":
                return await _bridge.GetMombotStatusAsync().ConfigureAwait(false);

            case "mtc.sendMombotPage":
            {
                string command = ReadString(parameters, "command", required: true);
                double waitSeconds = ReadDouble(parameters, "waitSeconds", 0, 0, 90);
                return await _bridge.SendMombotPageAsync(command, waitSeconds).ConfigureAwait(false);
            }

            case "mtc.runMombotCommand":
            {
                string command = ReadString(parameters, "command", required: true);
                return await _bridge.RunMombotCommandAsync(command).ConfigureAwait(false);
            }

            case "mtc.connectServer":
            {
                string? host = TryReadString(parameters, "host");
                int? port = TryReadInt(parameters, "port", 1, 65535);
                return await _bridge.ConnectServerAsync(host, port).ConfigureAwait(false);
            }

            case "mtc.disconnectServer":
            {
                return await _bridge.DisconnectServerAsync().ConfigureAwait(false);
            }

            case "mtc.readScript":
            {
                string path = ReadString(parameters, "path", required: true);
                int offset = ReadInt(parameters, "offset", 1, 1, int.MaxValue);
                int limit = ReadInt(parameters, "limit", 2000, 1, int.MaxValue);
                return await _bridge.ReadScriptAsync(path, offset, limit).ConfigureAwait(false);
            }

            case "mtc.writeScript":
            {
                string path = ReadString(parameters, "path", required: true);
                string content = ReadString(parameters, "content", required: true);
                return await _bridge.WriteScriptAsync(path, content).ConfigureAwait(false);
            }

            case "mtc.editScript":
            {
                string path = ReadString(parameters, "path", required: true);
                string oldText = ReadString(parameters, "oldText", required: true);
                string newText = ReadString(parameters, "newText", required: true);
                bool replaceAll = ReadBool(parameters, "replaceAll", false);
                return await _bridge.EditScriptAsync(path, oldText, newText, replaceAll).ConfigureAwait(false);
            }

            case "mtc.compileScript":
            {
                string path = ReadString(parameters, "path", required: true);
                bool run = ReadBool(parameters, "run", true);
                return await _bridge.CompileScriptAsync(path, run).ConfigureAwait(false);
            }

            case "mtc.runScript":
            {
                string script = ReadString(parameters, "script", required: true);
                MtcRpcActionResult started = await _bridge.RunScriptAsync(script).ConfigureAwait(false);
                McpServer.PublishScriptState("started", script, started);
                return started;
            }

            case "mtc.stopScript":
            {
                int? id = TryReadInt(parameters, "id", 0, int.MaxValue);
                string? name = TryReadString(parameters, "name");
                if (id == null && string.IsNullOrWhiteSpace(name))
                    throw new MtcRpcException(-32602, "Either id or name is required.");

                MtcRpcActionResult stopped = await _bridge.StopScriptAsync(id, name).ConfigureAwait(false);
                McpServer.PublishScriptState("stopped", id?.ToString() ?? name ?? string.Empty, stopped);
                return stopped;
            }

            default:
                throw new MtcRpcException(-32601, "Method not found", method);
        }
    }

    private object BuildCapabilities()
    {
        MtcJsonRpcServerOptions options = _options;
        return new
        {
            schema = "mtc.rpc.v1",
            protocol = "json-rpc-2.0",
            transport = new[] { "http-post", "websocket" },
            endpoint = options.Endpoint,
            methods = new[]
            {
                "rpc.discover",
                "mtc.getCapabilities",
                "mtc.getContext",
                "mtc.getCopilotRecommendation",
                "mtc.getRecentEvents",
                "mtc.querySector",
                "mtc.getSector",
                "mtc.listScripts",
                "mtc.subscribe",
                "mtc.unsubscribe",
                "mtc.proposeCommand",
                "mtc.connectServer",
                "mtc.disconnectServer",
                "mtc.sendCommand",
                "mtc.runMombotCommand",
                "mtc.getMombotStatus",
                "mtc.sendMombotPage",
                "mtc.runScript",
                "mtc.stopScript",
                "mtc.writeScript",
                "mtc.editScript",
                "mtc.readScript",
                "mtc.compileScript",
            },
        };
    }


    private void OnGameAgentEventRecorded(GameAgentEvent evt)
    {
        MtcRpcWebSocketClient[] clients;
        lock (_sync)
            clients = _clients.Values.ToArray();

        foreach (MtcRpcWebSocketClient client in clients)
            client.EnqueueEvent(evt);

        McpServer.PublishEvent(evt);
        if (evt.Metadata.TryGetValue("scriptEvent", out string? scriptEvent) &&
            string.Equals(scriptEvent, "error", StringComparison.OrdinalIgnoreCase) &&
            evt.Metadata.TryGetValue("script", out string? scriptName))
        {
            McpServer.PublishScriptState("error", scriptName, MtcRpcActionResult.Fail(evt.PlainText));
        }
    }

    public void PublishGameAgentEvent(GameAgentEvent evt)
        => OnGameAgentEventRecorded(evt);


    private static JsonRpcResponseObject ErrorResponse(object? id, int code, string message, object? data)
        => new()
        {
            Id = id,
            Error = new JsonRpcErrorObject
            {
                Code = code,
                Message = message,
                Data = data,
            },
        };

    private static object? ReadJsonRpcId(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out long longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out double doubleValue) => doubleValue,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };

    private static int ReadInt(JsonElement? parameters, string name, int defaultValue, int min, int max)
        => TryReadInt(parameters, name, min, max) ?? defaultValue;

    private static double ReadDouble(JsonElement? parameters, string name, double defaultValue, double min, double max)
    {
        if (!TryGetParam(parameters, name, out JsonElement value))
            return defaultValue;

        double parsed;
        if (value.ValueKind == JsonValueKind.Number)
        {
            parsed = value.GetDouble();
        }
        else if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out parsed))
        {
        }
        else
        {
            throw new MtcRpcException(-32602, $"Parameter '{name}' must be a number.");
        }

        return Math.Clamp(parsed, min, max);
    }

    private static int? TryReadInt(JsonElement? parameters, string name, int min, int max)
    {
        if (!TryGetParam(parameters, name, out JsonElement value))
            return null;

        int parsed;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out parsed))
            return Math.Clamp(parsed, min, max);
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed))
            return Math.Clamp(parsed, min, max);

        throw new MtcRpcException(-32602, $"Parameter '{name}' must be an integer.");
    }

    private static bool ReadBool(JsonElement? parameters, string name, bool defaultValue)
    {
        if (!TryGetParam(parameters, name, out JsonElement value))
            return defaultValue;
        if (value.ValueKind == JsonValueKind.True)
            return true;
        if (value.ValueKind == JsonValueKind.False)
            return false;
        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out bool parsed))
            return parsed;

        throw new MtcRpcException(-32602, $"Parameter '{name}' must be a boolean.");
    }

    private static string ReadString(JsonElement? parameters, string name, bool required, bool allowEmpty = false)
    {
        string? value = TryReadString(parameters, name);
        if (required && value == null)
            throw new MtcRpcException(-32602, $"Parameter '{name}' is required.");
        if (required && !allowEmpty && string.IsNullOrWhiteSpace(value))
            throw new MtcRpcException(-32602, $"Parameter '{name}' is required.");
        return value ?? string.Empty;
    }

    private static string? TryReadString(JsonElement? parameters, string name)
    {
        if (!TryGetParam(parameters, name, out JsonElement value))
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new MtcRpcException(-32602, $"Parameter '{name}' must be a string.");
        return value.GetString();
    }

    private static bool TryGetParam(JsonElement? parameters, string name, out JsonElement value)
    {
        value = default;
        if (parameters == null)
            return false;

        JsonElement element = parameters.Value;
        if (element.ValueKind != JsonValueKind.Object)
            throw new MtcRpcException(-32602, "Named params object is required.");

        return element.TryGetProperty(name, out value);
    }

    private static MtcJsonRpcServerOptions NormalizeOptions(MtcJsonRpcServerOptions options)
    {
        string bindAddress = string.IsNullOrWhiteSpace(options.BindAddress)
            ? "127.0.0.1"
            : options.BindAddress.Trim();

        int port = options.Port is >= 1024 and <= 65535 ? options.Port : 7623;
        return new MtcJsonRpcServerOptions
        {
            Enabled = options.Enabled,
            BindAddress = bindAddress,
            Port = port,
        };
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object payload, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(payload, JsonOptions);
        await WriteRawJsonAsync(response, json, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteRawJsonAsync(HttpListenerResponse response, string json, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            StopUnderLock();
            McpServer.Dispose();
        }
    }

    private sealed class MtcRpcWebSocketClient : IDisposable
    {
        private readonly MtcJsonRpcServer _server;
        private readonly WebSocket _socket;
        private readonly Channel<string> _sendQueue = Channel.CreateBounded<string>(
            new BoundedChannelOptions(SendQueueLimit)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

        private bool _disposed;

        public MtcRpcWebSocketClient(MtcJsonRpcServer server, WebSocket socket)
        {
            _server = server;
            _socket = socket;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public bool Subscribed { get; set; }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            Task sendTask = Task.Run(() => SendLoopAsync(cancellationToken), CancellationToken.None);
            try
            {
                await ReceiveLoopAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendQueue.Writer.TryComplete();
                try { await sendTask.ConfigureAwait(false); } catch { }
            }
        }

        public void EnqueueEvent(GameAgentEvent evt)
        {
            if (!Subscribed || _disposed)
                return;

            var notification = new
            {
                jsonrpc = "2.0",
                method = "mtc.event",
                @params = evt,
            };
            _sendQueue.Writer.TryWrite(JsonSerializer.Serialize(notification, JsonOptions));
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[16 * 1024];
            while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await CloseAsync(cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                string requestJson = Encoding.UTF8.GetString(message.ToArray());
                string? responseJson = await _server.DispatchJsonAsync(requestJson, this, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(responseJson))
                    _sendQueue.Writer.TryWrite(responseJson);
            }
        }

        private async Task SendLoopAsync(CancellationToken cancellationToken)
        {
            await foreach (string message in _sendQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_socket.State != WebSocketState.Open)
                    break;

                byte[] bytes = Encoding.UTF8.GetBytes(message);
                await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task CloseAsync(CancellationToken cancellationToken)
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _disposed = true;
            _sendQueue.Writer.TryComplete();
            try { _socket.Dispose(); } catch { }
        }
    }
}
