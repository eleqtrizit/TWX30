# MOMBot — Mind Over Matter Bot (agent quick reference)

MOMBot is TWX Proxy's pageable in-game helper bot for Trade Wars 2002. It runs as compiled
TWX scripts inside the proxy and is controllable from the MTC client:

- **`mombot_status`** (MCP tool) — structured bot status: enabled, attached, watcher state,
  accepted routes, bot/team name, subspace channel, current sector, mode, last module,
  script root, authorized users, game connection.
- **`send_mombot_page`** (MCP tool) — page a command to the bot over the game stream
  (in-game subspace quote addressed to the bot name). Optional `waitSeconds` collects the
  bot's response lines. Requires a live game connection.
- **`run_mombot_command`** (MCP tool) — dispatch a command locally through the native MTC
  mombot dispatcher (no game stream round trip; requires self commands enabled).

## Addressing the bot

The bot answers three routes (each independently enabled, see `mombot_status`):

| Route | Greeted as | How the bot sees it |
|---|---|---|
| Self / local | `self` | A line starting with the bot's name, dispatched locally |
| Subspace | message on bot's subspace channel | Server packet `R <sender> ... <botname> <command>` |
| Private | private message to the bot player | Server packet `P <sender> ... <botname> <command>` |

Either side uses the form: `<botname> <command> [params...]` — e.g. `mombot cim 2 3`.
Remote (subspace/private) senders must be in the bot's authorized-user list unless that
list is empty (then everyone is trusted). `bot` and `relog` are blocked for remote senders.

Aliases live in `scripts/mombot/aliases.cfg` (`alias[,alias]=real command`); config in
`scripts/mombot/mombot.cfg`. Help text per command: `scripts/mombot/help/<command>.txt`.

## Modes

The bot has a mode stack per operational context. Mode scripts live in
`scripts/mombot/modes/<category>/`; entering a mode can load a module and some modules are
transient (they auto-restore the parent mode when done). Categories: cashing, data,
defense, general, grid, offense, resource. `switch` changes mode; $BOT~MODE tracks it.

## Command catalog

### Commands (scripts/mombot/commands/)

- **cashing** — `bust` (planet buster), `mega`, `neg`, `ppt`, `rob`, `trade`, `tricon`
- **data** — `armids`, `avoids`, `busts` (busted sectors report), `cim` (Computer
  Interrogation Mode port report: `cim [upgrade level] [warps]`), `class0`, `clearbusts`,
  `corpinfo`, `course`, `disp`, `dscan`, `fedbd`, `figs`, `find`, `findplanet`, `getnear`,
  `getvar`, `history`, `holo`, `limps` (refresh deployed limpet list, shows deltas),
  `msgs`, `msl`, `news`, `overload`, `param`, `ping`, `plist`, `probe`, `pscan`, `qreport`,
  `remaliens`, `sector`, `select`, `setparam`, `setvar`, `slist`, `status`, `storeship`,
  `time`, `update`
- **defense** — `call`, `evac`, `hazkill`
- **general** — `bwarp`, `callout`, `cn9`, `corp`, `dep`, `emq`, `fed`, `help`, `keep`,
  `land`, `lift`, `login`/`logoff`, `mac`, `nmac`, `page`, `pwarp`, `qset`, `reboot`,
  `refresh`, `relog`, `reset`, `run` (dispatch raw mombot command line), `scrub`,
  `sendfile`, `ss` (load script), `subspace`, `switch` (mode), `topoff`, `tow`, `twarp`,
  `unlock`, `wait`, `with`, `xport`
- **grid** — `clear`, `clearfig`, `deploy`, `haz`, `pgrid`, `port`, `safemow`, `surround`
- **offense** — `cap` (take captive), `hkill`, `htorp`, `invader`, `kill`, `mex`, `mxex`
- **resource** — `buy`, `hagexp`, `max`, `refurb`, `scruball`, `sellship`

### Modes (scripts/mombot/modes/ — command into operational context)

- **cashing** — `alienhunt`, `bbb`, `furb`, `gpm`, `marco`, `merch`, `psst`, `quikpanel`,
  `sdt`, `sst`, `tbust` (Traitor's Planet Buster), `wppt`, `wrob`, `wsst`
- **data** — `beam`, `fedmon`, `finder`, `list`, `proztm`, `ridealong`, `sentinel`, `ztm`
- **defense** — `citfill`, `ig`, `reloader`, `runaway`, `saveme`, `tsaveme`, `unstack`
- **general** — `xenter`
- **grid** — `disr`, `dora`, `gridcheck`, `limpshovel`, `minesweep`, `mow` (mine on warp),
  `mowfuel`, `passgrid`, `pgridder`, `plimper`, `ramgrid` (RammaR gridder), `tram`,
  `ugrid`, `wall`, `wander`
- **offense** — `boton`, `citcap`, `citkill`, `density`, `dockkill`, `drop`, `foton`, `pdrop`,
  `plock`
- **resource** — `colo`, `dump`, `ecolo`, `farm` (farm planets in sectors:
  `farm set/list/clear/balance/fill [planets|all] [options]`), `fillships`, `lsd`,
  `makeplanet`, `move` (product mover: `move [f|o|e|fig|cr|fc|oc|ec] [planet] [rounds]`),
  `movefig`, `moveship`, `patp`, `pimp`, `strip`, `stripships`, `upgrade`

### Daemons (scripts/mombot/daemons/)

- `at` — scheduled / at-style commands
- `fillsector` — fill a sector with colonists/product
- `nofed` — evade Feds / busted-sector handling daemon
- `teammega` — corp mega payout daemon

### Engine internals (for reference)

- `scripts/mombot/mombot.cts` — main loop: listens for pages/subspace, dispatches commands
- `scripts/mombot/startups/watcher.cts` — system watcher daemon that boots and babysits the bot
- `scripts/mombot/preload/` — startup helpers: `_kazi` (fighter attack), `_dock_shopper`,
  `_ldrop`, `_macro_kit`
- Native hotkeys/menus: `Source/MTC/mombot/mombotCatalog.cs`; settings persistence:
  `Source/MTC/mombot/mombotSettings.cs`

## Native MTC shortcuts

The MTC proxy also exposes native one-letter Mombot commands (no page needed):
`t <sector>` = twarp, `m <sector>` = mow. Full native command specs are in the
`mombotCatalog` (see `mombot_status` output for what is currently accepted).
