using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TransporterLoadouts
{
    // One saved cargo line: a ThingDef and how many of it this transporter wants by default.
    // Its own IExposable so we don't depend on the exact scribe shape of a game class.
    public class CargoEntry : IExposable
    {
        public ThingDef def;
        public int count;

        public CargoEntry() { }

        public CargoEntry(ThingDef def, int count)
        {
            this.def = def;
            this.count = count;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref count, "count", 0);
        }
    }

    public class CompProperties_DefaultCargo : CompProperties
    {
        public CompProperties_DefaultCargo()
        {
            compClass = typeof(CompDefaultCargo);
        }
    }

    // Per-transporter default cargo manifest, scribed with the building. Added to every
    // ThingDef that has a CompTransporter at startup (see HarmonyPatches.InjectComps), so it
    // rides along on drop pods, shuttles and modded craft without an XML patch.
    public class CompDefaultCargo : ThingComp
    {
        private List<CargoEntry> manifest = new List<CargoEntry>();

        public bool HasManifest => manifest != null && manifest.Count > 0;
        public IReadOnlyList<CargoEntry> Manifest => manifest;

        // Replace the whole manifest. Callers pass parallel def/count lists (they serialize
        // cleanly over the Multiplayer sync layer). Zero/negative counts and null defs are
        // dropped; duplicate defs are merged.
        public void SetManifest(List<ThingDef> defs, List<int> counts)
        {
            manifest = new List<CargoEntry>();
            if (defs == null) return;
            for (int i = 0; i < defs.Count; i++)
            {
                ThingDef def = defs[i];
                int count = i < counts.Count ? counts[i] : 0;
                if (def == null || count <= 0) continue;
                CargoEntry existing = manifest.FirstOrDefault(e => e.def == def);
                if (existing != null) existing.count += count;
                else manifest.Add(new CargoEntry(def, count));
            }
        }

        public void Clear() => manifest = new List<CargoEntry>();

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref manifest, "defaultCargo", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && manifest == null)
                manifest = new List<CargoEntry>();
        }
    }
}
