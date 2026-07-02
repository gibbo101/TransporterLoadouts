using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TransporterLoadouts
{
    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        static HarmonyBootstrap()
        {
            new Harmony("luke.transporterloadouts").PatchAll(Assembly.GetExecutingAssembly());
            InjectComps();
        }

        // Give every transporter def a CompDefaultCargo. Done in code (not an XML patch) so we
        // read the fully inheritance-resolved comp list - this reliably catches vanilla pods and
        // shuttles (which inherit CompProperties_Transporter from abstract bases) and modded
        // craft alike. Runs before any save loads, so the comp is part of the def when
        // transporters re-initialize their comps on load.
        private static void InjectComps()
        {
            int added = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.comps == null || def.comps.Count == 0) continue;
                if (!def.comps.Any(c => c.compClass == typeof(CompTransporter))) continue;
                if (def.comps.Any(c => c is CompProperties_DefaultCargo)) continue;
                def.comps.Add(new CompProperties_DefaultCargo());
                added++;
            }
            if (added > 0)
                Log.Message("[Transporter Loadouts] Added default-cargo comp to " + added + " transporter def(s).");
        }
    }

    // On open, pre-fill the transporter's saved default cargo into the item rows. Local UI only
    // (the final Accept is vanilla-synced). Skipped when a load is already in progress / ready
    // to launch, so we don't stomp an existing manifest the dialog restored.
    [HarmonyPatch(typeof(Dialog_LoadTransporters), nameof(Dialog_LoadTransporters.PostOpen))]
    public static class Patch_LoadTransporters_PostOpen
    {
        public static void Postfix(Dialog_LoadTransporters __instance)
        {
            List<CompTransporter> transporters = DialogAccess.Transporters(__instance);
            if (transporters == null || transporters.Count == 0) return;
            CompTransporter primary = transporters[0];
            if (primary.LoadingInProgressOrReadyToLaunch) return;

            CompDefaultCargo comp = primary.parent?.TryGetComp<CompDefaultCargo>();
            if (comp == null || !comp.HasManifest) return;

            LoadoutApplier.ApplyManifest(comp, DialogAccess.Transferables(__instance));
            DialogAccess.NotifyCountsChanged(__instance);
        }
    }

    // Draw the "Default cargo" header button (Items tab only).
    [HarmonyPatch(typeof(Dialog_LoadTransporters), nameof(Dialog_LoadTransporters.DoWindowContents))]
    public static class Patch_LoadTransporters_Header
    {
        public static void Postfix(Dialog_LoadTransporters __instance, Rect inRect) =>
            LoadoutUI.DrawHeaderControls(__instance, inRect);
    }
}
