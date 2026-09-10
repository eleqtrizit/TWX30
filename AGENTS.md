# TWX Proxy 3.0 beta5

C#/.NET rewrite of the classic TWX Proxy helper for Trade Wars 2002.

## Layout

- `Source/` — active code
  - `TWXProxy/` — shared Core runtime: compiler support, script execution, proxy logic, database code
  - `TWXC/` — command-line compiler (`.ts` -> `.cts`)
  - `TWXD/` — command-line decompiler (`.cts` -> `.ts`)
  - `MTC/` — Avalonia desktop client with an embedded proxy
  - `TWXP/` — Avalonia multi-threaded proxy
- `Tests/` — test projects
- `include/`, `scripts/`, `docs/` — supporting files and documentation
- `twxwiki/`, `paths.md` — game-related content

Build details, project layout, and tooling notes: see `Source/README.md`.

## Scripting references

- `TWX_SCRIPTING_GUIDE.md` — guide to the TWX script language (commands, syntax, triggers) and how it executes in TWXProxy. Read before writing or debugging `.ts`/`.cts` scripts.
- `SCRIPT_INVENTORY.md` — one-line description of every bundled script in `scripts/` (Pack1/Pack2, LoneStar, Promethius, RammaR, Oz, mombot suite) and the shared `include/` libraries. Consult it to find an existing script or helper before authoring a new one.

## Starting the MTC client — single copy, no respawn

When you (the agent) need the MTC app running (e.g. to use the `mtc_*` tools):

1. **Start exactly one detached copy, with the required flags:**
   ```bash
   nohup "$HOME/.dotnet/dotnet" run --project Source/MTC/MTC.csproj -- --mcp --agent-mode >/dev/null 2>&1 &
   ```
   Plain `dotnet run` (no flags) starts the UI but **no MCP server on port 7623** —
   every `mtc_*` tool call fails with "fetch failed". `--mcp` opens the MCP
   endpoint; `--agent-mode` suppresses dialogues and enables server observation.

2. **Wait for the listener, not just the process.** First run builds (~10-15s),
   then the app self-relaunches itself detached once (`UnixAutoDetach`) — normal.
   Confirm readiness with:
   ```bash
   lsof -iTCP:7623 -sTCP:LISTEN
   ```
   A check right after launch will show nothing; that's the build, not a failure.

3. **Refresh the MCP gateway after a restart.** The gateway can cache
   "mtc unavailable"; if a tool call fails with "Server mtc not available",
   call `mcp({ connect: "mtc" })` once, then retry.

4. **NEVER set up respawn logic. This means:**
   - No `while`/`until` loops that relaunch the app when it exits.
   - No watchdog/retry wrappers (`while true; do dotnet run ...; sleep N; done`).
   - No `dotnet watch` (`make debug`) for normal runs — it relaunches the app on
     every file save; use `make debug` only when deliberately doing live-reload
     development, and stop it with Ctrl-C when done.
   - Do not use `nohup ... &` inside shell loops or with `||` retry chains.

   If the app dies, do not restart it in a loop. Stop, check the logs/output,
   and fix the underlying cause first. Restart at most once, manually, only
   after a deliberate fix.
