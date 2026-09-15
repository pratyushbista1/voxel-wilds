# Voxel Wilds

A single-player voxel survival sandbox built with Unity 6 and C#. Explore the
Overworld, the Nether and the End, build a home, craft equipment and fight a dragon.
Models are original, editable Blender assets.

## Play on Windows

Extract `Voxel-Wilds-Unity-Portable-2.0.0.zip` into a new folder, then run `Voxel Wilds.exe`.
Keep the executable, `Voxel Wilds_Data`, `UnityPlayer.dll` and the other packaged
files together. The executable alone is not a portable game.

The setup edition is `Voxel-Wilds-Unity-Setup-2.0.0.exe`. It installs a Start menu
shortcut and an uninstaller. Builds are unsigned, so Windows may show a publisher
warning. Check the download against `UNITY-PACKAGES-SHA256.txt` before sharing it.
The installed edition saves to `%APPDATA%/VoxelWildsUnity/saves/` when launched
from its shortcut. Uninstall through Windows Settings or `Uninstall Voxel Wilds.exe`
in the install folder. Your worlds and unknown personal files are kept.

Unity, Blender, Node.js and an internet connection are not needed to play.
The current build targets 64-bit Windows with DirectX 11 graphics, a mouse and a
keyboard. Old 1.2.0 builds are separate from the Unity edition.

## Controls

| Input | Action |
| --- | --- |
| W, A, S, D | Move |
| Mouse | Look |
| Space | Jump or swim upward |
| Left Ctrl | Sprint |
| Left Shift | Sneak or descend in Creative flight |
| Left mouse | Mine or attack |
| Right mouse | Place a block, use a station, bed, bucket or portal frame |
| Hold right mouse | Eat, block with a shield, or draw a bow |
| Release right mouse | Shoot a drawn bow |
| 1-9 or mouse wheel | Select hotbar item |
| E | Open or close inventory |
| F | Swap held item and offhand |
| Q / Ctrl+Q | Drop one item / a stack |
| G or double-tap Space | Toggle Creative flight |
| Middle mouse | Pick a block in Creative |
| Esc | Pause, close a screen or leave a bed |
| F11 | Toggle fullscreen |

There is no automatic block jumping. Death drops carried items and respawns the
player after three seconds. There are no respawn or teleport-to-spawn buttons.

## Worlds and survival

Worlds are seeded and generated in 16-block chunks. Terrain includes caves, ores,
trees, rivers, villages, village chests and underground dungeons. A village is
available near X 40, Z 40 in every seed. Villagers wander and flee nearby threats.

Survival includes damage, hunger, armor, shields, tool durability, mining, crafting,
chests and furnaces. Cows, pigs, sheep and chickens drop meat. Cook raw meat with
fuel in a furnace and hold right mouse to eat it. Creative provides an item catalog,
flight, spawn eggs and invulnerability.

The Overworld has a 20-minute day/night cycle. Zombies, skeletons, creepers, spiders
and endermen inhabit the world. Endermen normally leave you alone until stared at
or attacked; they teleport to dry, clear ground and take damage from water.
Skeletons shoot arrows, creepers explode and undead burn in sunlight.

Place a bed on two supported blocks. Using it sets the respawn point. At night,
sleeping with no nearby hostile mobs advances time to dawn. A missing bed falls
back to world spawn. Beds explode outside the Overworld.

## Inventory and crafting

The survival inventory has 27 storage slots, a nine-slot hotbar, four armor slots,
an offhand slot and a 2x2 crafting grid. A placed crafting table opens a 3x3 grid.
Chests contain 27 persistent slots.

Left-click moves stacks; right-click takes half or places one. Shift-click transfers
items, and number keys swap a hovered slot with the hotbar. Dragging distributes
single items to visited slots. Recipe-book entries arrange ingredients that are
available in the inventory. Click the crafting output to take it, or Shift-click
to craft repeatedly into the inventory. Overflow is retained or dropped rather
than discarded.

There are 41 recipes, including tools, armor, beds, buckets, bows and eyes of ender.
Recipes support translated and horizontally mirrored patterns. Furnaces preserve
fuel and cooking progress, and a lava fuel bucket returns an empty bucket.

## Water and lava

Fluids use scheduled block updates rather than static decorative blocks:

- Sources feed downward streams and weakening horizontal flow.
- Water spreads up to seven horizontal blocks; Overworld lava spreads three.
- Water updates every five game ticks. Lava updates every thirty ticks in the
  Overworld and every ten ticks in the Nether. There are twenty game ticks per second.
- Removing a source drains dependent flow. Two neighboring water sources can form
  a new source when the receiving block is supported.
- Water touching a lava source makes obsidian; flowing lava makes cobblestone.
  Lava falling into water makes stone.
- Buckets collect source blocks only. Water evaporates in the Nether.
- Players can swim, drown and take lava damage. Fluid states survive saving.

These are tested Minecraft-inspired rules, not a bit-for-bit implementation of
every Java or Bedrock Edition fluid edge case. Waterlogging, bubble columns and
all plant/fluid interactions are not implemented.

## Dimensions, spawners and the dragon

Build an obsidian frame four blocks wide and five blocks high, with a clear 2x3
opening. The corners are optional. Ignite it with flint and steel. Standing in the
portal transfers between the Overworld and Nether using an 8:1 coordinate scale.
Each dimension retains its own edits, containers, dropped items and mobs.

The Nether contains lava, fortress bridges, bastions, treasure, blazes, piglins,
brutes and wither skeletons. Mob spawners work while the player is within sixteen
blocks, check local space and nearby mob limits, and require appropriate light.
Torches can disable dark-spawning monsters; blaze spawners have different light rules.

The End entrance is near X 24, Y 33, Z 8. Use eyes of ender on twelve distinct portal
frames to unlock it in Survival. Creative can enter directly.

The End contains an island, obsidian pillars, destructible healing crystals and a
200-health dragon. The dragon circles, strafes, perches and leaves damaging breath
clouds. Use a bow to destroy elevated crystals and attack the dragon. Defeating it
unlocks the exit. Destroyed crystals and the defeated boss remain recorded in the save.

## Settings

Adjust view distance, field of view, mouse sensitivity, brightness, entity distance,
shadows, clouds, fog, view bobbing, fullscreen and difficulty. Frame limits include
30 through 360 FPS and Unlimited. Unlimited disables VSync and the application's
frame cap; it does not guarantee any particular frame rate and can increase heat.

## Saves

Unity worlds use checked `.vws` files. The default portable location is
`saves/unity/` beside the executable. Editor play uses this project's `saves/unity/`.
A non-writable location falls back to Unity's per-user application data directory.
The `-voxel-saves <directory>` launch argument selects a different save directory.

Autosave runs every twenty seconds of active play and when pausing or quitting.
Each successful replacement retains a checked `.bak` file. Corrupt primary files
can load from the backup; damaged originals are retained separately when saving.
Do not run two copies against the same world files.

JavaScript 1.x JSON worlds use a different terrain generator and are not loaded by
this edition. They are preserved in their original folders. Keep the old executable
to play them. The Unity conversion does not overwrite or upload personal worlds.

## Development

Open this repository's root folder directly in Unity Hub. It is the Unity project;
there is no nested project folder. Use **Unity 6000.3.23f1** and activate an appropriate
Unity license. Open `Assets/Scenes/Main.unity` and press Play.

Gameplay lives in `Assets/Scripts/Runtime`. Engine-independent world, inventory,
crafting, fluid and mob rules live in `Assets/Scripts/Core`. The built-in render
pipeline draws culled chunk meshes with separate collision and fluid meshes.

With the .NET 8 SDK installed:

```powershell
dotnet run --project Tests/CoreTests.csproj
```

Full local validation and building:

```powershell
./scripts/update-unity-meta.ps1
./scripts/test-unity.ps1
./scripts/build-unity.ps1 -SkipPackage
./scripts/test-player.ps1
./scripts/package-unity.ps1
./scripts/test-unity-installer.ps1
```

The editor scripts find Unity in `tools/Unity-6000.3.23f1/`, the standard Hub
installation path, or `UNITY_EDITOR_PATH`. API compilation uses the installed
Unity assemblies, not mock engine types. Player tests create disposable worlds
under `.cache/` and retain logs and screenshots under `artifacts/`.

Packaging requires NSIS 3. Install it in its standard location, put `makensis.exe`
on `PATH`, or pass `-Compiler <path-to-makensis.exe>` to `package-unity.ps1`.
Use `build-unity.ps1 -SkipPackage` when you only need the native player.

The default CI workflow runs engine-independent C# tests on Windows and Linux and
checks Unity metadata. It does not claim to run the Unity editor or the player.
The manually dispatched Windows build workflow additionally requires a supported
Unity CI license and the secrets described by [GameCI's activation guide](https://game.ci/docs/github/activation/).
Do not commit license files, credentials or local Unity settings.

## Blender assets

`Assets/Resources/Models/` contains 23 FBX models available to Unity. `ArtSource/`
contains editable Blender scenes and the earlier GLB exports.

```powershell
./tools/blender-4.5.13-windows-x64/blender.exe --background --factory-startup --python scripts/build_unity_models.py
```

The exporter rebuilds runtime models and the four new Blender sources. Save
hand-edited variants under different names before regeneration.

## Scope and license

This is an independent Minecraft-inspired game, not Minecraft or an exact replica.
It does not contain Mojang textures, sounds or models. Multiplayer, redstone,
enchanting, brewing, villager trading, full crop growth, animal breeding and complete
Minecraft content parity are not implemented. Some animations, terrain and combat
rules differ. The original 1.x browser and Electron code remains in repository
history; the active application is Unity C#.

Code and original assets use the [MIT license](LICENSE). Unity is separately
licensed by Unity Technologies. See [third-party notices](THIRD_PARTY_NOTICES.md).
