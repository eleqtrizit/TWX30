# Script Inventory

Inventory of every script bundled with TWX Proxy 3.0. File types: `.ts` = TWX script source, `.cts` = compiled (compiled with TWXC; decompilable with TWXD), `.txt` = documentation/menu data.

## scripts/0_Login.cts

- `0_Login.cts` — Automated login system script (compiled). Handles server login and contains a time-based feature check before continuing.

## scripts/Pack1/ — TWX Script Pack 1 (by Xide, TWX Proxy 3 distribution)

- `1_ECol.ts` — Express colonisation script; express warps between Terra and the target planet, grabbing colonists each pass (target planet surface; express route must be free of nav hazards).
- `1_KeepAlive.ts` — Keepalive script; sends `#` every 60 seconds to prevent disconnects (run anywhere with access to global functions).
- `1_Login.ts` — Login script for standard TWGS systems (runs on server connection).
- `1_Move.ts` — Moves products from one planet to another; supports multi-hop runs (source planet surface).
- `1_MoveCol.ts` — Moves colonists from one planet to another; supports multi-hop runs (source planet surface).
- `1_MoveFig.ts` — Moves fighters from one planet to another; supports multi-hop runs (source planet surface).
- `1_Port.ts` — Port-pair trading script; trades between two ports with a density scan, buying/selling until depletion (sector prompt).
- `1_PortFast.ts` — Faster but less efficient variant of `1_Port` (sector prompt).
- `1_Scout.ts` — Scout script; navigates using a density scanner, holo-scanning unusual-density sectors and docking at ports. No defences — don't run in low-turn games.
- `1_SSM.ts` — Sell-Steal-Move money script for evil traders, working a pair of xxB ports with a second ship (first steal at the second port).
- `1_SST.ts` — Sell-Steal-Transport money script for evils; more advanced variant of SSM using a second ship at a second port.
- `1_TCol.ts` — Trans-colonisation script; twarps to Terra and back to ferry colonists. No blind-warp protection.
- `1_Trade.ts` — Basic trade script; density-scanner navigation, holo scans on unusual density, docks at ports while trading. Useless in low-turn games.
- `1_ZTM.ts` — Zero Turn Mapping script (original); plots courses from a CIM dump. Horribly slow — clear avoids before starting.
- `1_ZTMFast.ts` — Fast rewrite of the ZTM script; much quicker than `1_ZTM`.

## scripts/Pack2/ — TWX Script Pack 2 (TWX Proxy 3 distribution; heavy use of the `scripts/include/` library)

- `2_Build.ts` — Builds planets: automates mass colonising of chosen planet types in a sector and optionally upgrades the port (command menu).
- `2_BuyDown.ts` — Buys down a planet's product/regeneration (planet surface).
- `2_Col.ts` — Colonises a planet to maximum productive capacity (planet surface).
- `2_Evil.ts` — Evil-trading driver: chains the SSM/SST helpers for selling/stealing runs (command menu).
- `2_Find.ts` — Finds a product type in the current sector via density/port scans (command menu).
- `2_Gather.ts` — Gathers a specific product type into a planet (planet surface).
- `2_MassColonise.ts` — Colonises every planet in the current sector (command menu).
- `2_MassUpgrade.ts` — Upgrades every planet in the current sector (command menu).
- `2_Ping.ts` — Repeatedly attacks a target with fighters until its fighters are gone (pinging) (command menu).
- `2_Probe.ts` — Ether-probe navigation/scanning script (command menu).
- `2_Query.ts` — Searches recorded game data for matching sectors and writes results to a file; reports total sectors found (command menu).
- `2_SDF.ts` — Ship-to-planet fighter deployment between ships/planets (command menu).
- `2_SDT.ts` — Sell-Defense-Torp trading driver for coordinated second-ship trading (command menu).
- `2_Sentinel.ts` — Planet/port sentinel; monitors surroundings and reacts, with an inactivity mode when disconnected (planet).
- `2_SSF.ts` — Ship-to-ship fighter transfer script (command menu).
- `2_SST.ts` — Sell-Steal-Transport driver for second-ship evil trading (command menu).
- `2_WorldSSM.ts` — World-wide SSM: sells/stoles across the whole universe, not just adjacent sectors (command menu).
- `2_WorldTrade.ts` — World-wide trading: trades between ports across the universe (command menu).

## scripts/include/ — shared helper library for Pack2

- `Colonise.ts` — Colonises a target planet (target planet prompt; holds empty, avoids clear).
- `FindProduct.ts` — Locates a product type within the current sector (sector prompt).
- `Gather.ts` — Finds and gathers a specific product type (planet surface).
- `MassColonise.ts` — Colonises every planet in the sector (sector prompt).
- `MassUpgrade.ts` — Upgrades every planet in a sector (sector prompt).
- `MoveProduct.ts` — Moves a product between two ports/planets, possibly far apart.
- `NearFig.ts` — Fighter/near-fig helper used by the pack's movement scripts.
- `PlanetCheck.ts` — Pre-colonise planet checks (sector prompt).
- `PlanetInfo.ts` — Records planet data before display for Pack2 reporting.
- `PlanetLoop.ts` — Iterates planets in a sector for the mass scripts (sector prompt).
- `PlanetUpgrade.ts` — Upgrades a planet using whatever resources are available (during planet landing).
- `Refurb.ts` — Refurbishes the ship (sector prompt, deep space).
- `SellSteal.ts` — Sell/steal trade routines for evil scripts (sector prompt).
- `SSM.ts` — Sell-Steal-Move routine (sector prompt).
- `SST.ts` — Sell-Steal-Transport routine (sector prompt).
- `Warp.ts` — Warp/movement helper for the pack (sector prompt).
- `WorldSSM.ts` — Universe-wide SSM routine (sector prompt).
- `haggle.ts` — Port haggle routines (best-price negotiation).
- `header.ts` — Pack2 header/banner menu (`Header~Pack2Header`) shared by all Pack2 scripts.
- `move.ts` — General movement helpers (warp/move transport).
- `playerinfo.ts` — Player/trader info capture helpers.
- `portcheck.ts` — Port status/pair checks.
- `portcheck`/`ppt.ts` — Port-pair trading routine.
- `worldtrade.ts` — Universe-wide trading routines.
- `gameprefs.ts` — Game preference persistence helpers.

## include/ — shared helpers at repo root

- `include/player.ts` — Player/prompt tracking library: captures the current prompt, key triggers, and player state used by MTC-era scripts.
- `include/switchboard.ts` (and compiled `switchboard.cts`) — Message switchboard: routes bot messages (e.g. Discord-format lines) through mode handlers.

## scripts/LoneStar/ — LoneStar script pack (~66 scripts; menu loader + categorized tools)

Official descriptions from `ls_0__Index.txt` / menu file `ls_0__Scripts.txt`; the rest characterized from decompiled sources. The loader reads `ls_0__Scripts.txt`; its two `.txt` files are docs/menu data, not scripts.

### Combat / Defense

- `ls_CIMHunter_v20.cts` — Scans Computer Interrogation Mode for blocked ports, reports avoid list.
- `ls_SafeMow.cts` — Gets a pod to safety, mapping the safest course that avoids enemy fighters.
- `ls_Surroundv2.cts` — Surround PDrop: places your planet in the path of a fighter surround (no fig data).
- `ls_LSurroundv2.cts` — LoneStar's own surround-PDrop variant; same concept, reworked engine.
- `ls_YourItv360.cts` — Regridder/killer script; aggressive target-clearing, brutal on servers.
- `ls_PGRiDDER06.cts` — LoneStar's take on MD's Planet Gridder.
- `ls_SWAT_v10.cts` — SWAT: continuous planet/fig assault with holo-scans, ore threshold and auto-return.
- `ls_Boton_v13.cts` — Bwarp-photon planet attacker; scans adjacent fig data, fires photons, auto-return.
- `ls_LOZIP304.cts` — OZ Improved Photon v3.03: fighter-hunt photon attack with fig files and orphan ports.
- `ls_ozip212.cts` — OZ Improved Photon v2.2: earlier photon fig-hunting engine, same concept (`ls_ozip212.txt` is its doc).
- `ls_DTorp_v1.cts` — Density Torp'R: density-scans adjacent sectors and fires photons on detected figs.
- `ls_Invader.cts` — Reports/attacks planet fighter defenses (figs per attack).
- `ls_BeRightBack_v1.cts` — BRB v1.0: planet-based attack run — launches from citadel, strikes target, returns.
- `ls_BRBv1.cts` — Same BRB concept, earlier/simpler build of the planet-based attack run.
- `ls_Limp_pDrop_3.cts` — Death Tracker 2000: scans/tracks active limpets and hunts the owner's ships.

### Grid

- `ls_Grid_Maker_v104.cts` — Builds target lists for selective gridding.
- `ls_LSPassiveGridder.010.cts` — Passive Gridder: explores, gathers MCIC data, avoids enemy figs.
- `ls_PassiveGridder.cts` — Passive Gridder v2.0 (same engine, mines/limps drop options); near-duplicate of `.010`.
- `ls_PassiveGridder.010.cts` — Another duplicate of the Passive Gridder v2.0 variant.
- `ls_MapArt.cts` — Maps your existing fighter grid only; good for early-game.
- `ls_Tunnel_Finder_v10.cts` — Lists sectors in a tunnel with a basic traffic report.
- `ls_finderv1.cts` — LoneStar's Tunnel Finder v1.0: ZTM-check + tunnel listing (earlier version).
- `ls_tfinderv11.cts` — Tunnel Finder v1.1: tunnel listing plus warpcount-based sector sweep.
- `ls_UnExScout_v3.cts` — Unexplored-sector scout: holoscans only when needed, tracks grid, avoids figs.
- `ls_Scout_v20.cts` — UnExplored Scout v2: queue-based unexplored-sector explorer with fig avoidance.
- `ls_WarpFinderv13.cts` — Finds the closest warp information; best in verbose mode.
- `ls_WARPSpec.cts` — Exports warp spec data to TWX or SWATH format (optional port data).
- `ls_planet.cts` — Small command-prompt helper: warp/planet display query used for planet info.
- `ls_LSZTM_11.cts` — LSZTM v1.1: full 7-pass Zero-Turn Mapping, writes 7-warp/dead-end/traffic files.
- `ls_ZTM_11.cts` — ZTM v1.1: identical 7-pass ZTM mapper (same file outputs); near-duplicate of LSZTM_11.
- `ls_Traffic.cts` — Traffic Analyzer: filters/reports sectors by range, frequency, warps, dead ends from ZTM traffic file.

### Maintenance

- `ls_Combined_Mover_v11.cts` — Smart mover of products/colonists/fighters to or from a planet.
- `ls_deFILER_v1.cts` — Cleans all game-specific files for a game (38 file types), keeps databases.
- `ls_SECTv162.cts` — Express Col & Tow v1.62: fast colonist/tow runs, faster than TWARP within 4 hops of Terra.
- `ls_LSECTv162.cts` — Same Express Col & Tow v1.62 core (master-file copy); near-duplicate of SECTv162.
- `ls_ORE_v162.cts` — Fast, smart ORE whore; stops when the planet is full.
- `ls_planet_creation_v10.cts` — Planet Creation: guided planet build (inspired by Alexio's script).
- `ls_PlanetCreator.cts` — "POP Goes The World" v1.0: planet construction helper with photon/traffic checks.
- `ls_Planet_Stripper_v103.cts` — Super-fast sector planet stripper; stops when a product category fills.
- `ls_Speed_Mow.cts` — Speed mow courier: runs product/col loads while flagging mines, hazards and losses.
- `ls_Move_Helper.cts` — Move helper with course-lock: navigates using the LSZTM traffic file.
- `ls_merc.cts` — Automated corp-bot helper: sub-space bot commands for CIM and product/planet ops.

### Cashing

- `ls_Bust_Manager.cts` — Imports/exports busts between TWX bust files and SectorParams.
- `ls_FURBY.cts` — Self furb; finds near-Terra figs, buys holds, returns to start.
- `ls_Ports.cts` — Port Emporium: selects port class and runs port upgrades.
- `ls_MCIC.001.cts` — Filters EP/Cherokee/M()M MCIC CSV equipment data by value.
- `ls_Dock_Shopper_v33.cts` — StarDock shopper: hardware/ship shopping utility.
- `ls_CLV.cts` — Saves traders list in memory, compares after a manual CLV, reports changes.
- `ls_CLV_Sort.cts` — System script: parses/sorts player rankings for CLV display.
- `ls__ShipManifest.cts` — Ship Manifest v1.0: reads/writes corp ship catalog to `LS_<game>_Mainfest.txt`.
- `ls_ShipManifest.cts` — Identical Ship Manifest; duplicate of `ls__ShipManifest.cts`.

### Misc

- `ls_Alien411.cts` — Scans/looks up active alien races; pick which race to scan.
- `ls_Dock_Me.cts` — Gets a blue/red onto dock and back with minimal input.
- `ls_New_Game_v192.cts` — New-game setup wizard for entering a fresh game.
- `ls_Who.002.cts` — Who's-Playing monitor in its own window, refreshes every 10s (adjustable).
- `ls_Load_v30.cts` — LoneStar's Script Loader v3.00: category menu launcher reading `ls_0__Scripts.txt`.
- `ls_sload10.cts` — Script Loader v1.00: earlier simple menu launcher writing default LS_Scripts.txt (`ls_sload10.readme.txt` is its doc).
- `ls_txt_spitter.cts` — Text-File Spitter: dumps a text file (ASCII art/macros) to the terminal, with redirect options.
- `ls_Terra ALARM.cts` — Sits in sector 1 scanning Terra, sets an alarm at a colonist-level threshold.
- `ls_TRv103.cts` — Gridder Target Lister v1.03: exports filterable gridding/sweep target lists to a file.

## scripts/Promethius/

- `pro_AdjacentSpeedBuy.ts` — Fast fighter/ore buying at adjacent ports.
- `pro_AssetCheck.ts` — Grabs corp assets for SS output; v2.15 compares them against game stats.
- `pro_BaseMove.cts` — Base repositioning/moving script.
- `pro_Bubble.cts` — Finds all bubbles in the universe you have ZTMs for; configurable size, sector output to file.
- `pro_EprobeRemote.cts` — Remote ether-probe helper.
- `pro_GameInfo_4.cts` — Dumps game information/parameters.
- `pro_GameOpen.cts` — Automated setup checks/announcements for game open.
- `pro_Monitor.cts` — Watches game events (observations) while docked/active.
- `pro_Ping.cts` / `pro_Ping.ts` — In-game ping testing (50- or 5-ping modes), file output formatted for EIS forum colors.
- `pro_Relog.cts` — Relogs to last prompt (Stardock, surface, Citadel, or Terra) after disconnect.
- `pro_SSWarpSpec.ts` — Warp-sector analysis for Sunshine University specs.
- `pro_ScriptPack.txt` — Official descriptions of the Promethius pack scripts.
- `pro_ShipAnalysis.cts` — Analyzes the current ship's specs/loadout.
- `pro_ZTM4.ts` — Zero Turn Mapping script (ZTM creation).
- `pro_ZTMSpeed20.cts` / `pro_ZTM_Speed.ts` — Speed-optimized ZTM plotting variants.
- `pro_cannondamage.cts` — Cannon damage calculation/testing.
- `pro_ecolonizer5.cts` — Ether-warp colonizer variant (v5).
- `pro_fueler.cts` — Fuels a single planet in a sector (warns of server lag).
- `pro_gameassets.cts` — Corp/game asset snapshot and comparison.
- `pro_gd.cts` — Grids to Stardock and lands on the dock.
- `pro_gt.cts` — Grids to Terra and ports.
- `pro_masscitbuilderv09.ts` — Mass citadel construction/upgrades across many planets.
- `pro_md.cts` — Mows to Stardock and lands on the dock.
- `pro_mt.cts` — Mows to Terra and ports.
- `pro_planet_display.ts` — Displays planet data/reporting.

## scripts/RammaR/

- `ram_P-Grid.cts` — Complete planet gridding suite (solo, with driver, with safe ship; v2.21, ZTM required).
- `ram_P-Grid_Readme.txt` — P-Grid instructions.
- `ram_Path-Blast.cts` — Express-warp style charging/gridding for early game or charging a specific target (v5.11).
- `ram_Path-Blast_Readme.txt` — Path-Blast instructions.
- `ram_Prober.cts` — Flexible ether-probing script; announces finds over subspace and logs everything (v1.2).
- `ram_Prober_Readme.txt` — Prober instructions.
- `ram_Pwarp_Density_Scout.cts` — Warps a planet along an optimized path checking for unusual density readings (v1.1).
- `ram_Pwarp_Density_Scout_Readme.txt` — Pwarp Density Scout instructions.
- `ram_Team_SDT.cts` — Coordinated multi-player SDT trading; rotates 1–4 "Red" traders through 2–3 sectors (v1.71).
- `ram_Twarp_Grid.cts` — TransWarp-drive gridding of a supplied sector list with fig tracking and near-fig jumps (v1.95).
- `ram_Twarp_Grid_Readme.txt` — Twarp Gridder instructions.
- `ram_Unfigged_Gridder.cts` — "Nearest Unfigged Gridder" v1.11: grids sectors that lack your fighters, with density reporting.
- `ram_pgrid_driver.cts` — Planet driver companion for P-Grid: moves the planet on call to a chosen door (v2.2).

## scripts/Oz/

- `oz-explore-v1.ts` — OZ-Hunter 2.0 explorer variant; charges to clear NNF dead ends (exported to gamename.dend).
- `oz-grid-test_for_beta2.ts` — OZ-Hunter gridding test variant (for TWX beta2) with twarp/area/density-level menu.
- `oz-passive-v1.ts` — OZ-Hunter 2.0 passive-mode hunter variant using half-fig deployments on file-based areas.
- `oz_bot_109b.ts` — Oz-Bot 2.0 (v1.09b): all-in-one bot requiring Cherokee's buydown/planet-nego helper scripts.
- `oz_cit-killa.ts` — Citadel killer: aggressive citadel takeover script run from the citadel prompt.
- `oz_flex-foton.ts` — OZ-PWarp Fast Flex Foton 2.0: photon anticipating pesky chargers, flex fig-file mode.
- `oz_foton.ts` — OZ-PWarp Fast Foton 2.0: photon script for intercepting enemy chargers.
- `oz_improved_foton.ts` — OZ-PWarp Fast Foton 3.2 (by Rincrast): improved photon, callable from Rinbot.
- `oz_planet-grid.ts` — OZ Planet Grid: planet gridding with configurable density limit, run from citadel.
- `oz_planet-trade_best.ts` — OZ Planet Trader (Rin version): full-featured planet product/colony trading menu.
- `oz_planet-trade_speed.ts` — OZ Planet Trader speed variant (nearly identical to "best" copy).
- `oz_pre-foton.ts` — OZ Anticipation Foton 2.0: pre-emptive photon setup ahead of incoming chargers.
- `oz_twarp_saveme_botver.ts` — TWarp "Saveme" bot version: listens for =saveme distress calls and rescues via twarp.

## scripts/mombot/ — MOMBot 3 suite (pageable in-game bot: main engine + command scripts + modes + library)

### Main
- `mombot.cts` — Main bot: listens for pages/subspace, dispatches commands. Ships with `aliases.cfg`, `mombot.cfg`, `help/` text files, `MOMBot_Manual.html` and `Mombot_Scripting.html`.

### commands/cashing
- `bust.cts` — Makes and busts planets until target experience reached.
- `mega.cts` — Attempts a mega rob on the port.
- `neg.cts` — Negotiates a planet trade agreement.
- `ppt.cts` — Scans/offers planet-to-planet trades with adjacent sectors.
- `rob.cts` — Attempts to rob the port.
- `trade.cts` — Day-1 trader; best trade plus port MCIC testing.
- `tricon.cts` — Plays Tricon (must start at stardock).

### commands/data
- `armids.cts` — Refreshes deployed armid list, shows differences.
- `avoids.cts` — Set, clear, save, or display avoid sectors.
- `busts.cts` — Displays all busted sectors on subspace.
- `cim.cts` — Computer Interrogation Mode: port report.
- `class0.cts` — Reports known Class 0 sectors.
- `clearbusts.cts` — Clears all busts in the database.
- `corpinfo.cts` — Reports all corporate assets on subspace.
- `course.cts` — Shows course path between sectors.
- `disp.cts` — Deposit help plus interactive sector scan/display.
- `dscan.cts` — Sends density scan output to subspace.
- `fedbd.cts` — Finds backdoors adjacent to fedspace and reports them.
- `figs.cts` — Refreshes deployed fighter list, shows differences.
- `find.cts` — Searches TWX database for fighter/port data.
- `findplanet.cts` — Locates corporate/personal planets and reports.
- `getnear.cts` — Lists nearest cashing ports with equipment volume.
- `getvar.cts` — Displays bot variables (stardock, rylos, backdoor...).
- `history.cts` — Shows recently issued bot commands.
- `holo.cts` — Sends holoscan output to subspace.
- `limps.cts` — Refreshes deployed limpet list, shows differences.
- `msgs.cts` — Reads or deletes in-game messages.
- `msl.cts` — Checks for/lists all MSL-marked sectors.
- `news.cts` — Reads and reports daily game news by category.
- `overload.cts` — Warns of sectors overloaded with planets.
- `param.cts` — Displays saved sector parameters (FIGSEC, MINESEC...).
- `ping.cts` — Measures and reports round-trip ping times.
- `plist.cts` — Displays sector planet scan on subspace.
- `probe.cts` — Ether prober; creates planets of chosen types.
- `pscan.cts` — Sends planet data over subspace.
- `qreport.cts` — Reports first five quasar shots for planets.
- `remaliens.cts` — Remembers/reports alien sectors.
- `sector.cts` — Displays sector data and saved parameters.
- `select.cts` — Database queries by planet/trader/ship/port/sector.
- `setparam.cts` — Sets sector parameters.
- `setvar.cts` — Sets bot variables.
- `slist.cts` — Displays ship list on subspace.
- `status.cts` — Reports bot status/stats on subspace.
- `storeship.cts` — Records information about the current ship.
- `time.cts` — Displays local system time.
- `update.cts` — Refreshes deployment lists, sector params, CIM/warps.

### commands/defense
- `call.cts` — Triggers a SaveMe rescue script.
- `evac.cts` — Moves all movable planets in sector to a target sector.
- `hazkill.cts` — Removes NavHaz by launching Genesis torpedoes.

### commands/general
- `bwarp.cts` — Bwarps (planet-teleport) to a sector or trader.
- `callout.cts` — Reports team name and current sector.
- `cn9.cts` — Resets in-game cn preferences to bot-friendly settings.
- `corp.cts` — Joins or drops a corporation.
- `dep.cts` — Deposits cash into citadel treasury.
- `emq.cts` — Emergency macro to escape menus, reset command state.
- `fed.cts` — Sends subspace messages.
- `help.cts` — Displays command help files.
- `keep.cts` — Tops up/draws down citadel treasury to target credits.
- `land.cts` — Lands on a planet.
- `lift.cts` — Lifts from planet/citadel prompts to command.
- `login.cts` — Sets overnight login message.
- `logoff.cts` — Logs off, optional timed relog and cloak.
- `mac.cts` — Sends a macro.
- `nmac.cts` — Sends a macro multiple times.
- `page.cts` — Pages the bot owner.
- `pwarp.cts` — Planet-warps to a sector or trader.
- `qset.cts` — Sets planet quasar cannon damage target.
- `reboot.cts` — Kills and restarts the bot.
- `refresh.cts` — Refreshes cached player/ship/planet state from prompt.
- `relog.cts` — Attempts to log the bot back into the game.
- `reset.cts` — Disconnects from the game server (EMX alias).
- `run.cts` — Dispatches a raw mombot command line.
- `scrub.cts` — Removes limpets from own hull; optional port seek.
- `sendfile.cts` — Outputs file contents to screen/subspace/fedcom.
- `ss.cts` — Sends subspace messages.
- `subspace.cts` — Changes subspace channel.
- `switch.cts` — Switches ships with a trader in citadel.
- `topoff.cts` — Fills ship with fighters from the sector.
- `tow.cts` — Tows ships; displays tow list.
- `twarp.cts` — Transwarps to a sector or trader.
- `unlock.cts` — Unlocks citadel ship so it can be traded.
- `wait.cts` — Waits specified milliseconds (multi-command helper).
- `with.cts` — Withdraws cash from citadel treasury.
- `xport.cts` — Exports to another ship (list/password variants).

### commands/grid
- `clear.cts` — Clears all enemy armids and limpets from sector.
- `clearfig.cts` — Clears adjacent fighters and calls saveme.
- `deploy.cts` — Unified deploy/put/lay/place of fighters and mines.
- `haz.cts` — Creates NavHaz by making and destroying planets.
- `pgrid.cts` — Planet-grids into a sector.
- `port.cts` — Builds, destroys, or upgrades ports.
- `safemow.cts` — Moves to a sector safely; optional porting.
- `surround.cts` — Surrounds sector with fighters, armids, or limpets.

### commands/offense
- `cap.cts` — Captures enemy ships, avoiding destruction.
- `hkill.cts` — Holoscans, attacks adjacent traders, then retreats.
- `htorp.cts` — Holoscans, then photons enemy in adjacent sector.
- `invader.cts` — Photon/enter/export/land/kill command combinations.
- `kill.cts` — Kills any enemy players.
- `mex.cts` — Move/attack/export using a safe ship.
- `mxex.cts` — Move/xport/attack using a moth ship.

### commands/resource
- `buy.cts` — Buys product from ports or figs/shields from Rylos/Alpha.
- `hagexp.cts` — Mayhem experience haggler.
- `max.cts` — Upgrades a port product as far as possible (no-exp option).
- `refurb.cts` — Auto-buys holds/fighters/shields; optional class 9 seek.
- `scruball.cts` — Scrubs limpets from empty docked ships, reports results.
- `sellship.cts` — Sells all the ships at dock it can.

### daemons
- `at.cts` — Runs a bot command daily at a set time.
- `fillsector.cts` — Buys fighters and adds them to the current sector.
- `nofed.cts` — Converts fed-restricted hotkey output to subspace.
- `teammega.cts` — Multi-bot coordinated buydown and mega rob.

### modes (scripted modes)

#### cashing
- `alienhunt.cts` — Hunts and captures alien ships; auto-turns personal.
- `bbb.cts` — Buys min ore/org/equip, jets to planet for SSS experience.
- `furb.cts` — FURB: buys/delivers a ship to a corpie to attack (incl. CK mode).
- `gpm.cts` — GoPop Moo v5: Mayhem corp grid-pop engine.
- `marco.cts` — Marco Polo: PPT trade routes (trade or report).
- `merch.cts` — Visits grid ports selling organics/equipment.
- `psst.cts` — Planet SST with two ships/two planets; steal, xport, furb.
- `quikpanel.cts` — Interactive cashing control panel/automation helper.
- `salesman.cts` — Trades all grid ports with haggling/upgrades options.
- `sdt.cts` — Two-ship steal cycle using last-rob sector tracking.
- `sst.cts` — Steal-ship-steal loop with EP haggle support.
- `tbust.cts` — Traitor's Planet Buster, adapted for MOMBot.
- `wppt.cts` — World PPT via the legacy worldtrade engine.
- `wrob.cts` — Travels the universe robbing ports.
- `wsst.cts` — World sell-steal-transport with furb/fighters options.

#### data
- `beam.cts` — Beams file/parameter data to a corp mate's bot.
- `fedmon.cts` — Monitors/relays public FedSpace movement messages.
- `finder.cts` — Reports nearest friendly fig after fighter hits.
- `list.cts` — Searches database sectors by port/MCIC/figs/warp count.
- `proztm.cts` — ProZTM by Promethius: zero-turn mapping monitor.
- `ridealong.cts` — Gathers port/sector data while riding along; on/off.
- `sentinel.cts` — Periodic CIM/CLV change watcher (Xide's Sentinel).
- `ztm.cts` — Zero Turn Mapping: ZTM plotting with resume support.

#### defense
- `citfill.cts` — Refills citadel fighters (auto option).
- `ig.cts` — Keeps an Interdictor Generator turned on after damage.
- `reloader.cts` — Sits above planet; lands/reloads fighters when hit.
- `runaway.cts` — Pwarps to random safe sectors after fighter hits.
- `saveme.cts` — Planet rescue shuttle; on/off with target controls.
- `tsaveme.cts` — Responds to saveme calls by twarping/bwarping to help.
- `unstack.cts` — Moves overloaded planets to FARM/BUBBLE sectors.

#### general
- `xenter.cts` — Exit/enter cycles to clear mines or fighters.

#### grid
- `disr.cts` — Disrupts mines in adjacent sectors.
- `dora.cts` — Dora the Explorer: explores universe (no ZTM), optional trades.
- `gridcheck.cts` — Visits and scans unknown sectors in your fig grid.
- `limpshovel.cts` — Dumps limpets to grid borders or near base.
- `minesweep.cts` — Sweeps grid mines via deploy/clear cycles.
- `mow.cts` — Fighter-mow to destination with kill/cap/port options.
- `mowfuel.cts` — Mows to unfigged upgraded fuel ports in grid.
- `passgrid.cts` — Passive limpet gridding with trade/holo options.
- `pgridder.cts` — Auto-pgrids until stopped; requires corpie running saveme.
- `plimper.cts` — Keeps personal limpets stocked in current sector.
- `ramgrid.cts` — RammaR's legendary gridder converted to mombot.
- `tram.cts` — Chain-selection gridder (LoneStar algorithm).
- `ugrid.cts` — Ultimate configurable gridder with refurb/avoid options.
- `wall.cts` — Plots courses to find all sectors N from an origin.

#### offense
- `boton.cts` — Bwarp-photon on fig hits; photons, returns, lands.
- `citcap.cts` — Captures enemy ships from planet citadel.
- `citkill.cts` — Citadel killer; destroys enemy ships from citadel.
- `density.cts` — Density-watch that reacts (kill/escape/photon/pel) on changes.
- `dockkill.cts` — Scans for targets and autokills in sector (pods/meat modes).
- `drop.cts` — Ship dropper with delay/trigger/kill/return options.
- `foton.cts` — Photon mode on fighter hits with tow/return options.
- `pdrop.cts` — Planet dropper with many drop/trigger options.
- `plock.cts` — Pre-locks with planet onto a sector; optional kill.

#### resource
- `colo.cts` — Fetches colonists from Terra in selectable cycles.
- `dump.cts` — Quickly dumps/jettisons planet resources.
- `ecolo.cts` — E-warp colonizing for red or non-twarp ships.
- `farm.cts` — Farms products/colonists from listed sector sets.
- `fillships.cts` — Fills all empty ships with fighters from sector.
- `lsd.cts` — LoneStar Dock Shopper encoded-order runner.
- `makeplanet.cts` — Makes planets of chosen types/with custom names.
- `move.cts` — Moves products/colonists between planets in rounds.
- `movefig.cts` — Moves fighters between planets and sector.
- `moveship.cts` — Moves empty ships between sectors.
- `patp.cts` — Pay At The Pump: refuels ports with fuel mechanics.
- `pimp.cts` — Makes planets and strips them of product.
- `strip.cts` — Strips planets' resources onto the starting planet.
- `stripships.cts` — Strips fighters from empty ships into the sector.
- `upgrade.cts` — Upgrades planets using sector products and colonists.

### include/*.ts (library modules)
- `bot.ts` — Bot core: trigger teardown, state, settings management.
- `combat.ts` — Combat subroutines (attack/capture/citadel attack strings).
- `connectivity.ts` — Keepalive echo and relog recovery.
- `fighters.ts` — Fighter deployment helpers (personal/toll/offensive).
- `findproduct.ts` — Finds planets holding a chosen product to source.
- `game.ts` — Game stats/state tracking and reporting.
- `gameprefs.ts` — Configures game cn preferences (ANSI, subtitles...).
- `grid.ts` — Gridding/surround subroutines.
- `haggle.ts` — Port trading/haggle engine.
- `help.ts` — Help file display system.
- `internal_commands.ts` — Internal bot commands and handlers (login-memo etc.).
- `invader.ts` — Implements the pe/ped/pel/px... invader command set.
- `loadvars.ts` — Loads bot variables/settings at run start.
- `lsd.ts` — LSD dock-shopper engine routines.
- `map.ts` — Stardock/backdoor discovery and map helpers.
- `menus.ts` — Interactive preference/main menus.
- `merchant.ts` — Merchant selling-route engine.
- `mines.ts` — Mine deployment helpers.
- `move.ts` — Movement and deploy-macro subroutines.
- `planet.ts` — Planet scanning, counting, and info routines.
- `planethaggle.ts` — Planet trade negotiation engine.
- `planetnames.ts` — Random planet-name array (1000 names).
- `player.ts` — Player stats/quikstats and prompt handling.
- `port.ts` — Port info collection routines.
- `search.ts` — find/near database search implementations.
- `sector.ts` — Current/adjacent sector and trader data gathering.
- `ship.ts` — Ship capability/stat subroutines.
- `switchboard.ts` — Subspace message routing and dispatch.
- `update.ts` — Fighter/deployment refresh subroutines.
- `user_interface.ts` — Command routing and team-name addressing.
- `xenter.ts` — Exit/enter cycle engine.

### preload
- `_dock_shopper.cts` — LSD dock-shopper menu state for ship outfitting.
- `_kazi.cts` — Kazi: automated planet invasion (shields/defender/zdy).
- `_ldrop.cts` — Ldrop: direct fighter drops launched from citadel.
- `_macro_kit.cts` — Macro kit: planet menu and drop amount automation.

### startups
- `watcher.cts` — System watcher daemon: event monitors, sector params, boots mombot.cts.

Generated by decompiling every `.cts` with TWXD and consulting the packs' own documentation (`ls_0__Index.txt`, `pro_ScriptPack.txt`, `ram_*_Readme.txt`, `mombot/help/*.txt`, source comments). Near-duplicate variants are noted as such where two copies of the same engine ship together.
