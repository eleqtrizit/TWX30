using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MTC;

/// <summary>
/// Describes one MCP tool exposed by the MTC MCP endpoint, including its JSON Schema input
/// and the <c>mtc.*</c> JSON-RPC method that actually implements it.
/// </summary>
internal sealed record McpToolDescriptor
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string JsonRpcMethod { get; init; }
    public required bool ReadOnly { get; init; }
    public required Dictionary<string, McpToolParameter> Parameters { get; init; }
    public required string[] RequiredParameters { get; init; }
}

/// <summary>One inputSchema property of an MCP tool.</summary>
/// <param name="Type">JSON Schema type of the parameter</param>
/// <param name="Description">Human-readable description shown to the agent</param>
internal sealed record McpToolParameter(string Type, string Description);

/// <summary>
/// Builds the MCP <c>tools/list</c> payload from a static mapping onto the proven
/// <c>mtc.*</c> JSON-RPC handlers, so the MCP surface never drifts from the RPC surface.
/// Parameter names intentionally match the JSON-RPC parameter names consumed by
/// <see cref="MtcJsonRpcServer.InvokeMethodAsync"/>.
/// </summary>
internal static class McpToolSchema
{
    private const int MaxRecentEventLimit = 700;

    private static readonly Dictionary<string, McpToolParameter> NoParameters = new();

    private static readonly Dictionary<string, McpToolParameter> RecentEventCountParameters = new()
    {
        ["recentEventCount"] = new("integer", $"Number of recent events to include (0-{MaxRecentEventLimit}, default 80)."),
    };

    public static IReadOnlyList<McpToolDescriptor> DescribeTools() =>
    [
        new()
        {
            Name = "get_context",
            Description = "Read the compact live game context: game name, connection state, current sector, prompt, online players, running scripts, bot mode, and the copilot recommendation.",
            JsonRpcMethod = "mtc.getContext",
            ReadOnly = true,
            Parameters = RecentEventCountParameters,
            RequiredParameters = [],
        },
        new()
        {
            Name = "get_recent_events",
            Description = "Read the last N observed game events as structured records, optionally including raw ANSI text.",
            JsonRpcMethod = "mtc.getRecentEvents",
            ReadOnly = true,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["limit"] = new("integer", $"Maximum number of events to return (1-{MaxRecentEventLimit}, default 120)."),
                ["includeAnsi"] = new("boolean", "Include raw ANSI escape sequences in each event (default false)."),
            },
            RequiredParameters = [],
        },
        new()
        {
            Name = "get_copilot_recommendation",
            Description = "Get the deterministic copilot's next structured action recommendation without sending anything to the game.",
            JsonRpcMethod = "mtc.getCopilotRecommendation",
            ReadOnly = true,
            Parameters = RecentEventCountParameters,
            RequiredParameters = [],
        },
        new()
        {
            Name = "query_sector",
            Description = "Read sector details from the local database snapshot: warps in/out, port, planets, fighters, armid and limpet mines.",
            JsonRpcMethod = "mtc.querySector",
            ReadOnly = true,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["sector"] = new("integer", "Sector number to query (1 or higher)."),
            },
            RequiredParameters = ["sector"],
        },
        new()
        {
            Name = "list_scripts",
            Description = "List scripts currently running in the interpreter: id, name, paused and bot flags.",
            JsonRpcMethod = "mtc.listScripts",
            ReadOnly = true,
            Parameters = NoParameters,
            RequiredParameters = [],
        },
        new()
        {
            Name = "propose_command",
            Description = "Draft a terminal command for player review without sending it to the game.",
            JsonRpcMethod = "mtc.proposeCommand",
            ReadOnly = true,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["command"] = new("string", "The command text to propose."),
            },
            RequiredParameters = ["command"],
        },
        new()
        {
            Name = "send_command",
            Description = "Send raw terminal input to the game stream.",
            JsonRpcMethod = "mtc.sendCommand",
            ReadOnly = false,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["command"] = new("string", "The terminal input to send."),
                ["appendEnter"] = new("boolean", "Append a carriage return after the command (default true)."),
            },
            RequiredParameters = ["command"],
        },
        new()
        {
            Name = "send_and_wait",
            Description = "Send a command to the game and wait for the next prompt, returning every line captured in between. Prefer this over send_command whenever the response matters: it is a single round trip.",
            JsonRpcMethod = "mtc.sendAndWait",
            ReadOnly = false,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["command"] = new("string", "Command text to submit to the game."),
                ["appendEnter"] = new("boolean", "Append a carriage return to the command (default true)."),
                ["timeoutSeconds"] = new("number", "How long to wait for the next prompt in seconds (0.5-90, default 8)."),
            },
            RequiredParameters = ["command"],
        },
        new()
        {
            Name = "run_mombot_command",
            Description = "Run a native MTC Mombot command, e.g. 't 1234' for twarp or 'm 1234' for mow.",
            JsonRpcMethod = "mtc.runMombotCommand",
            ReadOnly = false,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["command"] = new("string", "The Mombot command to run."),
            },
            RequiredParameters = ["command"],
        },
        new()
        {
            Name = "run_script",
            Description = "Start a compiled TWX script by name.",
            JsonRpcMethod = "mtc.runScript",
            ReadOnly = false,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["script"] = new("string", "Name of the compiled script to run."),
            },
            RequiredParameters = ["script"],
        },
        new()
        {
            Name = "connect_server",
            Description = "Connect to the game server (same as the Connect menu action). Pass host and port to connect to a specific server, e.g. host 'roguetw.net' and port 2002; without them, the currently configured connect settings are used. Other connection-dependent tools return 'Connect to server, first.' until this succeeds.",
            JsonRpcMethod = "mtc.connectServer",
            ReadOnly = false,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["host"] = new("string", "Server hostname or IP to connect to (optional)."),
                ["port"] = new("integer", "Server port (optional, default 2002)."),
            },
            RequiredParameters = [],
        },
        new()
        {
            Name = "disconnect_server",
            Description = "Disconnect from the game server (same as the Disconnect menu action). Returns a tool error when there is no active connection.",
            JsonRpcMethod = "mtc.disconnectServer",
            ReadOnly = false,
            Parameters = NoParameters,
            RequiredParameters = [],
        },
        new()
        {
            Name = "write_script",
            Description = "Create or overwrite a TWX script source file. The 'path' argument is RELATIVE to the scripts root directory (e.g. 'Pack2/2_Find.ts' or 'include/header.ts'); forward slashes are accepted; paths escaping the scripts root are rejected.",
            JsonRpcMethod = "mtc.writeScript",
            ReadOnly = false,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["path"] = new("string", "Path relative to the scripts root directory."),
                ["content"] = new("string", "Full file content to write (UTF-8, no BOM)."),
            },
            RequiredParameters = ["path", "content"],
        },
        new()
        {
            Name = "edit_script",
            Description = "Replace text in an existing TWX script source file. The 'path' argument is RELATIVE to the scripts root directory; paths escaping the scripts root are rejected. Replaces all occurrences when replaceAll is true, otherwise requires a unique match.",
            JsonRpcMethod = "mtc.editScript",
            ReadOnly = false,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["path"] = new("string", "Path relative to the scripts root directory."),
                ["oldText"] = new("string", "Exact text to replace (ordinal comparison)."),
                ["newText"] = new("string", "Replacement text."),
                ["replaceAll"] = new("boolean", "Replace every occurrence instead of requiring a unique match (default false)."),
            },
            RequiredParameters = ["path", "oldText", "newText"],
        },
        new()
        {
            Name = "read_script",
            Description = "Read a TWX script source file as text. The 'path' argument is RELATIVE to the scripts root directory; paths escaping the scripts root are rejected.",
            JsonRpcMethod = "mtc.readScript",
            ReadOnly = true,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["path"] = new("string", "Path relative to the scripts root directory."),
                ["offset"] = new("integer", "1-based starting line number (default 1)."),
                ["limit"] = new("integer", "Maximum number of lines to return (default 2000)."),
            },
            RequiredParameters = ["path"],
        },
        new()
        {
            Name = "compile_script",
            Description = "Compile a TWX script source file (.ts) with the TWX compiler to check it compiles, and optionally write the compiled .cts next to it so run_script can start it. The 'path' argument is RELATIVE to the scripts root directory (e.g. 'Pack2/2_Find.ts'); paths escaping the scripts root are rejected. Returns compiler diagnostics on failure and code size / line / definition counts on success.",
            JsonRpcMethod = "mtc.compileScript",
            ReadOnly = false,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["path"] = new("string", "Path of the .ts source file relative to the scripts root directory."),
                ["run"] = new("boolean", "When true (default), writes the compiled .cts next to the source file. When false, only checks compilation and writes nothing."),
            },
            RequiredParameters = ["path"],
        },
        new()
        {
            Name = "stop_script",
            Description = "Stop a running script by interpreter id or script name.",
            JsonRpcMethod = "mtc.stopScript",
            ReadOnly = false,
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["id"] = new("integer", "Interpreter id of the script to stop."),
                ["name"] = new("string", "Name of the script to stop (used when id is omitted)."),
            },
            RequiredParameters = [],
        },
    ];

    /// <summary>Builds the JSON Schema <c>inputSchema</c> object for a tool.</summary>
    /// <param name="tool">The tool descriptor to build the schema for</param>
    /// <returns>The schema as a serializable anonymous object</returns>
    public static object BuildInputSchema(McpToolDescriptor tool)
    {
        var properties = new Dictionary<string, object>();
        foreach ((string name, McpToolParameter parameter) in tool.Parameters)
        {
            properties[name] = new { type = parameter.Type, description = parameter.Description };
        }

        return new
        {
            type = "object",
            properties,
            required = tool.RequiredParameters,
        };
    }

    /// <summary>Finds a tool descriptor by MCP tool name.</summary>
    /// <param name="name">The tool name from a tools/call request</param>
    /// <returns>The matching descriptor, or null when unknown</returns>
    public static McpToolDescriptor? FindTool(string name)
    {
        foreach (McpToolDescriptor tool in DescribeTools())
        {
            if (string.Equals(tool.Name, name, StringComparison.Ordinal))
                return tool;
        }

        return null;
    }

    /// <summary>Serializes a tool call result into MCP text content, or an error result when the call failed.</summary>
    /// <param name="tool">The tool that was called</param>
    /// <param name="result">The object returned by the JSON-RPC handler</param>
    /// <returns>The MCP tools/call result payload</returns>
    public static object BuildToolCallResult(McpToolDescriptor tool, object? result)
    {
        string text = result switch
        {
            MtcRpcActionResult action => FormatActionResult(action),
            null => $"Tool '{tool.Name}' returned no data.",
            _ => JsonSerializer.Serialize(result, McpJson.Options),
        };

        return new
        {
            content = new[] { new { type = "text", text } },
            isError = false,
        };
    }

    /// <summary>Builds an MCP tools/call error result for a failed or blocked call.</summary>
    /// <param name="message">Human-readable error text shown to the agent</param>
    /// <returns>The MCP tools/call result payload with isError set</returns>
    public static object BuildToolCallError(string message)
        => new
        {
            content = new[] { new { type = "text", text = message } },
            isError = true,
        };

    private static string FormatActionResult(MtcRpcActionResult action)
    {
        var lines = new List<string>
        {
            $"success: {action.Success.ToString().ToLowerInvariant()}",
            action.Message,
        };
        foreach ((string key, string value) in action.Data)
            lines.Add($"{key}: {value}");
        return string.Join('\n', lines);
    }
}

/// <summary>Shared JSON serializer options for MCP payloads (camelCase, nulls omitted).</summary>
internal static class McpJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
