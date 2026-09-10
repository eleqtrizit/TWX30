# TWX Scripting Guide

A working guide to writing scripts for TWX Proxy 3.0. Everything here is derived
from the compiler/interpreter in `Source/Core` (`ScriptCmp.cs`, `ScriptCmd.cs`,
`ScriptCmdImpl.cs`, `Script.cs`) and the real scripts in `scripts/` (Pack1, Pack2,
Oz, Promethius, LoneStar, RammaT, mombot).

---

## 1. Source layout

| Path | Contents |
|---|---|
| `scripts/Pack1/` | Original script pack, one `.ts` per script (`1_Move.ts`, `1_PortFast.ts`, …) |
| `scripts/Pack2/` | Second pack, same style (`2_Ping.ts`, `2_Col.ts`, …) |
| `scripts/include/` | Shared subroutines included by pack scripts (`header.ts`, `move.ts`, `haggle.ts`, …) |
| `scripts/Test/` | Live-verification scripts used to validate this guide (`1_Stats.ts`, `2_PortProbe.ts`, `3_MoveTour.ts`, `4_Verify.ts`) |
| `scripts/Oz/`, `Promethius/`, `LoneStar/`, `RammaR/` | Community packs, same language |
| `scripts/mombot/` | MomBot daemon-based bot (uses `daemons/`, `modes/`, `preload/`, `commands/`) |
| `include/` | Repo-root test/demonstration scripts (`switchboard.ts`, `player.ts`) |

Workflow convention:

- Scripts are authored as **`.ts`** (plain text — not TypeScript despite the extension).
- `TWXC` compiles `.ts` → **`.cts`** (bytecode). Distribution packs ship `.cts`.
- `TWXD` decompiles `.cts` → `.ts` for study/editing.
- Include references use backslash and no extension: `include "include\header"`.

---

## 2. Toolchain

```bash
# compile (writes .cts next to source)
~/.dotnet/dotnet Source/TWXC/bin/Debug/net10.0/osx-arm64/twxc.dll scripts/Pack2/2_Ping.ts

# other utility modes (see TWXC usage output):
#   --compat                  legacy non-pruned bytecode for parity/debugging
#   --trim-includes           compile only reachable labels from includes
#   --strict-includes         fail when static labels referenced by includes are missing
#   --precompile script out   encrypted .inc include file
twxc --trim-includes path/to/script.ts

# decompile
~/.dotnet/dotnet Source/TWXD/bin/Debug/net10.0/osx-arm64/twxd.dll path/to/script.cts
```

Compound scripts (`.ts` that include others) are compiled as a whole; the
compiler also supports "dynamic namespaces" (see §7) so multiple scripts can
share library subroutines at runtime.

Scripts run inside the proxy client (MTC desktop client or TWXP). You launch
them from the client's script menu or a console hint; Pack scripts self-check
where they were invoked from (see §9).

---

## 3. Language basics

- **Case-insensitive** keywords, commands, and constants. Existing scripts mix
  `setVar` / `SETVAR` freely.
- **One statement per line.** Flow control is line-based with labels.
- **Comments** start with `#`.
- **Strings**: unquoted tokens are strings; quoted text may contain `*` to mean
  a newline inside `echo` output. Use `&` to concatenate expressions.

Variables and values:

```
setVar $target "Player"          # string / number in one variable type
setVar $count 5
setVar $warps SECTOR.WARPCOUNT[CURRENTSECTOR]
```

Arithmetic and logic are **commands, not infix operators**:

```
add $count 1
subtract $count 2
multiply $price 3
divide $total $qty
modulus $a 10
# infix comparisons are allowed inside if/while conditions:
if ($figs > 5) and ($safe = TRUE)
```

String helpers (all write into a target variable):

```
getWord CURRENTLINE $w 2         # 2nd word of the current line
getWordPos $name $pos $line      # 1-based position of $name in $line
getWordCount $line $n
cutText CURRENTLINE $loc 1 12    # substring by 1-based column range
stripText $planet1 "#"
replaceText $s "old" "new"
getLength $s $len
upperCase $s   /   lowerCase $s   /   trim $s   /   padLeft $s 10 " "
splitText $s "," $arr            # arrays (§5)
format / center / repeat / concat / find / sort
```

---

## 4. Control flow

```
if (condition)
  ...
elseif (condition)
  ...
else
  ...
end

while (condition)
  ...
end

goto :label
gosub :label          # pushes return address
  ...
  return
return                # at top level: ends the script run

halt                  # stop everything
stop                  # stop this script only
```

Labels start with `:` at column 1. Condition operators: `=`, `<>`, `>`, `<`,
`>=`, `<=` (lowercase equivalents `isgreater`, `isnotequal`, … also exist),
combined with `and`, `or`, `xor`, `not`-style composition via nested checks.

---

## 5. Variables, arrays, persistence

- `$name` — local variable, string/number, auto-typed.
- **Arrays**: `setVar $move~history[9] $move~history[8]`, built dynamically via
  `splitText` / `readToArray` / `setArray`.
- **Program variables**: `setProgVar <name> <value>` / read with the
  `PROGRAM.<name>` style access — shared across scripts (used by bots).
- **Persistent user settings** — the dominant pattern in Pack scripts:

```
loadVar $PingSaved                       # was this saved before?
if ($PingSaved)
  loadVar $Ping_Target                   # load each saved value
else
  setVar $Ping_Target "Player"           # first-run defaults
  ...
  saveVar $Ping_Target                   # save each one
  setVar $PingSaved 1
  saveVar $PingSaved
end
```

`saveVar`/`loadVar` persist per-game so the script remembers its configuration
between sessions. Globals (`saveGlobal`/`loadGlobal`/`clearGlobals`,
`listGlobals`) share state across scripts and are used by bots.

---

## 6. Talking to the game: send / wait / triggers

This is the heart of the language. The script stalls on `pause` until a trigger
fires, then execution jumps to the trigger's label.

```
send "d"                                  # type text into the game (no Enter needed on most)
send $attack & "y1**"                     # build from variables

waitOn "Command [TL="                     # macro: set trigger + pause + auto-kill
pause                                     # wait for any pending trigger to fire

TWGS input realities (verified live):

- `*` inside a `send` string means **Enter (CR)** — one `*` per Enter. So
  `send "10014*"` submits the sector like typing it and pressing Enter, and
  `send "y1**"` sends two Enters after `y1`. The same `*` rule as in `echo`
  (display newline) applies to both.
- TWGS **menu prompts** (`Enter your choice [T] ?`, the Command prompt, port
  menus) act on a single keystroke immediately — no Enter needed there.
- TWGS **text/numeric prompts** (`How many holds of Equipment do you want to
  buy [5]?`, `Your offer [461] ?`) need the `*`-Enter before anything happens.
  If your script just `send`s `p`/`t` and then pauses on the commerce text,
  the port dialogue sits waiting for input after hand-off.
- `waitOn`/triggers only fire on **new** text arriving after they are set —
  text already on screen (including a prompt printed before the script
  started) does not satisfy them. If the screen is already at the prompt you
  would wait on, generate fresh output first (a bare Enter re-displays) or
  check instead of wait.
- Typing a sector number that is **not adjacent** does not error out: TWGS
  computes a shortest path and prompts `Engage the Autopilot? (Y/N/Single
  step/Express) [Y]`. Single-step asks `Stop in this sector?` at every hop;
  `e` then `y` (Express) runs the rest of the route without stops. Only send
  warp numbers taken from `SECTOR.WARPS[...]`, or guard the autopilot prompt.
```

Trigger commands — each maps output text (or events) to a label:

| Command | Fires on |
|---|---|
| `setTextTrigger <name> :label "text"` | text seen anywhere in the stream |
| `setTextLineTrigger <n> :label "text"` | the *n-th* upcoming line containing text |
| `setTextOutTrigger` | text the *client* sends upstream |
| `setDelayTrigger <n> :label` | after *n* seconds |
| `setEventTrigger <code> :label "text"` | connection events (disconnect = 0/`disconnect`) |
| `killTrigger <name>` / `killAllTriggers` | remove triggers |

Idiomatic loop:

```
:Attack
send "y1**"
setTextTrigger pingFast :Attack $Ping_Trigger   # re-arm, then wait
pause
```

Guards every script should set:

```
setEventTrigger disconnect :disconnected "Connection lost"
...
:disconnected
  killAllTriggers
  setEventTrigger disconnect :disconnected "Connection lost"
  waitOn "Command [TL="          # wait for reconnect / prompt
  goto :Menu_Go                  # resume
```

Reading what arrived: inside a trigger label, `CURRENTLINE` holds the line that
matched (see helpers in §3). `getText` / `getOutText` / `getConsoleInput`
capture other sources (`getConsoleInput $Product SINGLEKEY` reads one keypress
without Enter). `getClientInput` behaves like `getInput` in proxy mode.

---

## 7. Subroutine and "namespace" conventions

Shared code lives in `scripts/include/*.ts` and is included at the **bottom** of
the script:

```
# includes:
include "include\header"
include "include\getTarget"
include "include\getAttack"
```

Because includes are merged into one program, every symbol is namespaced by
hand to avoid collisions. The repo convention:

- Library label: `:Namespace~Name` (e.g. `:GetTarget~GetTarget`, `:move~getsector`).
- Library state: `setVar $Namespace~field ...` (e.g. `$GetTarget~Index`,
  `$TestSector~Match`).
- The caller sets input variables before `gosub` and reads result variables
  after:

```
setVar $GetTarget~Target $Ping_Target
gosub :GetTarget~GetTarget
if ($GetTarget~Index = "")
  clientMessage "Target not found"
  halt
end
```

The compiler's dynamic-namespace scanning (regex `:name~`) makes these labels
safe across independently compiled scripts sharing a library.

Include resolution (from `ScriptCmp`): the file is searched relative to the
including script's directory first, then in `scripts/include/`, then successively
up the directory tree, with each of `""`, `.ts`, `.cts`, `.inc` tried per level.
Note: some Pack2 scripts (`2_Ping.ts`, the SSF/SDF family) include
`include\getTarget` and `include\getAttack`, which are **not present in this
repo** — those scripts won't compile until those files are restored or the
includes are removed.

---

## 8. Menus (configuration UI)

Pack scripts build an interactive settings menu rather than prompting serially:

```
addMenu "" "Ping" "Ping Settings" "." "" "Main" FALSE    # menu itself
addMenu "Ping" "Target" "Target name" "T" :Menu_Target "" FALSE
addMenu "Ping" "GO" "GO!" "G" :Menu_Go "" TRUE           # TRUE = exits the menu
setMenuHelp "Target" "This option lets you set the name..."
gosub :sub_SetMenu            # fill current values
openMenu "Ping"               # display

:Menu_Target
getInput $Ping_Target "Enter the name of the player to ping"
saveVar $Ping_Target
gosub :sub_SetMenu
openMenu "Ping"

:sub_SetMenu
  setMenuValue "Target" $Ping_Target
  setMenuValue "Safe" "YES"
  return
```

`getMenuValue`, `setMenuOptions`, `setMenuKey`, `closeMenu` round it out.
Bottom-line: **defaults → loadVar → addMenu entries → a gosub that syncs
menu values from variables → openMenu → one handler label per entry**.

---

## 9. Standard script skeleton

The pattern every first-party script follows (see `2_Ping.ts`, `1_Move.ts`):

1. **Copyright + description header comment block** (author, description,
   trigger point, warnings).
2. **Location guard** — verify the caller is on the expected game screen:

```
cutText CURRENTLINE $location 1 12
if ($location <> "Command [TL=")
  clientMessage "This script must be run from the command menu"
  halt
end
```

Other guards compare against `"Planet command"`, `"Planet prompt"`, etc.

3. `reqRecording` / `logging off` — set proxy recording mode.
4. Banner (`echo "**" ANSI_15 "..." "**"` with `*` line breaks).
5. Load or initialize saved settings (§5).
6. Config menu (§8) or prompts.
7. Main logic with triggers, disconnect guard, and safe-halt checks.
8. Includes at the bottom.

---

## 10. System constants (proxy game database)

Constants read live from the proxy's game database. Many are **indexed**:

```
SECTOR.WARPCOUNT[<sector>]          # number of warps out
SECTOR.WARPS[<sector>][<i>]         # i-th warp out (1-based)
SECTOR.PLANETS[<sector>][<i>]       # planet name
SECTOR.TRADERS / SECTOR.SHIPS[...]
PORT.BUYFUEL[<sector>]  PORT.SELLORG[<sector>]  PORT.CLASS[<sector>]
```

Categories worth knowing:

- **Sector**: `SECTOR.explored`, `adjacent`, `density`, `navhaz`, `beacon`,
  `anomaly`, `figs.quantity`, `figs.owner`, `figs.type`, `mines.*`, `limpets.*`,
  `planetcount`, `shipcount`, `tradercount`, `updated`, `deadend`, `constellation`.
- **Port**: `exists`, `class`, `name`, `fuel/organics/equipment` (quantities),
  `buy/sell` flags per good, `percent*`, `buildtime`, `updated`.
- **Ship & player (verified live: all BARE constants — no `SHIP.`/`PLAYER.`
  prefix, no sector index)**: `CREDITS`, `FIGHTERS`, `SHIELDS`, `TOTALHOLDS`,
  `OREHOLDS`, `ORGHOLDS`, `EQUHOLDS`, `COLHOLDS`, `EMPTYHOLDS`, `PHOTONS`,
  `ARMIDS`, `LIMPETS`, `GENTORPS`, `TWARPTYPE`, `CLOAKS`, `SCANTYPE`,
  `ALIGNMENT`, `EXPERIENCE`, `CORP`, `SHIPNUMBER`, `SHIPCLASS`, `TURNS`,
  `UNLIMITEDGAME`. `SHIP.CREDITS[CURRENTSECTOR]` resolves to the literal
  string and fails numerically at runtime ("'SHIP.CREDITS' is not a number");
  there is no `SHIP.` prefix in the constant table at all.
- **Session/meta**: `game`, `gamename`, `loginname`, `password`, `connected`,
  `currentline`, `currentansiline`, `rawpacket`, `sectors`, `time`, `date`,
  `version`, `stardock`, `alphacentauri`, `rylos`, `true`, `false`, `ansi_0..15`
  (color codes).

Full list with handlers: `AddSysConstant` calls in `Source/Core/ScriptCmd.cs`.

---

## 11. Database / advanced commands

The command table (~170 commands) includes groups the pack scripts rarely use:

- **Databases**: `createDatabase`, `openDatabase`, `editDatabase`, `copyDatabase`,
  `listDatabases`, `resetDatabase` — the proxy's own key/value record stores.
- **Navigation**: `getCourse <dist> <var>`, `getCourses`, `getNearestWarps`,
  `getAllCourses`, `setAvoid` / `listAvoids` / `clearAllAvoids` (plot around
  hostile sectors), `getDistance`.
- **Windows/UI extras**: `window`, `killWindow`, `setWindowContents`,
  `addQuickText`, `sound`.
- **Bot/instance management**: `openInstance`/`closeInstance` (multi-connection),
  `switchBot`, `nativeBot`, `setAutoTrigger`, `listActiveScripts`, `stopAll`.
- **Timers**: `startTimer`/`stopTimer`/`getTimer`.
- **Haggle**: `autoHaggle`, plus the native haggle engine
  (see `docs/haggle-modes.md`).
- **System**: `diagMode`/`diagLog`, `sys_check`/`sys_fail`/`sys_kill` (assertions),
  `reqVersion`.

Full dispatch table: `AddCommand(...)` calls at the top of
`Source/Core/ScriptCmd.cs` (each entry: name, min/max params, handler,
parameter kinds).

---

## 12. Reuse first — check before you write

The `scripts/` tree already contains decades of working TWX code. A new script
should almost never implement movement, trading, mapping, combat, or gridding
from scratch.

1. **Start with `SCRIPT_INVENTORY.md`** — every bundled script has a one-line
   description plus its trigger point. Find the capability you need before
   writing anything.
2. **Steal houses, not wheels:**
   - Movement/warping: `scripts/include/move.ts`, `scripts/include/Warp.ts`
   - Port trading/haggling: `scripts/include/haggle.ts`,
     `scripts/mombot/include/haggle.ts` (and the native engine, §14)
   - Sell/steal/transport (evil money-making): `scripts/include/SSM.ts`,
     `SST.ts`, `SellSteal.ts`
   - Colonise/upgrade planets: `scripts/include/Colonise.ts`, `MassColonise.ts`,
     `MassUpgrade.ts`, `PlanetUpgrade.ts`
   - Product logistics: `scripts/include/FindProduct.ts`, `Gather.ts`,
     `MoveProduct.ts`
   - ZTM/mapping: `1_ZTMFast.ts`, `pro_ZTM4.ts`, `ls_ZTM_11.cts`
   - Bot primitives (deploy, twarp, CIM, tab scans, refurb): mombot commands in
     `scripts/mombot/commands/` and `scripts/mombot/include/` modules
3. **Prefer composition over copying.** Wrap an existing script or `gosub`
   into an include routine with the `:ns~label` convention (§7). Do not paste
   subroutine bodies into a new script — copies drift and clobber shared
   variables.
4. **Only re-implement when the existing one truly doesn't fit** (wrong
   transport mode, hostile variable savestate, dead code). When you do, say so
   in the header comments and link the script you replaced.
5. **Binary-only scripts are still readable.** Existing community packs ship
   only `.cts`; decompile with TWXD (§2) to study or lift logic, as was done to
   build `SCRIPT_INVENTORY.md`.
6. **Mind the compatibility surface** when chaining: saved vars (`loadVar`/
   `saveVar`) persist across scripts and games; `GAMENAME`-suffixed data files
   are shared; and each script assumes a specific starting prompt (see the
   inventory's trigger points).

## 13. Writing a new script — checklist

1. Create `scripts/<Pack>/<n>_<Name>.ts` (or add to an existing pack).
2. Copy the header block: copyright comment, then `Author / Description /
   Trigger Point / Warnings`.
3. Add the location guard matching where the script is meant to be launched.
4. `setVar $Header~Script "<Name>"` if you plan to use `include\header` for the
   banner (Pack2 style), or write your own `echo` banner.
5. Implement settings with `loadVar`/`saveVar` and a menu if there are more than
   ~2 options.
6. Use `waitOn` for simple prompt sequences; named triggers + `pause` for event
   loops; always install the disconnect guard before going interactive.
7. Put reusable logic in `scripts/include/` with the `:ns~label` +
   `$ns~field` convention; include it at the bottom of the file.
8. Compile-check: `twxc scripts/<Pack>/<file>.ts` — fix all compiler errors.
9. Test in the MTC client (use the `mtc_*` tools or the GUI) against a live or
   recorded game before distributing the `.cts`.

### Pitfalls

- **Trigger labels are one-shot for `setTextLineTrigger` (n-th line) but
  re-arming patterns differ**: pack scripts re-set triggers inside the loop
  before each `pause`. Forgetting to re-arm is the classic infinite-hang.
- `waitOn` kills its own trigger; raw `setTextTrigger` + `pause` does not.
- `getWord`/`cutText` are 1-based; off-by-one from `getWordPos` results is
  common.
- Variables are global across the merged program — always namespace names in
  include files (`$Move~`, `$TestSector~`), or two scripts will clobber each
  other.
- `*` inside `echo` strings is a newline in the *client* display; it is not
  stripped from anything sent to the game via `send`.
- `if` conditions support infix comparisons, but assignment outside `setVar`
  does not exist — `$x = $y` alone is not a statement.
- Currentline content depends on how the trigger matched; prefer
  `setTextLineTrigger` with an explicit line count when position matters.

---

## 14. Testing scripts against a live game (verified workflow)

1. Author `.ts`, then compile with TWXC (`twxc path.ts`). The compiler catches
   unknown commands, bad parameter counts, and undefined labels — constant
   names (`CREDITS` vs `SHIP.CREDITS`) are only validated at runtime.
2. Run from the MTC client. Script `echo` output goes to the **script
   console**, which external automation cannot see. Two workarounds:
   - Runtime errors ARE surfaced as game events:
     `[Script error] <file> line <N>: Error executing command '<CMD>': ...` —
     so a failing numeric constant shows up immediately with its line number.
   - To "print" from a script under automation, `send` a built-up string at a
     TWGS prompt; the game echoes it back on the prompt line where normal
     event capture sees it. (Messy — it types into whatever prompt is live —
     but it proves values flow.)
3. `mtc_list_scripts` shows paused vs finished: a script that ends while the
   game is parked mid-dialogue (e.g. a port "How many holds" prompt) leaves
   the trader stranded; recovery needs manual prompts (`*`-Enter to accept the
   default, then navigate out).
4. `SECTOR.WARPINCOUNT` / `SECTOR.WARPSIN[$sector][$i]` exist alongside
   `SECTOR.WARPCOUNT` / `SECTOR.WARPS[$sector][$i]`; both are 1-based on the
   array index and default to `"0"` when out of range.
5. A minimal smoke script that compiles and runs cleanly from the Command
   prompt:

```
SETVAR $count SECTOR.WARPCOUNT[CURRENTSECTOR]
IF ($count = 0)
  HALT
END
SETVAR $dest SECTOR.WARPS[CURRENTSECTOR][1]
SEND $dest & "*"
WAITON "Command [TL="
HALT
```

## 15. Where to look in the source

| Area | File |
|---|---|
| Command list & handlers | `Source/Core/ScriptCmd.cs`, `ScriptCmdImpl*.cs` |
| Compiler / macros (`if`, `while`, `waitOn`, `include`) | `Source/Core/ScriptCmp.cs` |
| Interpreter core (`pause`, trigger firing, input wait) | `Source/Core/Script.cs` |
| Menu engine | `Source/Core/Menu.cs` |
| ANSI color codes | `Source/Core/Ansi.cs` |
| Haggle engine | `Source/Core/NativeHaggleEngine.cs`, `docs/haggle-modes.md` |
| Decompiler | `Source/TWXD/ScriptDecompiler.cs` |
| Best small example scripts | `scripts/Pack1/1_KeepAlive.ts`, `scripts/Pack1/1_Move.ts`, `scripts/Pack2/2_Ping.ts` |
| Bot architecture example | `scripts/mombot/` (daemons, modes, preload) |
