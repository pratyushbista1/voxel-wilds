import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { B } from '../src/blocks.js';
import { World, collides, moveBody } from '../src/world.js';
import { buildPortal, lightPortal, findPortalFrame, collapsePortals } from '../src/portals.js';
import { netherStructures } from '../src/structures.js';
import { Inventory, makeStack, maxStack } from '../src/inventory.js';
import { makeFurnace, tickFurnace } from '../src/furnace.js';
import { matchRecipe } from '../src/recipes.js';
import { normalizeSettings } from '../src/settings.js';
import { Mobs } from '../src/mobs.js';
import { Survival } from '../src/survival.js';
import { validateSave } from '../server.mjs';

const flat = () => {
  const edits = new Map();
  return {
    dimension: 'overworld',
    seedHash: 1,
    edits,
    get: (x, y, z) =>
      edits.get(`${Math.floor(x)},${Math.floor(y)},${Math.floor(z)}`) ?? (y < 1 ? B.STONE : B.AIR),
    set(x, y, z, id) {
      edits.set(`${x},${y},${z}`, id);
      return true;
    },
    surface: () => 1,
  };
};
test('Nether terrain and both structures are deterministic across chunk order', () => {
  const a = new World('underworld', [], 'nether'),
    b = new World('underworld', [], 'nether');
  a.getChunk(2, 3);
  b.getChunk(3, 2);
  assert.deepEqual(a.getChunk(3, 2).data, b.getChunk(3, 2).data);
  for (const s of netherStructures(0, 0, 160).filter((s) => s.x > 0 && s.z > 0)) {
    if (s.kind === 'fortress') {
      assert.equal(a.get(s.x, s.y, s.z), B.NETHER_BRICKS);
      assert.equal(a.get(s.x, s.y + 1, s.z + 23), B.SPAWNER);
    } else assert.equal(a.get(s.x, s.y + 2, s.z), B.CHEST);
  }
  assert.equal(a.get(0, 71, 0), B.BEDROCK);
  assert.ok(a.surface(0, 0) < 60);
});
test('Obsidian portal frames ignite in either direction and reject broken borders', () => {
  for (const [dx, dz] of [
    [1, 0],
    [0, 1],
  ]) {
    const w = flat();
    buildPortal(w, -4, 2, -4, dx, dz);
    const frame = findPortalFrame(w, -4 + dx, 3, -4 + dz);
    assert.equal(frame.cells.length, 6);
    for (const cell of frame.cells) w.set(...cell, 0);
    assert.ok(lightPortal(w, -4 + dx, 3, -4 + dz));
    w.set(-4, 3, -4, 0);
    assert.equal(findPortalFrame(w, -4 + dx, 3, -4 + dz), null);
    collapsePortals(w, -4, 3, -4);
    for (const cell of frame.cells) assert.equal(w.get(...cell), B.AIR);
  }
});
test('Beds have a two-row recipe, do not stack, and have half-height collision', () => {
  const recipe = matchRecipe(
    [20, 20, 20, 7, 7, 7, 0, 0, 0].map((id) => makeStack(id)),
    3
  );
  assert.equal(recipe.recipe.output, B.BED);
  assert.equal(maxStack(B.BED), 1);
  const w = flat();
  w.set(0, 1, 0, B.BED);
  assert.equal(collides(w, 0.5, 1.6, 0.5), false);
  assert.equal(collides(w, 0.5, 1.3, 0.5), true);
  const p = { x: 0.5, y: 2.6, z: 0.5 },
    v = { x: 0, y: -10, z: 0 };
  const result = moveBody(w, p, v, 0.2);
  assert.equal(result.grounded, true);
  assert.equal(p.y, 1.5625);
});
test('Igniting an already active portal never consumes more flint and steel durability', () => {
  const world = flat(),
    storage = new Inventory();
  storage.slots[0] = makeStack(130);
  const game = {
    world,
    storage,
    selected: 0,
    mode: 'survival',
    keys: new Set(),
    inventoryChanged() {},
    sound: { play() {} },
    ui: { toast() {} },
  };
  const survival = new Survival(game);
  buildPortal(world, 0, 1, 0);
  const frame = findPortalFrame(world, 1, 2, 0);
  for (const cell of frame.cells) world.set(...cell, B.AIR);
  const target = { x: 1, y: 1, z: 0, id: B.OBSIDIAN, normal: { x: 0, y: 1, z: 0 } };
  assert.equal(survival.interact(target, { igniter: true }), true);
  assert.equal(storage.slots[0].durability, 63);
  for (let i = 0; i < 10; i++) survival.interact(target, { igniter: true });
  assert.equal(storage.slots[0].durability, 63);
});
test('Chest transfers and meat smelting conserve stacks', () => {
  const inv = new Inventory();
  inv.chest = Array(27).fill(null);
  inv.slots[0] = makeStack(122, 4);
  inv.quickMove('bag', 0);
  assert.equal(inv.chest[0].count, 4);
  assert.equal(inv.slots[0], null);
  inv.quickMove('chest', 0);
  assert.equal(inv.slots[0].count, 4);
  assert.equal(inv.chest[0], null);
  for (const [raw, cooked] of [
    [120, 121],
    [122, 123],
    [124, 125],
    [126, 127],
  ]) {
    const f = makeFurnace();
    f.slots = [makeStack(raw, 2), makeStack(91), null];
    for (let i = 0; i < 202; i++) tickFurnace(f, 0.05);
    assert.equal(f.slots[2].id, cooked);
    assert.equal(f.slots[0].count, 1);
  }
});
test('Zombie melee is blocked by walls, mobs take damage, and animal kills drop meat', () => {
  const w = flat(),
    hits = [],
    drops = [];
  const g = {
    settings: { difficulty: 'normal', entityDistance: 64 },
    storage: new Inventory(),
    mobSites: new Set(),
    stats: {},
    isNight: () => true,
    blockLight: () => 0,
    hurt: (...hit) => hits.push(hit),
    drops: { spawn: (stack) => drops.push(stack) },
  };
  const mobs = new Mobs(new THREE.Scene(), w, { make: () => new THREE.Group() }, g);
  g.creatures = mobs;
  const zombie = mobs.spawn('zombie', 0.5, 0.5, 1),
    player = { x: 0.5, y: 1, z: 2 };
  w.set(0, 1, 1, B.STONE);
  w.set(0, 2, 1, B.STONE);
  mobs.update(0.05, player, true, true);
  assert.equal(hits.length, 0);
  w.set(0, 1, 1, 0);
  w.set(0, 2, 1, 0);
  mobs.update(0.05, player, true, true);
  assert.equal(hits.length, 1);
  assert.equal(hits[0][0], 3);
  const cow = mobs.spawn('cow', 5, 5, 1);
  mobs.hurt(cow, 100);
  assert.ok(drops.some((s) => s.id === 122 && s.count >= 1));
  assert.equal(mobs.list.includes(cow), false);
  mobs.hurt(zombie, 7, new THREE.Vector3(0, 0, 1));
  assert.equal(zombie.health, 13);
  assert.ok(zombie.velocity.z > 0);
  mobs.dispose();
});
test('Unlimited FPS and video settings are validated without changing defaults', () => {
  const s = normalizeSettings({
    maxFps: 0,
    renderScale: 999,
    brightness: -2,
    distance: 99,
    difficulty: 'bogus',
  });
  assert.equal(s.maxFps, 0);
  assert.equal(s.renderScale, 150);
  assert.equal(s.brightness, 25);
  assert.equal(s.distance, 12);
  assert.equal(s.difficulty, 'normal');
});
test('Undead burn in daylight while cover protects them; peaceful removes hostile mobs', () => {
  const w = flat();
  const g = {
    settings: { difficulty: 'normal', entityDistance: 64 },
    storage: new Inventory(),
    mobSites: new Set(),
    stats: {},
    isNight: () => false,
    blockLight: () => 0,
    hurt() {},
    drops: { spawn() {} },
  };
  const mobs = new Mobs(new THREE.Scene(), w, { make: () => new THREE.Group() }, g);
  const exposed = mobs.spawn('zombie', 0.5, 0.5, 1);
  const sheltered = mobs.spawn('skeleton', 5.5, 0.5, 1);
  for (let x = 3; x <= 8; x++) for (let z = -2; z <= 3; z++) w.set(x, 4, z, B.STONE);
  exposed.walking = sheltered.walking = false;
  exposed.timer = sheltered.timer = 100;
  for (let i = 0; i < 25; i++) mobs.update(0.05, { x: 0, y: 1, z: 12 }, false, false);
  assert.ok(exposed.health < 20);
  assert.equal(sheltered.health, 20);
  g.settings.difficulty = 'peaceful';
  mobs.update(0.05, { x: 0, y: 1, z: 12 }, false, true);
  assert.equal(mobs.list.length, 0);
  mobs.dispose();
});
test('Gold pacifies piglins until provoked, but never pacifies brutes', () => {
  const w = flat(),
    g = {
      settings: { difficulty: 'normal' },
      storage: new Inventory(),
      stats: {},
      isNight: () => false,
    };
  const mobs = new Mobs(new THREE.Scene(), w, { make: () => new THREE.Group() }, g);
  const piglin = mobs.spawn('piglin', 0.5, 0.5, 1),
    brute = mobs.spawn('brute', 5.5, 0.5, 1);
  assert.equal(mobs.hostile(piglin), true);
  g.storage.armor[0] = makeStack(150);
  assert.equal(mobs.hostile(piglin), false);
  assert.equal(mobs.hostile(brute), true);
  mobs.alertPiglins();
  assert.equal(mobs.hostile(piglin), true);
  mobs.dispose();
});
test('Generated fortress spawners produce blazes and stop after being mined', () => {
  const w = new World('spawner', [], 'nether');
  const g = {
    settings: { difficulty: 'normal', entityDistance: 64 },
    storage: new Inventory(),
    mobSites: new Set(['fortress:48,40']),
    stats: {},
    isNight: () => true,
    blockLight: () => 15,
    hurt() {},
    drops: { spawn() {} },
  };
  const mobs = new Mobs(new THREE.Scene(), w, { make: () => new THREE.Group() }, g);
  mobs.spawnTimer = 11;
  mobs.update(0.05, { x: 48.5, y: 35, z: 58.5 }, true, true);
  assert.equal(mobs.list.filter((m) => m.kind === 'blaze').length, 1);
  for (const m of [...mobs.list]) mobs.remove(m);
  w.set(48, 35, 63, B.AIR);
  mobs.spawnTimer = 11;
  mobs.update(0.05, { x: 48.5, y: 35, z: 58.5 }, true, true);
  assert.equal(mobs.list.filter((m) => m.kind === 'blaze').length, 0);
  mobs.dispose();
});
test('Version 3 saves validate both dimensions, bed positions, loot and portal links', () => {
  const inv = new Inventory();
  const state = {
    edits: [['1,30,1', B.CHEST]],
    furnaces: [],
    drops: [],
    mobs: [],
    containers: [['1,30,1', Array(27).fill(null)]],
    mobSites: [],
  };
  const save = {
    version: 3,
    id: 'expansion-test',
    name: 'Expansion',
    seed: 'test',
    mode: 'survival',
    ...state,
    inventory: {},
    inventoryData: inv.serialize(),
    hotbar: Array(9).fill(0),
    player: {
      x: 0,
      y: 35,
      z: 0,
      yaw: 0,
      pitch: 0,
      health: 20,
      hunger: 20,
      saturation: 5,
      exhaustion: 0,
    },
    time: 400,
    playTime: 1,
    dimension: 'overworld',
    dimensions: { overworld: state, nether: structuredClone(state) },
    bedSpawn: { x: 1, y: 30, z: 3, facing: 0, dimension: 'overworld' },
    portalLinks: [
      {
        overworld: { x: 0, y: 30, z: 0, dx: 1, dz: 0 },
        nether: { x: 0, y: 25, z: 0, dx: 1, dz: 0 },
      },
    ],
  };
  assert.equal(validateSave(save, save.id), save);
  const broken = structuredClone(save);
  broken.dimensions.nether.edits[0][1] = 250;
  assert.throws(() => validateSave(broken, broken.id));
  const badBed = structuredClone(save);
  badBed.bedSpawn.facing = 7;
  assert.throws(() => validateSave(badBed, badBed.id));
  const badPortal = structuredClone(save);
  badPortal.portalLinks[0].nether.dx = 2;
  assert.throws(() => validateSave(badPortal, badPortal.id));
  const badChest = structuredClone(save);
  badChest.dimensions.nether.containers[0][1][0] = { id: 25, count: 2 };
  assert.throws(() => validateSave(badChest, badChest.id));
  const mismatch = structuredClone(save);
  mismatch.edits = [...mismatch.edits, ['1,31,1', B.GOLD_BLOCK]];
  assert.throws(() => validateSave(mismatch, mismatch.id));
});
