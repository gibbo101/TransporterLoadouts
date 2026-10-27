using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TransporterLoadouts
{
    // Every player-issued change to a transporter goes through here, so each one runs as a
    // single synced command under the Multiplayer mod (registered in MultiplayerCompat).
    // Methods are addressed by the transporter Thing, which the sync layer serializes by id.
    // Pre-filling an open load dialog is local UI and lives elsewhere; its Accept is vanilla-synced.
    public static class CargoActions
    {
        public static void SetDefaultCargo(Thing transporter, List<ThingDef> defs, List<int> counts)
        {
            CompDefaultCargo comp = transporter?.TryGetComp<CompDefaultCargo>();
            if (comp == null) return;
            comp.SetManifest(defs, counts);
            NotifyIssuer(transporter, "Saved");
        }

        public static void ClearDefaultCargo(Thing transporter)
        {
            CompDefaultCargo comp = transporter?.TryGetComp<CompDefaultCargo>();
            if (comp == null) return;
            comp.Clear();
            NotifyIssuer(transporter, "Cleared");
        }

        // Left-click on the unload gizmo: the vanilla full unload, dropping everything aboard.
        public static void UnloadAll(Thing transporter)
        {
            CompTransporter trans = transporter?.TryGetComp<CompTransporter>();
            trans?.CancelLoad();
        }

        // Right-click "Unload all except default cargo". Runs the vanilla group cancel with
        // UnloadPatches.KeepDefaults set, so the CleanUpLoadingVars prefix keeps the default items
        // aboard and drops the rest. Calls the map overload rather than the no-arg CancelLoad,
        // which would also queue a shuttle ship-job unload that ejects the kept cargo.
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

        public static void SetAutoLoad(Thing transporter, bool value)
        {
            CompDefaultCargo comp = transporter?.TryGetComp<CompDefaultCargo>();
            comp?.SetAutoLoad(value);
        }

        // Confirms a manifest change to the player who made it; other MP clients stay silent.
        private static void NotifyIssuer(Thing transporter, string verb)
        {
            if (MultiplayerCompat.ShowUiForThisClient)
                Messages.Message(verb + " default cargo for " + transporter.LabelShortCap + ".",
                    transporter, MessageTypeDefOf.TaskCompletion, historical: false);
        }
    }
}
