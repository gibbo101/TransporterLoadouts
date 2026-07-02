using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TransporterLoadouts
{
    // Loadout edits mutate per-building saved state, so they run as synced commands under the
    // Multiplayer mod (registered in MultiplayerCompat). Addressed by the transporter Thing,
    // which the sync layer serializes by id. Pre-filling a dialog on open is NOT here - that's
    // local UI (the final Accept is vanilla-synced).
    public static class CargoActions
    {
        public static void SetDefaultCargo(Thing transporter, List<ThingDef> defs, List<int> counts)
        {
            CompDefaultCargo comp = transporter?.TryGetComp<CompDefaultCargo>();
            if (comp == null) return;
            comp.SetManifest(defs, counts);
            if (MultiplayerCompat.ShowUiForThisClient)
                Messages.Message("Saved default cargo for " + transporter.LabelShortCap + ".",
                    transporter, MessageTypeDefOf.TaskCompletion, historical: false);
        }

        public static void ClearDefaultCargo(Thing transporter)
        {
            CompDefaultCargo comp = transporter?.TryGetComp<CompDefaultCargo>();
            if (comp == null) return;
            comp.Clear();
            if (MultiplayerCompat.ShowUiForThisClient)
                Messages.Message("Cleared default cargo for " + transporter.LabelShortCap + ".",
                    transporter, MessageTypeDefOf.TaskCompletion, historical: false);
        }

        // Left-click on the (replaced) unload gizmo: ordinary full unload, routed through a
        // synced method so it stays MP-consistent (KeepDefaults stays false -> vanilla drop-all).
        public static void UnloadAll(Thing transporter)
        {
            CompTransporter trans = transporter?.TryGetComp<CompTransporter>();
            trans?.CancelLoad();
        }

        // Right-click "Unload all except default cargo" (slice 2). Runs the vanilla group cancel
        // with a flag set, which our CleanUpLoadingVars prefix reads to keep the default items
        // aboard and drop the rest. We call the map overload directly (not the no-arg CancelLoad)
        // to skip the shuttle ship-job unload, so kept cargo isn't ejected again.
        public static void UnloadKeepingDefaults(Thing transporter)
        {
            CompTransporter trans = transporter?.TryGetComp<CompTransporter>();
            CompDefaultCargo comp = transporter?.TryGetComp<CompDefaultCargo>();
            if (trans == null || comp == null || !comp.HasManifest) return;
            if (!trans.LoadingInProgressOrReadyToLaunch) return;
            Map map = trans.Map;
            if (map == null) return;
            UnloadPatches.KeepDefaults = true;
            try { trans.CancelLoad(map); }
            finally { UnloadPatches.KeepDefaults = false; }
        }

        // The per-transporter auto-load toggle (slice 3).
        public static void SetAutoLoad(Thing transporter, bool value)
        {
            CompDefaultCargo comp = transporter?.TryGetComp<CompDefaultCargo>();
            comp?.SetAutoLoad(value);
        }
    }
}
