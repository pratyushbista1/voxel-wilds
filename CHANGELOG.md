# Changelog

## 2.0.3

- Replaced generic inventory symbols with original 32-pixel item art and textured block icons.
- Rebuilt inventory and crafting panels with gray beveled slots, an avatar preview, armor and offhand slots, and a toggleable recipe book.
- Added equal-share left dragging and one-item right dragging while preserving stack counts and equipment restrictions.
- Tuned walking, forward sprinting, air control, braking, jump height, camera bob and sprint field-of-view transitions.
- Added native inventory screenshots, click and drag checks, and movement regression tests.

## 2.0.2

- Added a visible first-person arm and hand, smoother equip transitions, and repeating mining and attack swings.
- Corrected alternating limb movement, grounded walking cadence, and idle settling for mobs.
- Added working two-block village doors with saved orientation and open state.
- Added interaction checks for hand visibility, animation cycles, door collision, and door save behavior.

## 2.0.1

- Replaced flat block colors with original face-specific pixel textures and an atlas with mip padding.
- Added sunlight shadow receiving, adjustable block-corner shading and nearby block lighting.
- Added a day/night gradient sky, sunset colors, a sun, a moon and stars.
- Added animated water highlights and sky-color reflections, plus glowing lava and portals.
- Restored detailed Blender models for placed chests, beds, lanterns and campfires.
- Added grass tufts, crossed plant meshes and textured blocks in the player's hand.
- Added optional HDR cinematic tonemapping, bloom and exposure controls.
- Added Low, Medium, High and Ultra presets, MSAA, shadow resolution and distance, texture filtering and anisotropic filtering.
- Split settings into scrollable Graphics and Gameplay tabs, with separate VSync and frame-limit controls.
- Added an isolated native graphics-check scene with saved comparison screenshots.
- Kept 2.0.0 builds as a launcher fallback and preserved existing Unity saves.

## 2.0.0

- Replaced the active browser and Electron application with a Unity 6 C# project.
- Added native Windows builds, a portable ZIP and a per-user installer with an uninstaller.
- Added endermen, villagers, an End island, healing crystals and a dragon boss fight.
- Added villages, dungeons, fortress and bastion structures, and active mob spawners.
- Added scheduled water and lava flow, draining sources, buckets and fluid reactions.
- Rebuilt inventory, crafting, armor, offhand, chests, furnaces and tool durability.
- Preserved automatic respawning, bed respawn points, sleeping and the day/night cycle.
- Added separate dimension state and checked saves with recoverable backups.
- Kept cursor and crafting-grid items safe during autosave and reload.
- Imported 23 original FBX models, including four new editable Blender models.
- Added graphics controls including Unlimited FPS, shadows, fog, clouds and view distance.
- Added engine-independent tests, real Unity API compilation, editor checks and native gameplay tests.
- Preserved legacy source in history and local backups. Old JSON worlds remain separate and are not converted.

## 1.2.0

- Added automatic respawning after death and removed both manual spawn buttons.
- Added two-block beds, saved respawn points, safe fallback spawning and sleeping to dawn.
- Added zombies, skeletons, creepers, spiders, farm animals and Nether mobs.
- Added melee pursuit, knockback, ranged attacks, daylight burning and creeper explosions.
- Added animal meat drops, cooking, gold equipment and new crafting recipes.
- Added persistent 27-slot chests and generated treasure.
- Added obsidian portals, paired dimension travel and separate Overworld/Nether saves.
- Added Nether terrain, lava damage, fortresses, bastions and blaze spawners.
- Added twelve editable Blender models and a survival model gallery.
- Added Unlimited FPS, render scale, brightness and expanded graphics/difficulty controls.
- Added version 3 save validation and retained loading support for older worlds.
- Added automated tests for sleeping, combat, loot, portals and death/reload edge cases.
- Moved new Windows builds into versioned release folders, preserving older builds.

## 1.1.0

- Removed automatic one-block step-up and tightened collision resolution.
- Added smooth tool swings, walking transitions, mouse sway and joint-based creature animation.
- Fixed half-heart rendering and immediate damage updates.
- Added individual tool durability, armor wear, shields and breakage.
- Replaced the inventory with 36 stack slots, armor, offhand and cursor handling.
- Added stack splitting, drag distribution, Shift-click transfer and hotbar swaps.
- Added shaped 2x2 and 3x3 crafting, a recipe book, furnaces and persistent smelting.
- Added dropped items, pickup, overflow handling and drops on death.
- Added hunger exhaustion, eating time and fall-distance damage.
- Added version 1 save migration and protected valid backups from corrupt primary saves.
- Added Windows portable and setup executables with uninstall support.
- Added save-before-close handling and an isolated desktop renderer.
- Updated menu copy, controls, documentation and automated tests.

## 1.0.0

- Initial browser release with Creative and Survival modes, seeded terrain and Blender models.
