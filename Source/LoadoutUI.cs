using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace TransporterLoadouts
{
    // The single "Default cargo" button drawn into the load dialog's header, on the Items tab
    // only (so it stays clear of the Crew Presets "Presets..." button, which is Pawns-tab only,
    // when both mods are installed). Left-click opens a menu: fill / save current / clear.
    internal static class LoadoutUI
    {
        private static readonly Color ActiveColor = new Color(0.55f, 1f, 0.55f);
        private const float ButtonWidth = 170f;

        public static void DrawHeaderControls(Dialog_LoadTransporters dlg, Rect inRect)
        {
            if (!DialogAccess.OnItemsTab(dlg))
                return;

            List<CompTransporter> transporters = DialogAccess.Transporters(dlg);
            if (transporters == null || transporters.Count == 0)
                return;
            // The default cargo is the primary transporter's manifest. Loading a whole group of
            // pods through one dialog is uncommon; if it happens, the first pod's default drives.
            Thing transporter = transporters[0].parent;
            CompDefaultCargo comp = transporter?.TryGetComp<CompDefaultCargo>();
            if (comp == null)
                return;

            List<TransferableOneWay> transferables = DialogAccess.Transferables(dlg);

            Rect btn = new Rect(inRect.xMax - ButtonWidth, 6f, ButtonWidth, 30f);
            Text.Font = GameFont.Small;
            bool satisfied = comp.HasManifest && LoadoutApplier.DefaultsSatisfied(comp, transferables);
            if (satisfied) GUI.color = ActiveColor;
            string label = comp.HasManifest ? "Default cargo (" + comp.Manifest.Count + ")" : "Default cargo...";
            bool clicked = Widgets.ButtonText(btn, label.Truncate(btn.width - 20f));
            GUI.color = Color.white;
            TooltipHandler.TipRegion(btn, comp.HasManifest
                ? "This transporter has a saved default cargo, filled in automatically when you open its load dialog.\n\nClick to fill it now, overwrite it with the current items, or clear it."
                : "Save the current item selection as this transporter's default cargo. It will be pre-filled every time you load it.");

            if (clicked)
                OpenMenu(dlg, transporter, comp, transferables);
        }

        private static void OpenMenu(Dialog_LoadTransporters dlg, Thing transporter,
            CompDefaultCargo comp, List<TransferableOneWay> transferables)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();

            if (comp.HasManifest)
            {
                opts.Add(new FloatMenuOption("Fill default cargo (" + comp.Manifest.Count + " item type" + (comp.Manifest.Count == 1 ? "" : "s") + ")", () =>
                {
                    int shortfalls = LoadoutApplier.ApplyManifest(comp, transferables);
                    DialogAccess.NotifyCountsChanged(dlg);
                    if (shortfalls > 0)
                        Messages.Message(shortfalls + " default item type" + (shortfalls == 1 ? "" : "s") + " couldn't be fully loaded (not enough on the map).",
                            MessageTypeDefOf.CautionInput, historical: false);
                }));
            }

            opts.Add(new FloatMenuOption("Save current items as default", () =>
                SaveCurrent(transporter, comp, transferables)));

            if (comp.HasManifest)
            {
                opts.Add(new FloatMenuOption("Clear default cargo", () =>
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Clear the saved default cargo for " + transporter.LabelShortCap + "?",
                        () => CargoActions.ClearDefaultCargo(transporter)))));
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private static void SaveCurrent(Thing transporter, CompDefaultCargo comp, List<TransferableOneWay> transferables)
        {
            LoadoutApplier.CaptureCurrentItems(transferables, out List<ThingDef> defs, out List<int> counts);
            if (defs.Count == 0)
            {
                Messages.Message("No items are selected to save as a default.", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            if (comp.HasManifest)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Overwrite the saved default cargo for " + transporter.LabelShortCap + " with the current selection (" + defs.Count + " item type" + (defs.Count == 1 ? "" : "s") + ")?",
                    () => CargoActions.SetDefaultCargo(transporter, defs, counts)));
            }
            else
            {
                CargoActions.SetDefaultCargo(transporter, defs, counts);
            }
        }
    }
}
