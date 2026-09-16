# twxproxy 3.0 beta5

twxproxy 3.0 beta5 is the current C#/.NET rewrite of the classic TWX Proxy helper for Trade Wars 2002.

Current source version: `3.0 beta5`

The active code lives under `Source/` and includes:

- `TWXProxy`: shared Core runtime, compiler support, script execution, proxy logic, and database code
- `TWXC`: command-line compiler for `.ts -> .cts`
- `TWXD`: command-line decompiler for `.cts -> .ts`
- `MTC`: Avalonia desktop client with an embedded proxy
- `TWXP`: Avalonia based multi threaded proxy

For build details, project layout, and tooling notes, see [`Source/README.md`](Source/README.md).

## MTC MCP Server

MTC ships an embedded MCP (Model Context Protocol) server that lets coding agents drive the game, the proxy, and MOMBot programmatically. Launch it with:

```bash
dotnet run --project Source/MTC/MTC.csproj -- --mcp --agent-mode
```

`--mcp` opens the MCP endpoint on port `7623`; `--agent-mode` suppresses UI dialogs and enables server observation. Point an MCP client at `http://localhost:7623` (or use an MCP gateway) after the port is listening.

### Tool Catalog

Tools marked *read-only* do not send anything to the game stream.

| Tool | Type | Description |
|------|------|-------------|
| `get_context` | read-only | Compact live game context: connection state, sector, prompt, bot mode, copilot recommendation |
| `get_recent_events` | read-only | Last N observed game events as structured records (optionally with raw ANSI text) |
| `get_copilot_recommendation` | read-only | Deterministic next-action recommendation without sending anything to the game |
| `query_sector` | read-only | Sector details from the local database snapshot: warps, port, planets, fighters, mines |
| `list_scripts` | read-only | Scripts currently running in the interpreter (id, name, paused/bot flags) |
| `connect_server` | mutating | Connect to the game server (host/port optional; uses configured defaults) |
| `disconnect_server` | mutating | Disconnect from the game server |
| `send_command` | mutating | Send raw terminal input to the game stream |
| `send_and_wait` | mutating | Send a command and wait for the next prompt, returning all lines in between |
| `propose_command` | mutating | Draft a terminal command for player review without sending it |
| `run_script` / `stop_script` | mutating | Start or stop a compiled TWX script by name/id |
| `write_script` / `edit_script` / `read_script` | mutating | Create, patch, and read TWX script source files relative to the scripts root |
| `compile_script` | mutating | Compile a `.ts` script with the TWX compiler, optionally writing the `.cts` next to it |
| `mombot_status` | read-only | Structured MOMBot status: enabled, auto-start, watcher state, command routes, connection state |
| `configure_mombot` | mutating | Write MOMBot relog settings (login, password, game letter) and persist the game config |
| `set_mombot_enabled` | mutating | Start or stop the native MOMBot through the same path the Bot menu uses |
| `send_mombot_page` | mutating | Send a MOMBot command as an in-game subspace page, optionally collecting the bot's response |
| `run_mombot_command` | mutating | Run a native MOMBot command locally (non-game-stream), e.g. `t 1234` for twarp |

All tools are exposed both as MCP tools and as JSON-RPC methods on the same endpoint (method names: `mtc.<camelCase>` — e.g. `mtc.sendCommand`, `mtc.setMombotEnabled`).

For agent-side setup notes (single copy, port wait, no respawn loops), see `AGENTS.md` in the repository root. MOMBot usage and its command catalog are documented in [`MOMBOT.md`](MOMBOT.md).
