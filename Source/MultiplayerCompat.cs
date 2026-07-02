using Multiplayer.API;
using Verse;

namespace TransporterLoadouts
{
    // Registers loadout edits as sync methods when the Multiplayer mod is running. Filling a
    // transporter's default cargo into an open dialog stays local (it only ticks rows; Accept
    // is vanilla-synced). Saving/clearing a default mutates per-building state, so those sync
    // as single calls addressed by the transporter Thing. When Multiplayer is absent, the
    // shipped 0MultiplayerAPI stub reports MP.enabled = false and these no-op.
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

        // True while in an actual multiplayer session. Used to keep sim-tick auto-load
        // deterministic: mod settings are per-machine (not synced), so sim code must not branch
        // on them in MP - it falls back to the fixed on-colony / off-elsewhere default instead.
        public static bool InMultiplayer => MP.enabled && MP.IsInMultiplayer;
    }
}
