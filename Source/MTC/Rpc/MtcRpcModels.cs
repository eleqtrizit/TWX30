using System.Text.Json.Serialization;

namespace MTC;

internal sealed class MtcJsonRpcServerOptions
{
    public bool Enabled { get; init; }
    public string BindAddress { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 7623;

    public string Endpoint => $"http://{BindAddress}:{Port}/";
}

internal sealed class MtcRpcActionResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, string> Data { get; init; } = [];

    public static MtcRpcActionResult Ok(string message, Dictionary<string, string>? data = null)
        => new()
        {
            Success = true,
            Message = message,
            Data = data ?? [],
        };

    public static MtcRpcActionResult Fail(string message, Dictionary<string, string>? data = null)
        => new()
        {
            Success = false,
            Message = message,
            Data = data ?? [],
        };
}

internal sealed class MtcRpcBridge
{
    public required Func<int, Task<GameAgentContextSnapshot>> GetContextAsync { get; init; }
    public required Func<int, bool, Task<IReadOnlyList<GameAgentEvent>>> GetRecentEventsAsync { get; init; }
    public required Func<int, Task<GameAgentSectorSnapshot?>> QuerySectorAsync { get; init; }
    public required Func<Task<IReadOnlyList<GameAgentRunningScriptSnapshot>>> ListScriptsAsync { get; init; }
    public required Func<string, bool, Task<MtcRpcActionResult>> SendCommandAsync { get; init; }
    public required Func<string, Task<MtcRpcActionResult>> RunMombotCommandAsync { get; init; }
    public required Func<string, Task<MtcRpcActionResult>> RunScriptAsync { get; init; }
    public required Func<int?, string?, Task<MtcRpcActionResult>> StopScriptAsync { get; init; }
    public required Func<string?, int?, Task<MtcRpcActionResult>> ConnectServerAsync { get; init; }
    public required Func<Task<MtcRpcActionResult>> DisconnectServerAsync { get; init; }
    public required Func<string, string, Task<MtcRpcActionResult>> WriteScriptAsync { get; init; }
    public required Func<string, string, string, bool, Task<MtcRpcActionResult>> EditScriptAsync { get; init; }
    public required Func<string, int, int, Task<MtcScriptReadResult>> ReadScriptAsync { get; init; }
}

/// <summary>Result of a script file read: sliced lines plus metadata for the agent.</summary>
internal sealed record MtcScriptReadResult
{
    public required string Path { get; init; }
    public required int TotalLines { get; init; }
    public required int Offset { get; init; }
    public required string Content { get; init; }
}

internal sealed class MtcRpcException : Exception
{
    public MtcRpcException(int code, string message, object? data = null)
        : base(message)
    {
        Code = code;
        DataObject = data;
    }

    public int Code { get; }
    public object? DataObject { get; }
}

internal sealed class JsonRpcErrorObject
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; init; }
}

internal sealed class JsonRpcResponseObject
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcErrorObject? Error { get; init; }

    [JsonPropertyName("id")]
    public object? Id { get; init; }
}
