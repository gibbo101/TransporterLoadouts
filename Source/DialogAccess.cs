using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TransporterLoadouts
{
    // Reflective access to the private bits of Dialog_LoadTransporters we need: the
    // transferable rows, the transporter comps being loaded, and which tab is showing.
    // (Same FieldRefAccess idiom the Crew Presets / shuttle mods use.)
    internal static class DialogAccess
    {
        private static readonly FieldInfo TransferablesField =
            AccessTools.Field(typeof(Dialog_LoadTransporters), "transferables");
        private static readonly FieldInfo TransportersField =
            AccessTools.Field(typeof(Dialog_LoadTransporters), "transporters");
        private static readonly FieldInfo TabField =
            AccessTools.Field(typeof(Dialog_LoadTransporters), "tab");
        private static readonly MethodInfo CountChangedMethod =
            AccessTools.Method(typeof(Dialog_LoadTransporters), "CountToTransferChanged");

        public static List<TransferableOneWay> Transferables(Dialog_LoadTransporters dlg) =>
            (List<TransferableOneWay>)TransferablesField.GetValue(dlg);

        public static List<CompTransporter> Transporters(Dialog_LoadTransporters dlg) =>
            (List<CompTransporter>)TransportersField.GetValue(dlg);

        // Tab enum is { Pawns = 0, Items = 1 }.
        public static bool OnItemsTab(Dialog_LoadTransporters dlg) =>
            Convert.ToInt32(TabField.GetValue(dlg)) == 1;

        public static void NotifyCountsChanged(Dialog_LoadTransporters dlg) =>
            CountChangedMethod.Invoke(dlg, null);
    }
}
