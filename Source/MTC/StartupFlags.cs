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

    /// <summary>
    /// Optional listener port override (<c>--port N</c>), for running several MTC copies side
    /// by side. When set, it takes precedence over the port configured in preferences.
    /// </summary>
    public static int? JsonRpcPortOverride { get; private set; }

    /// <summary>
    /// True while the main window is performing its startup pass. While true, a JSON-RPC bind
    /// failure terminates the application instead of only reporting the error.
    /// </summary>
    public static bool InStartup { get; set; }

    /// <summary>
    /// When true (command-line flag <c>--fail-immediately</c>), the application verifies the
    /// JSON-RPC port is free before the UI starts and exits with a CLI diagnostic if not.
    /// </summary>
    public static bool FailImmediately { get; private set; }

    /// <summary>
    /// When true (command-line flag <c>--agent-mode</c>), the application runs non-interactively
    /// for automation: startup and shutdown dialogs (tab recovery, update checks, close
    /// confirmations) are suppressed so an unattended session never blocks on a prompt.
    /// </summary>
    public static bool AgentMode { get; private set; }

    /// <summary>Parses startup flags from the command-line arguments.</summary>
    /// <param name="args">Command-line arguments passed to the executable</param>
    public static void Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "--mcp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--mcp-on", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--json-rpc", StringComparison.OrdinalIgnoreCase))
            {
                JsonRpcEnabledAtStartup = true;
                continue;
            }

            if (string.Equals(arg, "--fail-immediately", StringComparison.OrdinalIgnoreCase))
            {
                FailImmediately = true;
                continue;
            }

            if (string.Equals(arg, "--agent-mode", StringComparison.OrdinalIgnoreCase))
            {
                AgentMode = true;
                continue;
            }

            (string name, string? inlineValue) = SplitSwitch(arg);
            if (!string.Equals(name, "--port", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "--json-rpc-port", StringComparison.OrdinalIgnoreCase))
                continue;

            string valueText = inlineValue ?? (i + 1 < args.Count ? args[i + 1] : string.Empty);
            if (int.TryParse(valueText, out int port) && port is >= 1024 and <= 65535)
                JsonRpcPortOverride = port;
        }
    }

    private static (string Name, string? InlineValue) SplitSwitch(string arg)
    {
        int separator = arg.IndexOf('=');
        return separator < 0 ? (arg, null) : (arg[..separator], arg[(separator + 1)..]);
    }

    /// <summary>
    /// Checks whether a TCP listener can bind the given address and port. Mirrors what the
    /// JSON-RPC server will attempt during startup.
    /// </summary>
    /// <param name="bindAddress">Bind address configured for the listener</param>
    /// <param name="port">Listener port (1024-65535)</param>
    /// <returns>True when something is already listening on the port</returns>
    public static bool IsPortInUse(string bindAddress, int port)
    {
        if (port is < 1024 or > 65535)
            return false;

        var address = System.Net.IPAddress.Loopback;
        if (!string.IsNullOrWhiteSpace(bindAddress) &&
            !System.Net.IPAddress.TryParse(bindAddress, out address))
        {
            address = System.Net.IPAddress.Loopback;
        }

        try
        {
            using var probe = new System.Net.Sockets.TcpListener(address, port);
            probe.Start();
            return false;
        }
        catch (System.Net.Sockets.SocketException)
        {
            return true;
        }
    }
}
