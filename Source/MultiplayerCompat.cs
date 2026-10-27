using Multiplayer.API;
using Verse;

namespace TransporterLoadouts
{
    // Registers each public CargoActions method for sync when the Multiplayer mod is running:
    // each mutates per-building state and syncs as a single call addressed by the transporter
    // Thing. Filling default cargo into an open dialog stays local (it only ticks rows; Accept
    // is vanilla-synced). When Multiplayer is absent, the shipped 0MultiplayerAPI stub reports
    // MP.enabled = false and registration is skipped.
    [StaticConstructorOnStartup]
    public static class MultiplayerCompat
    {
        static MultiplayerCompat()
        {
            if (!MP.enabled) return;
            MP.RegisterSyncMethod(typeof(CargoActions), nameof(CargoActions.SetDefaultCargo));
            MP.RegisterSyncMethod(typeof(CargoActions), nameof(CargoActions.ClearDefaultCargo));
            MP.RegisterSyncMethod(typeof(CargoActions), nameof(CargoActions.UnloadAll));
            MP.RegisterSyncMethod(typeof(CargoActions), nameof(CargoActions.UnloadKeepingDefaults));
            MP.RegisterSyncMethod(typeof(CargoActions), nameof(CargoActions.SetAutoLoad));
        }

        // For UI side-effects inside synced methods: only the issuing client shows them.
        public static bool ShowUiForThisClient => !MP.enabled || MP.IsExecutingSyncCommandIssuedBySelf;

        // True while in an actual multiplayer session. Mod settings are per-machine and not
        // synced, so sim code must not branch on them while this is true.
        public static bool InMultiplayer => MP.enabled && MP.IsInMultiplayer;
    }
}
