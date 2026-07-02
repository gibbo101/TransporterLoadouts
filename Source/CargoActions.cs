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
    }
}
