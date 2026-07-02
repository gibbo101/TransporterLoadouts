using UnityEngine;
using Verse;

namespace TransporterLoadouts
{
    // Global defaults for the per-transporter "Auto-load default cargo" toggle, shown under
    // Options -> Mod options -> Transporter Loadouts. A transporter with no explicit override
    // follows the setting for whichever map type it is currently on, and re-evaluates when it
    // changes maps (see CompDefaultCargo.AutoLoadActive).
    public class TransporterLoadoutsSettings : ModSettings
    {
        public bool autoLoadColonyMaps = true;
        public bool autoLoadOtherMaps = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref autoLoadColonyMaps, "autoLoadColonyMaps", true);
            Scribe_Values.Look(ref autoLoadOtherMaps, "autoLoadOtherMaps", false);
            base.ExposeData();
        }
    }

    public class TransporterLoadoutsMod : Mod
    {
        public static TransporterLoadoutsSettings Settings;

        public TransporterLoadoutsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<TransporterLoadoutsSettings>();
        }

        public override string SettingsCategory() => "Transporter Loadouts";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);
            list.Label("Default “Auto-load default cargo” state for transporters, by map type. Each transporter switches to the matching default when it changes maps; you can still flip any individual transporter with its gizmo.");
            list.Gap();
            list.CheckboxLabeled("On colony maps", ref Settings.autoLoadColonyMaps,
                "When on, a transporter sitting on one of your colony maps automatically loads its saved default cargo.");
            list.CheckboxLabeled("On other maps (mission sites, etc.)", ref Settings.autoLoadOtherMaps,
                "When on, a transporter that is away from your colony maps automatically loads its saved default cargo. Off by default so meals and supplies aboard stay available to your pawns while away.");
            list.End();
        }
    }
}
