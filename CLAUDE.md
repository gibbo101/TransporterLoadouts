# CLAUDE.md — Transporter Loadouts

A RimWorld 1.6 mod: every transporter/shuttle remembers **its own** default cargo manifest,
pre-filled when you open its load dialog. Per-instance, save-specific. This file is
mod-specific; the **workspace** `../CLAUDE.md` (global RimWorld rules) also applies — on
conflict, this file wins.

Full design + build status: `DESIGN.md` (in this folder). Sibling mod: **Crew Presets**
(`../CrewPresets/`) — separate mod that pairs with this one if both installed.

## Scope rules
- **Standalone, no DLC/mod dependencies** (Harmony only). Patches the *vanilla*
  `Dialog_LoadTransporters`, so it covers drop pods, shuttles (Royalty/Odyssey), and modded
  craft — anything whose `ThingDef` resolves a `CompTransporter`.
- **No XML Defs/Patches.** The comp is injected in code (see below) precisely because
  `PatchOperation`s run pre-inheritance and would miss transporters that inherit
  `CompProperties_Transporter` from an abstract base.

## Architecture (C#, `Source/`)
- `CompDefaultCargo` (+ `CompProperties_DefaultCargo`, + `CargoEntry` IExposable): the saved
  manifest `List<CargoEntry{ThingDef,count}>`, scribed with the building via `PostExposeData`
  (`Scribe_Collections … LookMode.Deep`, key `"defaultCargo"`).
- `HarmonyBootstrap.InjectComps()` ([StaticConstructorOnStartup]): adds a
  `CompProperties_DefaultCargo` to every `ThingDef` whose *resolved* comps include
  `CompTransporter`. Runs before saves load, so existing-save transporters get it on re-init.
- `DialogAccess`: `AccessTools.FieldRefAccess`-style reflection into `Dialog_LoadTransporters`
  privates — `transferables`, `transporters`, `tab` (enum `Pawns=0, Items=1`), and the private
  `CountToTransferChanged()` (call after mutating rows so mass/food caches refresh).
- `LoadoutApplier`: pure ops over the transferable rows — `CaptureCurrentItems` (ticked item
  rows, pawns excluded), `ApplyManifest` (AdjustTo each target, clamped; returns shortfall
  count), `DefaultsSatisfied` (green-tint test).
- `Patch_LoadTransporters_PostOpen`: auto-fill the primary transporter's manifest on open
  (local UI; skipped if `LoadingInProgressOrReadyToLaunch`).
- `LoadoutUI` + `Patch_LoadTransporters_Header`: the "Default cargo" button, **Items tab only**
  (keeps clear of Crew Presets' Pawns-tab "Presets..." button). Menu: Fill / Save current /
  Clear, with confirmations on overwrite + clear.
- **MP:** `CargoActions.SetDefaultCargo` / `ClearDefaultCargo` are synced (addressed by the
  transporter `Thing`; manifest as parallel `List<ThingDef>`/`List<int>`). Filling on open is
  local. UI messages inside synced methods gated by `MultiplayerCompat.ShowUiForThisClient`.
  Ships `0MultiplayerAPI.dll` (csproj `CopyToMod`).

## Conventions
- Harmony ID: `luke.transporterloadouts`. packageId: `luke.transporterloadouts`.
- Def prefix (if defs ever needed): `TransporterLoadouts_`.
- Prefer Postfix > Prefix. Draw button as a `DoWindowContents` postfix (its `inRect` param is
  the method's *mutated* local → `inRect.xMax` = width − 17; button at `xMax − 170, y = 6`).
- Save discipline: once published, don't rename the scribe key `defaultCargo` or `CargoEntry`
  field names (`def`, `count`) — breaks existing saves.

## Build / deploy / test
- `cd Source && dotnet build -c Release` (.NET 10 SDK → net48). `CopyToMod` drops both DLLs into
  `Assemblies/` (gitignored).
- Deploy: `robocopy <thisFolder> "D:\SteamLibrary\steamapps\common\RimWorld\Mods\TransporterLoadouts" /MIR /XD Source obj bin .git /R:20 /W:5`
  (DLL is user-locked while RimWorld runs — `/R` retries until you exit; verify with
  `Get-FileHash`). C# changes need a full game restart.
- Reference first: `../../RimWorld-Decompiled/Assembly-CSharp/` (read-only) and `../Docs/`.

## Build order (from DESIGN.md)
1. ✅ `CompDefaultCargo` + comp injection.
2. ✅ Auto-fill on open + "Default cargo" button (Fill / Save / Clear).
3. ✅ MP sync pass on the manifest edits.
4. ⬜ **Slice 2:** unloading keeps the default cargo aboard (patch the player `CancelLoad` path,
   not the launch path — see DESIGN.md for the mechanism + edge cases).
5. ⬜ In-game test; then GitHub repo + Workshop prep when Luke asks.

## Remaining / open questions
- Filter granularity per entry (exact def vs category/"any meal"); stuff/quality handling.
- Group-load (multiple pods, one dialog) currently uses `transporters[0]`'s manifest only.
- Optional per-shuttle "auto-fill on load" toggle (currently always on).
