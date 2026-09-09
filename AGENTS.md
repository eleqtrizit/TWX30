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

## Starting the MTC client — single copy, no respawn

When you (the agent) need the MTC app running (e.g. to use the `mtc_*` tools):

1. **Start exactly one detached copy:**
   ```bash
   nohup $(HOME)/.dotnet/dotnet run --project Source/MTC/MTC.csproj >/dev/null 2>&1 &
   ```
   Run this command once, then verify with `pgrep` that a single copy is up.
   Note: on macOS/Linux the app self-relaunches itself detached once at startup
   (`UnixAutoDetach`) — this is normal and results in one detached copy. Wait a
   moment and re-check `pgrep` before concluding anything is wrong.

2. **NEVER set up respawn logic. This means:**
   - No `while`/`until` loops that relaunch the app when it exits.
   - No watchdog/retry wrappers (`while true; do dotnet run ...; sleep N; done`).
   - No `dotnet watch` (`make debug`) for normal runs — it relaunches the app on
     every file save; use `make debug` only when deliberately doing live-reload
     development, and stop it with Ctrl-C when done.
   - Do not use `nohup ... &` inside shell loops or with `||` retry chains.

   If the app dies, do not restart it in a loop. Stop, check the logs/output,
   and fix the underlying cause first. Restart at most once, manually, only
   after a deliberate fix.
