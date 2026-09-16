# Gameplay via MOMBot — agent playbook

Operational notes for driving TW2002 through the MTC client and the native
MOMBot. Reusable in any game. Generic game tips live in GAMEPLAY.md; this
session's sector/port data lives in the Session log at the bottom.

## MTC/MOMBot setup (agent-side)

- MTC must run with `--mcp --agent-mode` (see AGENTS.md for launch + port
  wait).
- Enable sequence after connecting: `mtc_configure_mombot` (login name,
  password, game letter — writes relog settings and marks the bot configured),
  then `mtc_set_mombot_enabled true`, then verify with `mtc_mombot_status`.
- After a MTC restart the game connection is dropped; reconnect and log in
  again before configuring/enabling the bot.
- `mtc_run_mombot_command` failed while the bot was not enabled; once enabled,
  prefer `mtc_send_mombot_page` for round-trip commands.
- **Native dispatch is NOT agent-safe.** `run_mombot_command "status"` sent
  from MCP misrouted through the one-letter hotkey/macro layer and typed raw
  game input (opened the Onboard Computer ship catalog mid-port). Subspace
  pages (`'MomBot <command>`) are constrained to an addressable line — the
  safe channel. Recovery from a native misfire: ship catalog `Q` closes the
  Vid Term → you land at `Computer command [TL=...]` → `Q` deactivates the
  computer → back to the sector prompt.

## Getting into the game (generic flow)

Send each line via `mtc_send_and_wait` with `appendEnter: true`, waiting out
each prompt:

1. `mtc_connect_server` (host/port).
2. `Please enter your name` → send **empty line** (Enter for none) → TWGS menu.
3. `Selection (? for menu):` → game letter.
4. `What is your name?` → `<account>`.
5. `Use ANSI graphics?` → `N`.
6. `Enter your choice:` → `T` (Play Trade Wars 2002).
7. `Show today's log? (Y/N) [N]` → empty (default `No`).
8. Each `[Pause]` → a single space or Enter continues.
9. `Password?` → `<password>`.
10. More `[Pause]` screens (Who's Playing, messages, avoid-scan) → space/Enter
    each, until the `Command [TL=...]:[sector] (?=Help)? :` prompt appears.

## Using the bot

- Pages go out as subspace quotes: `'MomBot <command>` on channel 0. The bot
  announces `Sub-space radio (0): [<ENTER> for multiple lines]`, then replies
  inside a comm-link; `Sub-space comm-link terminated` marks the end of a
  complete reply. If `waitSeconds` is too short the reply truncates silently.
  **Keep the wait ≤ ~15s.** Requests >~20s can be aborted by the MCP gateway
  ("This operation was aborted") — and an aborted call still leaves the page
  in flight, so you learn nothing and may double-dispatch. Prefer short waits
  and re-check `mtc_get_recent_events` instead.
- Commands that scan (CIM) need longer waits (~25-45s).
- The bot announces a start line (`<mode> {MomBot} - <cmd> starting up!`).
- The bot listens at any prompt, including the TWGS menu, but ship actions
  require being in-game.
- `switch` is NOT mode switching — it swaps ships with a trader docked in your
  citadel. "Modes" are loaded by invoking the mode's command directly (e.g.
  `merch`), and `mtc_get_context` shows the loaded module.
- Driving cadence that works: agent moves the ship (`mtc_send_and_wait` for
  warps/docks/haggling), bot does tools (`cim`, `class0`, `getnear`, `find`,
  `status`).

## Ship upgrades at the StarDock

- Stardocks are Class 9 special ports; dock with `P`, then choose `S - Land on
  the StarDock`. Menu: C CinePlex / G Bank / H Hardware Emporium / L Library /
  P FedSpace Police / S Shipyards / T Tavern / Q leave.
- **Shipyards** (`S`): B buy new ship (priced list, ships must be named,
  optional password), S sell extra ships (only ships visible in orbit there),
  E examine specs, P upgrade current ship (A cargo holds / B fighters / C
  shield points — per-unit costs shown), R re-registration.
- **Hold upgrade: answer the shipyards `P` menu with `A`.** The mombot command
  `refurb holds` does the whole landing + shipyards + upgrade dance by itself
  (it also bought fighters and shields unprompted). 35 holds cost ~55k on the
  Merchant Freighter (~1,900-2,400 cr/hold at that point).
- `V` from the sector prompt shows the stardock sector + trade-in values.
- Ship names: the trader scan shows all your ships by name; the transporter
  (`X`) beams to an owned ship **by ship ID number** (40, not 1 or the name).
  Small ships have intrasector-only transport range.
- Do NOT leave ships in FedSpace overnight ("no littering!") — the Feds
  repossess unmanned ships. Old Scout Marauder was left parked there.
- Random StarDock events: wandering (`Y` at the stardock menu) can get you
  mugged (lose credits).

## Bot commands learned (additions)

- `trade {q} {mcic}` — best trade at the current port with auto-haggle +
  MCIC testing. Refuses with `Trade Must start at command prompt.` when the
  terminal has recent activity/history at it; page it only when the prompt is
  genuinely idle and untouched for a beat, else keep driving ports manually.

- `cim` — Computer Interrogation. Dumps the port report into the bot DB and
  echoes a summary via comm-link: `Upped Ports:` (>10000 level) and
  `Ports with MCIC at least -60/-65`. Output format: `sector ore% org% equ%`,
  leading `-` = port BUYS that product. `cim` re-runs port CIM only; a second
  arg meaning warp-CIM did not trigger with `cim 0 warps` (still port CIM).
- `refurb {holds|fighters|shields} {seek}` — warps/parks at stardock, walks
  the shipyards, installs upgrades. Needs no pre-setup. Long wait (~30s+).
- `class0` worked after adoption: reports stardock sectors it knows.

- `class0` — lists discovered stardocks (Rylos/Alpha Centauri) as
  `Rylos is Sector: N` (0 = unknown). Needs exploration data.
- `getnear {min}` — nearest "cashing ports" with product >= min. Aborts with
  `Unable To Determine Port Max From CFG File` unless `$game~port_max` is set.
- `setvar` — paged setvar only accepts fixed vars (s, r, a, b, x, tl, h); `~`
  is eaten in subspace, so game vars can't be set by page.
- `status` — full bot status card (credits, cargo, mode, Fed Safe, sector).

## Bot state lessons

- `$game~port_max` is learned from a text trigger on `Port Production Max=`;
  TWGS View Game Settings (`S` from the TWGS menu) does not show that line, so
  on such servers set the var with a helper script.
- Helper script trick: write a TWX script via `mtc_write_script`
  (`scripts/SetPortMax.ts`: `setvar $game~port_max N` + `savevar`), compile
  with `mtc_compile_script`, run at the game prompt with `mtc_run_script`.
  Vars persist across sessions.
- Stopped scripts linger in `mtc_list_scripts` (paused `switch.cts` etc.);
  clean up with `mtc_stop_script <id>` before paging more work.

## Pitfalls

- **Mombot prompt-state commands are broken on TWGS v2 (this server).** The
  bot's `quikstats` (`#145` + `/`) capture parses the status bar, but its
  prompt classification never engages here: `$player~current_prompt` stays
  `Undefined` ("Invalid starting prompt: [Undefined]"), the status card shows
  `Turns 0` / `Time Left: Bad Prompt` (cosmetic artifacts of the same parse
  gap), and every prompt-gated command refuses:
  - `trade` → "Trade Must start at command prompt."
  - `qreport` → "Cannon Calculator must be run from command prompt"
  - `merch` → "You must run Planet Merchant command from a Citadel prompt."
  - `refresh` → "Invalid starting prompt: [Undefined]."
  Workarounds: `reboot` re-activates the bot but does NOT rebuild prompt
  state; setting `$player~current_prompt` via a TWX helper gets overwritten by
  the next quikstats call. Agent-driven port trading works fine; the bot
  remains useful for data work (CIM, class0, getnear, status, refactor/refurb
  at stardock)
- **Never `mtc_stop_script` a paused bot script.** TWX triggers are global —
  stopping one script can kill the bot's live text triggers (this is likely
  how the prompt state died). Let paused scripts be, or use the bot's own
  `stopall`/`reboot`.
- **Y/N prompts auto-answer "No" if the answer isn't already in the input
  buffer.** Chain them into one line: `PS` (dock then land on stardock),
  `HY` (pick ship then confirm), `QY` (quit then confirm). Multi-char answers
  get consumed one char per prompt (`Wolverine` became W-o-l... I).

- Stray keystrokes appear at the game prompt during page waits (e.g. a stray
  `E` triggered "You do not have any Ether Probes."); verify unintended input
  in the event log before acting.
- A page can be double-sent if a previous page never got its terminating
  Enter — verifiable by duplicated `Sub-space radio` blocks.
- A second telnet session can appear mid-session ("Telnet connection
  detected" then back to the TWGS name prompt). Just log in again; bot state
  and saved vars survive.
- `X` at the sector prompt is the Transporter room (own-ship scan), NOT exit.
- `Q` = `<Quit>` → `Confirmed? (Y/N)` → `Y` exits to the TWGS menu.
- Non-adjacent sector number = plot + autopilot prompt (free to compute).

## Session log — game B "Ultimate Borg" @ roguetw.net:2002

- Account eleq, started 23663; game has unlimited turns/time (TL=00:00:00 is
  cosmetic). V shows: stardock = 10456.
- Trade pair in use: 12274 Korred Minor (BBS, sells equ) <-> 21108 Ceuta (BSB,
  buys equ ~2900, sells org). **Equipment is the money leg** (+~4x), organics
  adds ~1k/cycle as filler.
- Money numbers at 10 holds: buy ~379-404 total → sell ~1,375-1,485 total per
  10. At 85 holds: buy ~3,700 → sell ~11,3xx → **+8.5k per 2-turn cycle**
  (plus org ~+1.2k same-dock). All haggle dollar figures are TOTALS for the
  load, not per-unit.
- Bought Wolverine: **Merchant Freighter (ship 40)**, 69,280 cr, 50 initial
  holds, upgraded to **85 holds** via mombot `refurb holds` (55k). Bot also
  added 112 figs / 108 shields (scout carried them away).
- Old Scout Marauder (Pi Proxy, ship 39, 250 figs) left parked in FedSpace —
  repossession risk acknowledged.
- Progression: 237k start → ~238k after 10-hold cycles → 188k after ship
  purchase → 85-hold loop growing ~+9.5k/2 turns. Rank: Staff Sergeant
  (exp 59). Credits ~38k mid-session (funds parked in the loop).
- Next: keep looping until 21108's equ stock (2,631 @ 89%) drains below viable,
  then reroute to another equ-sink (4964 upped SSB next to the stardock buys
  32,760 equ — worth a price test vs the 1-hop pair including 15-hop transit).
- StarDock `T` trade report: Class 9 buys ALL products at 3000/100% — a
  universal (if low-price) sell-any-cargo sink right inside FedSpace.

