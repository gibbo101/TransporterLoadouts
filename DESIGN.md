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
loadout — keep them aboard and ready — so the basics don't get reloaded every mission.
Mechanism researched in the decompile:
- The shuttle gizmo shows "Unload" (fully loaded) or "Cancel load" (mid-load); both call
  `CompTransporter.CancelLoad()` → `CancelLoad(Map)` → per group member `CleanUpLoadingVars(map)`, whose
  `innerContainer.TryDropAll(...)` dumps everything and `groupID = -1` resets to idle.
- **Do NOT blanket-patch `CleanUpLoadingVars`** — the post-launch/leave path also calls it; keeping items
  there would strand cargo in a departed pod. Instead gate on the player-cancel path: a Prefix/Postfix
  around `CancelLoad(Map)` sets a flag; the `CleanUpLoadingVars` prefix only diverges when that flag is
  set AND the comp has a manifest, dropping only the NON-manifest items (and overflow beyond the target
  counts) while leaving manifest items in `innerContainer` and keeping `groupID >= 0` (stays
  ready-to-launch). Clear `leftToLoad`.
- OPEN EDGE CASES to verify in-game: groups of pods (each keeps its own defaults), shuttle ship-jobs
  (`Shuttle?.shipParent` `ShipJob_Unload` at CancelLoad start — may still try to unload; may need to skip
  it when defaults are kept), and the "Cancel load" (mid-load) case vs the "Unload" (loaded) case.
- MP: `CancelLoad` runs in the (synced) gizmo action path; the divert is deterministic state mutation.

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
