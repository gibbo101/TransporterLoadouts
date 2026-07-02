using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace TransporterLoadouts
{
    // A Command_Action that also offers extra right-click float-menu options (the base Gizmo
    // exposes RightClickFloatMenuOptions only as a read-only virtual, so we subclass to add ours).
    public class Command_UnloadTransporter : Command_Action
    {
        public List<FloatMenuOption> extraOptions;

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                foreach (FloatMenuOption o in base.RightClickFloatMenuOptions) yield return o;
                if (extraOptions != null)
                    foreach (FloatMenuOption o in extraOptions) yield return o;
            }
        }
    }

    // Shared state + field access for the slice 2 (unload keeps defaults) and slice 3
    // (auto-load toggle gizmo) patches on CompTransporter.
    public static class UnloadPatches
    {
        // Set only for the duration of a synced "unload all except default cargo" (see
        // CargoActions.UnloadKeepingDefaults). The CleanUpLoadingVars prefix reads it; every
        // other caller (vanilla left-click unload, launch, leave) sees it false.
        public static bool KeepDefaults;

        internal static readonly AccessTools.FieldRef<CompTransporter, List<TransferableOneWay>> LeftToLoadRef =
            AccessTools.FieldRefAccess<CompTransporter, List<TransferableOneWay>>("leftToLoad");
        internal static readonly AccessTools.FieldRef<CompTransporter, bool> MassDirtyRef =
            AccessTools.FieldRefAccess<CompTransporter, bool>("massUsageDirty");

        private static Texture2D autoLoadIcon;
        internal static Texture2D AutoLoadIcon =>
            autoLoadIcon ?? (autoLoadIcon = ContentFinder<Texture2D>.Get("UI/Commands/LoadTransporter"));
        private static Texture2D cancelIcon;
        internal static Texture2D CancelIcon =>
            cancelIcon ?? (cancelIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"));
    }

    // Keep the default cargo aboard when the player chose "unload all except default cargo".
    // Runs per group member; each consults its own manifest. Diverts ONLY when KeepDefaults is
    // set AND this comp has a manifest - otherwise vanilla (drop everything, reset groupID).
    [HarmonyPatch(typeof(CompTransporter), nameof(CompTransporter.CleanUpLoadingVars))]
    public static class Patch_CleanUpLoadingVars
    {
        public static bool Prefix(CompTransporter __instance, Map map)
        {
            if (!UnloadPatches.KeepDefaults) return true;
            CompDefaultCargo comp = __instance.parent?.TryGetComp<CompDefaultCargo>();
            if (comp == null || !comp.HasManifest) return true;

            // Remaining count to keep, per def.
            Dictionary<ThingDef, int> keep = new Dictionary<ThingDef, int>();
            foreach (CargoEntry e in comp.Manifest)
            {
                keep.TryGetValue(e.def, out int c);
                keep[e.def] = c + e.count;
            }

            ThingOwner owner = __instance.GetDirectlyHeldThings();
            for (int i = owner.Count - 1; i >= 0; i--)
            {
                Thing t = owner[i];
                keep.TryGetValue(t.def, out int k);
                if (k >= t.stackCount)
                {
                    keep[t.def] = k - t.stackCount; // keep the whole stack
                    continue;
                }
                if (k > 0) keep[t.def] = 0;
                int dropCount = t.stackCount - k;
                owner.TryDrop(t, __instance.parent.Position, map, ThingPlaceMode.Near, dropCount, out _);
            }

            // Nothing left queued to load; keep groupID >= 0 so it stays ready-to-launch.
            UnloadPatches.LeftToLoadRef(__instance)?.Clear();
            UnloadPatches.MassDirtyRef(__instance) = true;
            return false; // skip the vanilla drop-all + groupID reset
        }
    }

    // Add the right-click "unload except default" option to the unload gizmo, and the per-
    // transporter "Auto-load default cargo" toggle. Both only when a default is saved.
    [HarmonyPatch(typeof(CompTransporter), nameof(CompTransporter.CompGetGizmosExtra))]
    public static class Patch_CompGetGizmosExtra
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, CompTransporter __instance)
        {
            CompDefaultCargo comp = __instance.parent?.TryGetComp<CompDefaultCargo>();
            bool hasManifest = comp != null && comp.HasManifest;
            Thing thing = __instance.parent;

            foreach (Gizmo g in __result)
            {
                // The unload / cancel-load gizmo (icon = the Cancel texture). Replace it with our
                // subclass: left-click = ordinary full unload, right-click = keep default cargo.
                // Both routed through synced methods so it's MP-consistent. Only when a default
                // is saved; otherwise the vanilla gizmo is left untouched.
                if (hasManifest && g is Command_Action ca && ca.icon == UnloadPatches.CancelIcon)
                {
                    yield return new Command_UnloadTransporter
                    {
                        defaultLabel = ca.defaultLabel,
                        defaultDesc = ca.defaultDesc,
                        icon = ca.icon,
                        action = () =>
                        {
                            SoundDefOf.Designate_Cancel.PlayOneShotOnCamera();
                            CargoActions.UnloadAll(thing);
                        },
                        extraOptions = new List<FloatMenuOption>
                        {
                            new FloatMenuOption("Unload all except default cargo",
                                () => CargoActions.UnloadKeepingDefaults(thing))
                        }
                    };
                    continue;
                }
                yield return g;
            }

            if (hasManifest && thing != null && thing.Spawned)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "Auto-load default cargo",
                    defaultDesc = "When on, colonists automatically load this transporter's saved default cargo while it sits idle on a colony map.\n\nThe default for each map type is set in Options -> Mod options -> Transporter Loadouts; a transporter switches to that default when it changes maps.",
                    icon = UnloadPatches.AutoLoadIcon,
                    isActive = () => comp.AutoLoadActive,
                    toggleAction = () => CargoActions.SetAutoLoad(thing, !comp.AutoLoadActive)
                };
            }
        }
    }
}
