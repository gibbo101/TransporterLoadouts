using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TransporterLoadouts
{
    // Pure operations over a dialog's transferable rows: read the current item selection,
    // fill a saved manifest into the rows, and test whether the manifest is already satisfied.
    // All local UI work - the final Accept is vanilla-synced, so none of this needs syncing.
    internal static class LoadoutApplier
    {
        // Item rows only (pawns are handled on the other tab) that the player has ticked > 0.
        public static void CaptureCurrentItems(List<TransferableOneWay> transferables,
            out List<ThingDef> defs, out List<int> counts)
        {
            defs = new List<ThingDef>();
            counts = new List<int>();
            foreach (TransferableOneWay t in transferables)
            {
                if (!IsItem(t) || t.CountToTransfer <= 0) continue;
                defs.Add(t.ThingDef);
                counts.Add(t.CountToTransfer);
            }
        }

        // Set each manifest def's row toward its target count (auto-clamped to what's on the
        // map). Returns the number of defs that couldn't be fully satisfied for a shortfall note.
        public static int ApplyManifest(CompDefaultCargo comp, List<TransferableOneWay> transferables)
        {
            int shortfalls = 0;
            foreach (CargoEntry entry in comp.Manifest)
            {
                TransferableOneWay row = transferables.FirstOrDefault(t => IsItem(t) && t.ThingDef == entry.def);
                if (row == null)
                {
                    shortfalls++;
                    continue;
                }
                row.AdjustTo(entry.count);
                if (row.CountToTransfer < entry.count) shortfalls++;
            }
            return shortfalls;
        }

        // Green-tint test: every manifest def is filled to its target (or maxed out at what's
        // available). Extra items the player added beyond the manifest don't matter.
        public static bool DefaultsSatisfied(CompDefaultCargo comp, List<TransferableOneWay> transferables)
        {
            foreach (CargoEntry entry in comp.Manifest)
            {
                TransferableOneWay row = transferables.FirstOrDefault(t => IsItem(t) && t.ThingDef == entry.def);
                if (row == null) return false;
                int available = Available(row);
                if (available <= 0) return false;
                int target = entry.count < available ? entry.count : available;
                if (row.CountToTransfer < target) return false;
            }
            return true;
        }

        private static bool IsItem(TransferableOneWay t) =>
            t.HasAnyThing && !(t.AnyThing is Pawn);

        private static int Available(TransferableOneWay t)
        {
            int sum = 0;
            for (int i = 0; i < t.things.Count; i++) sum += t.things[i].stackCount;
            return sum;
        }
    }
}
