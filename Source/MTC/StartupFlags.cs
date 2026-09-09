namespace MTC;

/// <summary>
/// Process-scoped flags set from command-line arguments before the UI starts.
/// </summary>
internal static class MtcStartupFlags
{
    /// <summary>
    /// When true (command-line flag <c>--mcp</c>), the JSON-RPC/MCP listener starts immediately
    /// at application startup, without waiting for a loaded game context.
    /// </summary>
    public static bool JsonRpcEnabledAtStartup { get; private set; }

    /// <summary>Parses startup flags from the command-line arguments.</summary>
    /// <param name="args">Command-line arguments passed to the executable</param>
    public static void Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        foreach (string arg in args)
        {
            if (string.Equals(arg, "--mcp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--mcp-on", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--json-rpc", StringComparison.OrdinalIgnoreCase))
            {
                JsonRpcEnabledAtStartup = true;
            }
        }
    }
}
