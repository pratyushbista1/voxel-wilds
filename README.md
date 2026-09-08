# Voxel Wilds

A singleplayer voxel sandbox with seeded terrain, mining, building, crafting and survival.
Built with JavaScript, Three.js and Electron, with editable Blender models.

## Play on Windows

Use one of the files in `release/`:

- `Voxel-Wilds-Portable-1.1.0.exe`: run directly. Keep it in a writable folder.
- `Voxel-Wilds-Setup-1.1.0.exe`: install with desktop and Start menu shortcuts.
  The installation includes `Uninstall Voxel Wilds.exe`.
- `Voxel-Wilds-Uninstall-1.1.0.exe`: opens the installed game's uninstaller.

Friends only need the portable EXE or the setup EXE. Node.js, Chrome, Blender and an
internet connection are not required to play. The builds target 64-bit Windows.
A mouse, keyboard and hardware-accelerated graphics are required.

These builds are unsigned. Windows may display a publisher or SmartScreen warning.
Share the checksum file along with the EXE so recipients can verify the download.

To uninstall an installed copy, use Windows Settings > Apps > Voxel Wilds, or run
`Uninstall Voxel Wilds.exe` from its installation folder. Saved worlds are retained.
The portable version has no installer: remove its EXE when you no longer want it;
keep its `userdata/` folder if you want to retain worlds.

## Controls

| Input                 | Action                                         |
| --------------------- | ---------------------------------------------- |
| W, A, S, D            | Move                                           |
| Mouse                 | Look                                           |
| Space                 | Jump or swim up                                |
| Ctrl                  | Sprint                                         |
| Shift                 | Sneak or fly down                              |
| Hold left mouse       | Mine or attack                                 |
| Right mouse           | Place a block or open a crafting table/furnace |
| Hold right mouse      | Eat berries or block with a shield             |
| Middle mouse          | Pick a block in Creative                       |
| 1-9 or mouse wheel    | Select a hotbar slot                           |
| E                     | Inventory                                      |
| F                     | Swap selected item with offhand                |
| Q / Ctrl+Q            | Drop one item / a stack                        |
| Double-tap Space or G | Toggle Creative flight                         |
| H                     | Controls and crafting help                     |
| Esc                   | Pause                                          |
| F11                   | Fullscreen                                     |

Walking into a block does not jump automatically. Creative is invulnerable;
damage, hunger and tool wear apply in Survival.

## Inventory and crafting

The inventory follows Java Edition's layout: 27 storage slots, a nine-slot
hotbar, four armor slots, an offhand slot and a 2x2 personal crafting grid.
Right-click a placed crafting table for the 3x3 grid.

Left-click picks up, moves or merges a stack. Right-click takes half or places
one item. Shift-click transfers items. Drag across slots to distribute a stack;
right-drag places one per slot. Double-click collects matching items. Number keys
swap the hovered slot with the corresponding hotbar slot.

Recipes use shaped patterns and allow translation and horizontal mirroring.
The recipe book arranges available ingredients; click the output to craft,
or Shift-click it to craft as many as fit. Larger recipes require a table.

A log makes four planks. Four planks in a square make a table. Two planks placed
vertically make four sticks. Pickaxes need three materials across the top row
and two sticks below the center. A furnace needs eight cobblestone around an
empty center. Furnaces smelt raw iron, clay, sand and other supported inputs,
with fuel in the lower slot.

Stacks hold up to 64 items; tools, armor and shields occupy individual slots.
Durability is saved per item and equipment breaks at zero. Full inventories
leave excess items on the ground. Inventory screens do not pause Survival;
press Esc to pause safely. On death, items drop and remain for five minutes.

## Worlds and saves

| Run mode                             | Save location                    |
| ------------------------------------ | -------------------------------- |
| Source launcher or `npm run desktop` | `Game/saves/`                    |
| Portable EXE                         | `userdata/saves/` beside the EXE |
| Installed EXE                        | `%APPDATA%/Voxel Wilds/saves/`   |

Worlds are JSON files. Autosave runs every 20 seconds of play, on pause, when
closing inventory, and before quitting. Each world retains its previous valid
save as a `.bak` file. An unreadable primary save falls back to that backup.

Version 1 worlds load and migrate automatically, keeping terrain edits and
inventory counts. Close the game before copying saves between versions or PCs.
Do not run two copies against the same save folder. Keep the game open if a save
error appears.

## Development

Install Node.js 22.12 or newer, then run these commands from this folder:

```powershell
npm ci
npm run setup:desktop
npm run desktop
```

For browser play, use `npm start`. The launcher starts a loopback-only server and
opens the game. Do not open `index.html` directly. `Play Voxel Wilds.bat` prefers
the built desktop app when present, keeping the existing worlds in `Game/saves/`;
otherwise it starts the browser version.

```powershell
npm test
npm run test:browser
npm run test:desktop
npm run format:check
npm run build:win
npm run test:portable
```

The browser tests use Chrome at its usual Windows installation path. Set
`CHROME_PATH` to use a different installation. Tests use isolated worlds under
`.cache/`; screenshots and reports go to `artifacts/`. Desktop tests run in a
hidden window. Set `VOXEL_PACKAGED_EXE` to test the unpacked release executable.

`npm run test:installer` installs into an isolated test folder, runs the desktop
test, then uninstalls it and verifies that its save survives. It refuses to
overwrite an existing installed copy. `node scripts/check-saves.mjs` checks local
world migrations on temporary copies without modifying the original saves.

The Windows build creates an NSIS setup installer and a portable EXE.
Build files, downloads and package caches stay in this project folder.
A fresh build needs internet access to download dependencies and packaging tools.

## Blender models

`assets/models/` contains the seven GLB models loaded by the game.
`assets/blender/` contains their editable sources and the collection scene.
The game never runs Blender at runtime.

![Model collection](assets/model-gallery.png)

To regenerate the assets with Blender 4.5:

```powershell
blender --background --factory-startup --python scripts/build_models.py
```

Regeneration replaces the generated model files. Save hand-edited variants
under different names first.

## Scope and license

This is an independent Minecraft-inspired sandbox, not a complete Minecraft
implementation. It includes the crafting patterns for its available items, not
Minecraft's entire item catalog. It has no multiplayer, redstone, farming,
portals, enchanting or fluid simulation. Water is static and building is limited
to Y 1-71.

Code and original assets use the [MIT license](LICENSE).
See [third-party notices](THIRD_PARTY_NOTICES.md) for dependencies and trademarks.
