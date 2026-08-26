# Development notes (archived Claude session log)

Design decisions and session-by-session progress, archived 2026-07-02 from Claude Code
session memory into the repo. Append-style log — newer entries may supersede older ones.
**=== TRANSPORTER LOADOUTS — SLICE 1 BUILT+DEPLOYED+COMMITTED 2026-07-02 (dd1e32c, `master`, local-only) ===**
Folder `TransporterLoadouts/` (packageId+Harmony ID `luke.transporterloadouts`, shuttle-mod csproj recipe,
ships 0MultiplayerAPI stub). Deployed to `D:\...\Mods\TransporterLoadouts` via robocopy /MIR /XD Source obj bin
.git. **NOT yet in-game-tested.** Source/ files: `CompDefaultCargo.cs` (CompProperties_DefaultCargo +
CompDefaultCargo manifest List<CargoEntry{ThingDef,count}> scribed Deep key "defaultCargo" + CargoEntry
IExposable); `HarmonyPatches.cs` (bootstrap PatchAll + **InjectComps**: adds the comp at
[StaticConstructorOnStartup] to every ThingDef whose RESOLVED comps include CompTransporter — chosen over XML
PatchOperationAdd because PatchOps run PRE-inheritance and vanilla pods/shuttles inherit CompProperties_Transporter
from abstract bases; + PostOpen postfix auto-fills primary transporter's manifest into item rows, skipped if
LoadingInProgressOrReadyToLaunch; + DoWindowContents postfix draws button); `DialogAccess.cs` (reflect
Dialog_LoadTransporters privates: transferables/transporters/tab[Pawns=0,Items=1]/CountToTransferChanged);
`LoadoutApplier.cs` (CaptureCurrentItems[items only, pawns excluded, count>0] / ApplyManifest[AdjustTo clamped,
returns shortfall count] / DefaultsSatisfied[green-tint]); `LoadoutUI.cs` ("Default cargo" button **ITEMS TAB
ONLY** = clears Crew Presets' Pawns-tab button so both coexist; menu Fill/Save-current/Clear w/ confirm-on-
overwrite+clear; green when satisfied); `CargoActions.cs` (synced SetDefaultCargo(Thing,List<ThingDef>,List<int>)
/ ClearDefaultCargo(Thing)); `MultiplayerCompat.cs` (registers those 2 + ShowUiForThisClient gate). Group-load
uses transporters[0]'s manifest only. MVP limit: exact-ThingDef match (ignores stuff/quality).
**LUKE'S SLICE-2 ASK (his words 2026-07-02, NOT built yet): pressing UNLOAD on a loaded shuttle should NOT dump
items matching its default loadout — keep them aboard/ready so basics aren't reloaded every mission; PLUS a
right-click "Unload everything" escape hatch (full vanilla dump).** UX **INVERTED** (Luke 2026-07-02 2nd pass): LEFT-click Unload = VANILLA
dump-all (UNTOUCHED); RIGHT-click Unload = "Unload all except default cargo" (keep defaults); no saved default →
vanilla only, no right-click option. Locked mechanism (in DESIGN.md): gizmo = Command_Action in
CompTransporter.CompGetGizmosExtra (~L368, icon UI/Designators/Cancel) → .action calls CancelLoad() →
CancelLoad(Map) → per-group CleanUpLoadingVars() → innerContainer.TryDropAll dumps all + groupID=-1.
**ADDITIVE ONLY — do NOT replace the vanilla .action, do NOT blanket-patch CleanUpLoadingVars (launch/leave share
it → would strand cargo in departed pod):** postfix CompGetGizmosExtra, match the unload Command_Action by
icon==ContentFinder Get("UI/Designators/Cancel"), ADD a rightClickFloatMenuOptions entry "Unload all except
default cargo"→synced CargoActions.UnloadKeepingDefaults(Thing). Keep-flag-gated Prefix on CleanUpLoadingVars
diverts ONLY when keep flag set + comp has manifest → drop only NON-manifest items, keep manifest items +
groupID>=0 ready, clear leftToLoad; every other caller (vanilla left-click/launch/leave) has flag clear → vanilla.
No ForceUnload method (left-click IS full unload). MP: right-click = OUR MP.RegisterSyncMethod (by Thing); left =
vanilla synced path. MUST-VERIFY when building: launch path does NOT route through CancelLoad() (check
CompTransporter ~L473 + other CleanUpLoadingVars callers); pod groups; shuttle shipParent ShipJob_Unload;
Cancel-load(mid) vs Unload(loaded). **SHORTFALL (Luke "50 meals lost on a mission"): everything clamps to
available — auto-fill AdjustTo clamps to map stock (fills 20 of a 50 default; 0/no-row if none), manual Fill
reports "N couldn't be fully loaded", keep-on-unload keeps up to target of what's in the pod; NO errors. OPEN UX:
DefaultsSatisfied green-tints at clamped-max (20/50 reads green) — decide in-game if partial should show a
"short"/amber state.**
**SLICE 3 (Luke 2026-07-02, designed not built): AUTO-LOAD default cargo — colonists auto-fill the manifest
without the player opening the dialog.** Control = a `Command_Toggle` gizmo "Auto-load default cargo" in the
shuttle command bar (Luke's screenshot: same bar as Launch/Cancel-load/Set-to-load; same CompGetGizmosExtra
postfix as slice 2), shown only when HasManifest. Per-transporter, scribed. **DEFAULT = ON when transporter on a
colony/home map, OFF when not** — impl via (autoLoadSet bool + autoLoad bool) on CompDefaultCargo: effective =
autoLoadSet ? autoLoad : (parent.Map?.IsPlayerHome==true); toggle → synced CargoActions.SetAutoLoad(Thing,bool).
Trigger = CompDefaultCargo.CompTickRare (250-tick SIM tick → MP-deterministic, NO sync method, no Rand): if
effective-on + IsPlayerHome + !LoadingInProgressOrReadyToLaunch + HasManifest + ≥1 manifest item on map →
initiate load. Reuse vanilla load chain (VERIFY: Dialog_LoadTransporters.TryAccept →
AssignTransferablesToRandomTransporters sets leftToLoad via AddToTheToLoadList → TransporterUtility.InitiateLoading
spawns LordJob_LoadAndEnterTransporters) — don't reimplement the lord. Guards: skip quest/Autoload shuttles,
home-map only, idle only; top-up falls out naturally. **WHY (Luke's use-case): off-colony the toggle is off so
meals aboard a cargo ship stay usable by colonists on an excursion rather than locked in re-loading; home it
tops back up = "self-restocking mobile larder."** Slices 2+3 share the CompGetGizmosExtra postfix → build together.
**SLICES 2+3 BUILT+DEPLOYED+COMMITTED 2026-07-02 (commit 97c5c9c; slice 1 was dd1e32c).** New Source files:
TransporterLoadoutsMod.cs (Mod+ModSettings: autoLoadColonyMaps=true/autoLoadOtherMaps=false, Mod-options page),
UnloadPatches.cs (KeepDefaults flag + FieldRef leftToLoad/massUsageDirty + CleanUpLoadingVars prefix divert +
CompGetGizmosExtra postfix: replaces unload gizmo w/ Command_UnloadTransporter[left=synced UnloadAll,
right=synced UnloadKeepingDefaults] + appends Command_Toggle "Auto-load default cargo"). CompDefaultCargo gained
autoLoadSet/autoLoad (scribed) + AutoLoadActive + MapDefault + PostSpawnSetup reset + CompTick(IsHashIntervalTick
250)→TryAutoLoad (AllSendableItems→AddToTheToLoadList→InitiateLoading). CargoActions+3 synced (UnloadAll,
UnloadKeepingDefaults, SetAutoLoad). **MP-SAFETY FIX: mod settings are per-machine → sim TryAutoLoad must NOT
branch on them; MapDefault uses MultiplayerCompat.InMultiplayer to fall back to fixed on-colony/off-else in MP.**
DECOMPILE FACTS CONFIRMED: launch path = CompLaunchable:359 calls CleanUpLoadingVars DIRECTLY (not CancelLoad) →
keep-flag can't leak; MakeLordsAsAppropriate w/ empty pawns makes no lord (item-only auto-load fine); haulers
load only Things listed in leftToLoad[].things (FindThingToLoad) → must populate w/ real map stacks via
AllSendableItems; transporters are Normal-tickers (CompTransporter.CompTick) so use CompTick+IsHashIntervalTick,
NOT CompTickRare. **KEY UX GOTCHAS for testing (Luke confused 2026-07-02): (a) C# needs FULL RESTART — he saw old
slice-1 build (log confirmed mod loaded, 12 defs, no errors); (b) auto-load toggle gizmo + mod-options only exist
in slices 2+3 build; (c) the toggle gizmo shows ONLY after a default cargo is SAVED for that shuttle (via load
dialog Items-tab "Default cargo" button → Save current items); (d) slice-1 "Default cargo" button lives IN THE
LOAD DIALOG Items tab, NOT the bottom gizmo bar.** STILL: in-game test all 3 slices, MP 2-client test.
**PUBLISHED TO GITHUB 2026-07-02 (both PRIVATE repos, remote `origin`/master, gh acct lukegibson101):
github.com/lukegibson101/TransporterLoadouts (HEAD b519fb6) + github.com/lukegibson101/CrewPresets (HEAD
1374874).** Both have **About/Preview.png created** (1200x675, matching the shuttle mod's house style: Segoe UI
Black title + middot subtitle + dark blue-gray gradient + green accents; TL = "DEFAULT CARGO" manifest card +
arrow + transport pod; CP = preset menu [Mining Crew/Strike Team(active)/Pack Train] + arrow + highlighted 5-pawn
crew). Generated via System.Drawing PowerShell scripts (temp: gen_tl_preview.ps1 / gen_cp_preview.ps1) — GOTCHAS:
PS5.1 reads UTF-8 .ps1 as ANSI so build non-ASCII chars in-script via [char]0x00B7 (middot); PS comma operator
binds TIGHTER than +, so wrap every PointF/RectangleF arg via helper funcs taking pre-evaluated coords. TL toggle gizmo uses custom Textures/UI/Commands/AutoLoadDefaultCargo.png (down-arrow into open crate).
**BOTH PUBLISHED TO STEAM WORKSHOP 2026-07-02 (Luke uploaded in-game):** Crew Presets = item **3756372833**,
Transporter Loadouts = item **3756377821**. PublishedFileId.txt for BOTH captured back into workspace About/ +
committed+pushed (CP commit after its 1st upload; TL after its 1st). **PublishedFileId workflow VALIDATED via
litmus test: CP re-upload (to attach the preview it missed on the too-fast 1st upload) kept the SAME id 3756372833
= UPDATED not duplicated.** Previews: Luke considered reworking with real assets (real vanilla shuttle sprite at
`extracted_art/Shuttle.png`+`PassengerShuttle_south.png`; real in-game UI screenshots exist: 194823=Default-cargo
button in load dialog, 194627=Presets menu+colonist list, 195547=auto-load gizmo) but **DECIDED TO KEEP the
stylized mockup previews as-is ("they have their charm")** — do NOT redo them unless he re-asks. GOTCHA for future
Steam uploads: preview must be <1MB (ours 114KB ok); upload reads the Mods-folder copy, so Preview.png +
PublishedFileId.txt must be present THERE before hitting upload. NEXT: in-game test all 3 slices; MP 2-client test. **Luke re-emphasized 2026-07-02: MP-compatibility is MANDATORY for these mods.**
NEXT SESSION: build slice 2, then Luke tests slice 1+2 in-game.

**Key decisions (locked with Luke):**
- **SEPARATE standalone mod**, NOT part of Odyssey Shuttle Variants — it patches the VANILLA
  selection dialogs (works for drop pods / Royalty shuttle / gravships / pit gates / caravans /
  our craft), and must NOT carry an Odyssey/Biotech dependency.
- Crew group = named list of pawn refs, **any mix of colonists + animals + mechs**. **Per-save** storage
  (GameComponent, scribed like bills) — pawn membership is save-specific; no cross-save template layer.
- (Loadouts were ORIGINALLY a payload manifest bolted onto crew groups in one combined mod — SPLIT OUT
  2026-07-01 into Mod B above, and reframed from global named templates to per-shuttle-instance default cargo.)
- Crew Presets target dialogs (all use vanilla `TransferableOneWay`/`CountToTransfer`, each its own `Window`):
  `Dialog_LoadTransporters` (start here), `Dialog_FormCaravan`, `Dialog_EnterPortal` (pit gate/bunker),
  `Dialog_SplitCaravan`, `Dialog_BeginGravshipLaunch` (VERIFY it uses transferables — didn't show in grep).
- Apply = set CountToTransfer on present members; **skip-and-warn** for absent/dead ("Couldn't add: …").
  Editor grays dead/absent + quick-remove; lazy-prune gone pawns.
- Caps (seat count / mech bandwidth / cargo mass) already enforced by each dialog → preset just ticks.
- MP: group CRUD = synced (shared state, MultiplayerAPI like the shuttle mod); APPLYING is local UI only.
- Reuse the shuttle mod's `DialogAccess` AccessTools.FieldRefAccess pattern to reach each dialog's
  private `transferables`.
