# Design — Transporter Loadouts

Status: **SLICE 1 BUILT & DEPLOYED 2026-07-02** (core remember-and-prefill loop; builds clean,
in game Mods folder, not yet in-game-tested). Sibling to **Crew Presets** — separate mods that
pair if both installed (their header buttons don't collide: Loadouts is Items-tab, Presets is
Pawns-tab).

## What slice 1 ships (in `Source/`)
- **`CompDefaultCargo`** (+ `CompProperties_DefaultCargo`, + `CargoEntry` IExposable): per-transporter
  manifest `List<CargoEntry{ThingDef, count}>`, scribed with the building (per-instance, save-specific).
- **Comp injection at startup** (`HarmonyBootstrap.InjectComps`): adds the comp to every `ThingDef`
  whose resolved comps include `CompTransporter`. Chosen over an XML `PatchOperationAdd` because
  PatchOperations run pre-inheritance — most vanilla pods/shuttles inherit `CompProperties_Transporter`
  from abstract bases, so an XPath on the literal comp list would miss them. Runtime injection sees the
  resolved list and catches vanilla + modded craft; runs before saves load so existing-save transporters
  get the comp on re-init.
- **Auto-fill on open** (`Patch_LoadTransporters_PostOpen`): after `PostOpen` builds the transferables,
  fill the primary transporter's saved manifest into the item rows (clamped to what's on the map). Local
  UI only — vanilla Accept is synced. Skipped when a load is already in progress / ready to launch.
- **"Default cargo" header button** (`LoadoutUI`, `Patch_LoadTransporters_Header`, Items tab only):
  green-tinted when the manifest is satisfied; menu = Fill / Save current items as default (confirm on
  overwrite) / Clear (confirm). Save captures ticked item rows (pawns excluded).
- **MP** (`CargoActions` + `MultiplayerCompat`): `SetDefaultCargo`/`ClearDefaultCargo` synced, addressed
  by the transporter `Thing`; manifest passed as parallel `List<ThingDef>`/`List<int>`. Filling on open
  stays local. Ships `0MultiplayerAPI.dll`.
- Def-vs-instance: the load dialog for a *group* of pods uses `transporters[0]`'s manifest (common case
  is one shuttle; group support is a possible later refinement).
- KNOWN MVP LIMITATION: matching is by exact `ThingDef` (ignores stuff/quality) — fine for
  meals/chemfuel/shells; a "steel longsword" default would match any-stuff longsword. Category/"any X"
  filters are a later refinement (see Open questions).

## SLICE 2 — "unloading keeps the default cargo" (Luke 2026-07-02, NEXT)
Goal (Luke): pressing **Unload** on a loaded shuttle should NOT dump the items that match its default
loadout — keep them aboard and ready — so the basics don't get reloaded every mission. Plus a
**right-click "Unload everything"** escape hatch for a full vanilla dump.

UX (INVERTED per Luke 2026-07-02 — vanilla left-click stays untouched; keep-defaults is a right-click
opt-in):
- **Left-click Unload** → vanilla, unload everything (UNCHANGED — we never touch the vanilla action).
- **Right-click Unload → "Unload all except default cargo"** → keep the default items in `innerContainer`,
  drop the rest, stay ready-to-launch.
- **No saved default** → left-click vanilla only; no right-click option shown.

Mechanism (decompile facts): the gizmo is a `Command_Action` in `CompTransporter.CompGetGizmosExtra`
(~line 368; label "CommandCancelLoad"/"CommandUnload", icon `UI/Designators/Cancel`), whose `.action`
calls `CompTransporter.CancelLoad()` → `CancelLoad(Map)` → per group member `CleanUpLoadingVars(map)`,
whose `innerContainer.TryDropAll(...)` dumps everything and `groupID = -1` resets to idle.

Chosen approach — **additive right-click only; do NOT replace the vanilla action, do NOT blanket-patch
`CleanUpLoadingVars`** (its post-launch/leave callers must keep dumping, or cargo strands in a departed pod):
- Postfix `CompGetGizmosExtra`: find the unload `Command_Action` (match `icon ==
  ContentFinder<Texture2D>.Get("UI/Designators/Cancel")`); when the comp has a manifest, **add a
  `rightClickFloatMenuOptions`** entry "Unload all except default cargo" → synced
  `CargoActions.UnloadKeepingDefaults(Thing)`. The vanilla `.action` (left-click) is left alone → it stays
  the untouched vanilla synced `CancelLoad` that drops everything.
- `UnloadKeepingDefaults` sets a per-thread "keep" flag, then calls `CancelLoad()`. A **Prefix on
  `CleanUpLoadingVars(Map)`** diverts ONLY when the keep flag is set AND that comp has a manifest: drop
  only the NON-manifest items (and overflow beyond the target counts), leave manifest items in
  `innerContainer`, keep `groupID >= 0` (ready-to-launch), clear `leftToLoad`. Every other caller (vanilla
  left-click, launch, leave) has the flag clear → runs vanilla. No `ForceUnload` method needed — left-click
  IS the full unload.
- Shortfall (Luke's "50 meals lost on a mission"): keep-defaults keeps up to the target count of what's
  actually in `innerContainer` — if only 20 of 50 meals came back, it keeps 20; never errors.
- MP: the right-click path is our own `MP.RegisterSyncMethod` (addressed by the transporter `Thing`) → one
  synced command; the divert reads the scribed manifest and drops the ordered `innerContainer` items —
  deterministic, no `Rand`/wall-clock. Left-click is the vanilla synced path, unchanged.
- OPEN EDGE CASES to verify in-game: **confirm the launch path does NOT route through `CancelLoad()`**
  (grep the other `CancelLoad`/`CleanUpLoadingVars` callers — esp. CompTransporter line ~473) — needed so
  the flag can never leak into launch/leave; groups of pods (each keeps its own defaults); shuttle
  ship-jobs (`Shuttle?.shipParent` `ShipJob_Unload` fired at `CancelLoad()` start — may still try to
  unload, may need skipping when keeping defaults); "Cancel load" (mid-load) vs "Unload" (fully loaded).

## Shortfall handling (Luke 2026-07-02 — "default 50 meals, lost on a mission")
Everything clamps to what's actually available; no errors, no blocking, at every touchpoint:
- **Auto-fill on open** (`LoadoutApplier.ApplyManifest`): each line `AdjustTo(target)` auto-clamps to the
  map stock (fills 20 if you have 20 of a 50 default; fills 0 / no row if you have none).
- **Manual Fill** button surfaces the gap: "N default item type(s) couldn't be fully loaded (not enough on
  the map)."
- **Keep-on-unload** keeps up to the target of what's physically in the pod.
- OPEN UX ITEM: `DefaultsSatisfied` currently green-tints when loaded to the clamped max (so 20-of-50
  reads green). Decide in-game whether a partial fill should instead show a "short" state (amber /
  "Default cargo (short)") so scarcity is visible rather than looking fully met.

## SLICE 3 — auto-load the default cargo on the home map (Luke 2026-07-02, NEXT-NEXT)
Goal (Luke): on the home map, colonists automatically load a transporter's default cargo without the
player opening the dialog and hitting Accept — "not me have to set it." Pairs with keep-on-unload: a
shuttle stays stocked to its default on its own.
Control (RESOLVED w/ Luke 2026-07-02 + screenshot): a **`Command_Toggle` gizmo "Auto-load default cargo"
in the shuttle's command bar** (same `CompGetGizmosExtra` postfix as slice 2 — the bar with Launch /
Cancel load / Set to load / Refuel from cargo). Per-transporter, scribed. Default state depends on map:
**ON when the transporter is on a colony/home map, OFF when not.**
- Store an explicit-override pair on `CompDefaultCargo`: `bool autoLoadSet` (scribed) + `bool autoLoad`
  (scribed). Effective value = `autoLoadSet ? autoLoad : (parent.Map?.IsPlayerHome == true)`. Clicking the
  toggle sets `autoLoadSet = true` and flips the stored value → synced `CargoActions.SetAutoLoad(Thing,
  bool)`. (So a freshly built colony shuttle auto-loads by default; one sitting on a non-home map doesn't,
  until the player opts in.)
- Only show the toggle when the comp `HasManifest` (nothing to auto-load otherwise).
Mechanism:
- Trigger in `CompDefaultCargo.CompTickRare` (every 250 ticks — a SIM tick, so it runs identically on all
  MP clients → deterministic, **no sync method needed**; strictly no `Rand`/wall-clock/unordered
  iteration): if [effective auto-load ON] AND `parent.Map.IsPlayerHome` AND `!LoadingInProgressOrReadyToLaunch`
  AND `HasManifest` AND ≥1 manifest item is available on the map → initiate a load of the default manifest.
- Initiate loading the same way the dialog's Accept does — VERIFY the exact vanilla chain first
  (`Dialog_LoadTransporters.TryAccept` → `AssignTransferablesToRandomTransporters` sets each comp's
  `leftToLoad` via `AddToTheToLoadList`, then `TransporterUtility.InitiateLoading(group)` spawns the
  `LordJob_LoadAndEnterTransporters` so haulers carry items in). Reuse those game methods rather than
  reimplementing the lord.
- Runs in sim → inherently MP-deterministic; do NOT also register a sync method (that would double-fire).
- Guards: skip quest/required-items shuttles (`Shuttle?.Autoload` or shuttle with required items) so we
  don't fight their own loading; home-map only; only when idle (not already loading/ready).
- Top-up falls out naturally: because it only fires when not already loading and below default, consumed
  stock gets re-hauled up to the default next check.
- Interaction w/ slice 2: a full left-click unload that leaves it empty will re-trigger auto-load (if the
  toggle is on) — intended (keeps it stocked); a player who wants it left empty turns the toggle off.

---

## Original concept & scope (retained)

## Concept (per-shuttle default cargo)
Each transporter/shuttle remembers **its own default cargo manifest**, which is pre-filled whenever you open
its load dialog — so a shuttle you've set up as a supply runner always offers "50 meals + 100 chemfuel," and
another set up as an artillery drop always offers "1 mortar + 50 incendiary shells," without re-ticking items
every launch. (Luke's vision 2026-07-01: **per-shuttle-instance**, NOT global named templates.)

## Scope: standalone mod, no dependencies
A generic **default-cargo comp** added to transporters (any `ThingDef` with `CompTransporter` — drop pods,
Royalty/Core shuttle, caravans-via-? , our shuttles, other mods' craft) via a `PatchOperationAdd`. No
Odyssey/Biotech dependency. (Could alternatively be folded into the shuttle mod if we ever want it scoped to
just our craft — but standalone + generic matches the "separate mod / Shuttle 1 vs Shuttle 2" intent.)

## Storage — per-instance, save-specific
- A comp on the transporter building (`CompDefaultCargo`) holding a manifest: a list of `{ThingDef, count}`
  (optionally a stuff/quality/category filter per entry, e.g. "any meal").
- Scribed with the building → **per-instance and save-specific** (each shuttle has its own; lives in the save).
  NOTE: this is the same save-scope as crew presets — the earlier "loadouts are global/cross-save" idea was
  from the old named-template design and does NOT apply to this per-shuttle vision.

## Mechanism
- **Set it**: a gizmo on the shuttle — "Set default cargo" → an editor (ThingDef picker + count rows) — and/or
  a "Save current items as this shuttle's default" button inside the load dialog's Items tab.
- **Apply it**: Harmony patch on `Dialog_LoadTransporters` open → for each manifest entry, set the matching
  item transferable's `CountToTransfer` toward the requested count, clamped by what's available on the map.
  The player still reviews/confirms the load; over the shuttle's cargo mass → the dialog's existing red bar.
  Shortfalls reported (e.g. "only 140/200 chemfuel on hand").
- Optional: a per-shuttle toggle "auto-fill default cargo on load" (on by default) so it can be switched off.

## Target dialog
- `RimWorld/Dialog_LoadTransporters.cs` (Items tab) — the load flow for the shuttle that owns the comp.
  (Caravans don't have a single owning "shuttle" the same way, so v1 is transporter-load-dialog only;
  caravan default-cargo could be a later extension if wanted.)

## Multiplayer
- The comp's manifest is per-building game state → editing it = a synced command (`MP.RegisterSyncMethod`;
  ship the `0MultiplayerAPI` stub like the shuttle mod). **Pre-filling** the dialog on open is local UI (the
  final Accept/launch is vanilla-synced).

## Build order
1. `CompProperties_DefaultCargo` + `CompDefaultCargo` (manifest `List<{ThingDef,count[,filter]}>`, scribed);
   `PatchOperationAdd` it onto transporter ThingDefs (or a runtime add).
2. Editor window (ThingDef picker + count rows) opened from a shuttle gizmo.
3. Harmony patch: on `Dialog_LoadTransporters` open, pre-tick the owning shuttle's manifest (shortfall-aware).
4. "Save current items as default" button in the dialog; per-shuttle auto-fill toggle.
5. MP sync pass on the manifest edits.

## Open questions
- Filter granularity per entry (exact def vs category vs "any X"); quality/stuff handling.
- Which transporter defs get the comp (all with `CompTransporter`, or a curated list?).
- Caravan support later? Mod name + packageId.
