using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace TransporterLoadouts
{
    // One saved cargo line: a ThingDef and how many of it this transporter wants by default.
    // A dedicated IExposable keeps the save format independent of any game class's scribe shape.
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
    // ThingDef that has a CompTransporter at startup (see HarmonyBootstrap.InjectComps), so it
    // rides along on drop pods, shuttles and modded craft without an XML patch.
    public class CompDefaultCargo : ThingComp
    {
        private List<CargoEntry> manifest = new List<CargoEntry>();

        // Auto-load override for this transporter. When autoLoadSet is false the effective state
        // follows the mod-settings default for the current map type; the override is cleared on
        // every fresh spawn (see PostSpawnSetup), so it re-evaluates after a change of map.
        private bool autoLoadSet;
        private bool autoLoad;

        public bool HasManifest => manifest != null && manifest.Count > 0;
        public IReadOnlyList<CargoEntry> Manifest => manifest;

        public bool AutoLoadActive => autoLoadSet ? autoLoad : MapDefault;

        private bool MapDefault
        {
            get
            {
                Map map = parent.MapHeld;
                bool colony = map != null && map.IsPlayerHome;
                // Mod settings are per-machine and sim-tick auto-load reads this, so in
                // multiplayer the fixed shipped default applies to keep clients in step.
                if (MultiplayerCompat.InMultiplayer)
                    return colony;
                TransporterLoadoutsSettings s = TransporterLoadoutsMod.Settings;
                if (s == null) return colony;
                return colony ? s.autoLoadColonyMaps : s.autoLoadOtherMaps;
            }
        }

        // Called only from the synced CargoActions.SetAutoLoad.
        public void SetAutoLoad(bool value)
        {
            autoLoadSet = true;
            autoLoad = value;
        }

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

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            // A genuine spawn (built, or arrived on a new map after a flight) resets the
            // override so the transporter follows this map type's default. A save reload keeps
            // the player's choice.
            if (!respawningAfterLoad)
                autoLoadSet = false;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned) return;
            if (!parent.IsHashIntervalTick(250)) return; // deterministic per-thing interval
            TryAutoLoad();
        }

        // Sim-tick auto-load: if enabled and idle on a colony map, set this transporter to load
        // its default cargo (clamped to what's reachable) so haulers fill it - no dialog needed.
        // Runs in the deterministic sim, identically on every MP client, so it needs no sync.
        private void TryAutoLoad()
        {
            if (!HasManifest || !AutoLoadActive) return;
            Map map = parent.Map;
            if (map == null || !map.IsPlayerHome) return;

            CompTransporter trans = parent.GetComp<CompTransporter>();
            if (trans == null || trans.LoadingInProgressOrReadyToLaunch) return;

            // Leave quest shuttles that auto-load their own required manifest alone.
            CompShuttle shuttle = parent.GetComp<CompShuttle>();
            if (shuttle != null && shuttle.Autoload) return;

            List<CompTransporter> group = new List<CompTransporter> { trans };
            List<Thing> sendable;
            try { sendable = TransporterUtility.AllSendableItems(group, map).ToList(); }
            catch { return; }

            List<KeyValuePair<TransferableOneWay, int>> toAdd = BuildLoadList(sendable);
            if (toAdd.Count == 0) return;

            TransporterUtility.InitiateLoading(group);
            foreach (KeyValuePair<TransferableOneWay, int> kv in toAdd)
                trans.AddToTheToLoadList(kv.Key, kv.Value);
        }

        // One transferable per manifest line that has any matching sendable stock, paired with
        // the count to load: the line's target, clamped to the stock available.
        private List<KeyValuePair<TransferableOneWay, int>> BuildLoadList(List<Thing> sendable)
        {
            List<KeyValuePair<TransferableOneWay, int>> toAdd = new List<KeyValuePair<TransferableOneWay, int>>();
            foreach (CargoEntry entry in manifest)
            {
                List<Thing> things = new List<Thing>();
                int available = 0;
                for (int i = 0; i < sendable.Count; i++)
                {
                    if (sendable[i].def != entry.def) continue;
                    things.Add(sendable[i]);
                    available += sendable[i].stackCount;
                }
                if (things.Count == 0) continue;
                int want = Mathf.Min(entry.count, available);
                if (want <= 0) continue;
                TransferableOneWay tow = new TransferableOneWay();
                tow.things.AddRange(things);
                toAdd.Add(new KeyValuePair<TransferableOneWay, int>(tow, want));
            }
            return toAdd;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref manifest, "defaultCargo", LookMode.Deep);
            Scribe_Values.Look(ref autoLoadSet, "autoLoadSet", false);
            Scribe_Values.Look(ref autoLoad, "autoLoad", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && manifest == null)
                manifest = new List<CargoEntry>();
        }
    }
}
