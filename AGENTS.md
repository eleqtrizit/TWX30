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
