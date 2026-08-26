# Transporter Loadouts

A RimWorld 1.6 mod. Every transporter and shuttle remembers **its own** default cargo
manifest, pre-filled each time you open its load dialog — set a shuttle up once as a supply
runner ("50 meals + 100 chemfuel") and never re-tick the same items again.

**Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3756377821

## Features

- Opening a transporter's load dialog auto-fills its saved default cargo, clamped to what
  you actually have on the map.
- Save the current item selection as that transporter's default with one click, or clear it.
- Unloading keeps the default cargo aboard, so you don't reload the basics every mission.
- Loadouts are per-transporter and stored in the save, so one shuttle can be your supply
  runner and another your artillery drop.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

No DLC required. It patches the vanilla `Dialog_LoadTransporters`, so it covers drop pods,
shuttles (Royalty/Odyssey) and modded craft — anything whose `ThingDef` resolves a
`CompTransporter`.

## How it works

`CompDefaultCargo` holds the manifest and is scribed with the building. The comp is injected
in code at startup (`HarmonyBootstrap.InjectComps`) rather than by XML `PatchOperation`,
because patch operations run pre-inheritance and would miss transporters that inherit
`CompProperties_Transporter` from an abstract base. The "Default cargo" button is drawn on
the Items tab only, keeping clear of Crew Presets' Pawns-tab button.

## Multiplayer

Built with the [Multiplayer](https://github.com/rwmt/Multiplayer) mod in mind — manifest
edits are synced. Runs fine without Multiplayer installed (the API stub reports
`MP.enabled == false` and the sync code no-ops).

## Building

```
cd Source
dotnet build -c Release
```

Any modern .NET SDK compiles the `net48` target — no .NET Framework Developer Pack needed.
`Krafs.Rimworld.Ref` supplies the game reference assemblies, so no path to your RimWorld
install is required. The build drops `TransporterLoadouts.dll` and the `0MultiplayerAPI.dll`
stub into `Assemblies/` (gitignored — it is a build artifact).

To test in-game, copy the mod folder (minus `Source/`, `obj/`, `bin/`, `.git/`) into your
RimWorld `Mods/` directory. C# changes require a full game restart.

## Companion mods

- **[Crew Presets](https://steamcommunity.com/sharedfiles/filedetails/?id=3756372833)** —
  save named groups of colonists, animals and mechs and apply them in one click. This mod
  picks the freight; that one picks the people.
- **[Odyssey Shuttle Variants](https://steamcommunity.com/sharedfiles/filedetails/?id=3755775382)** —
  buildable, role-distinct craft for Odyssey.

## License

[MIT](LICENSE)
