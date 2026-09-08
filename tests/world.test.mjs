import test from 'node:test';
import assert from 'node:assert/strict';
import { World, raycast, collides, moveBody, CHUNK, HEIGHT, index } from '../src/world.js';
import { B, craft } from '../src/blocks.js';
import { RECIPES } from '../src/recipes.js';

test('seeds reproduce terrain and trees regardless of chunk generation order', () => {
  const a = new World('test-valley'),
    b = new World('test-valley'),
    c = new World('different-valley');
  a.getChunk(-1, 0);
  a.getChunk(0, 0);
  b.getChunk(0, 0);
  b.getChunk(-1, 0);
  assert.deepEqual(a.getChunk(-1, 0).data, b.getChunk(-1, 0).data);
  assert.deepEqual(a.getChunk(0, 0).data, b.getChunk(0, 0).data);
  assert.notDeepEqual(a.getChunk(0, 0).data, c.getChunk(0, 0).data);
});
test('negative coordinates and chunk-edge edits survive save and eviction', () => {
  const w = new World('seams');
  assert.equal(w.set(-1, 65, -16, B.BRICKS), true);
  assert.equal(w.set(16, 65, 0, B.GLASS), true);
  assert.equal(w.get(-1, 65, -16), B.BRICKS);
  assert.ok(w.dirty.has('-1,-1'));
  assert.ok(w.dirty.has('0,-1'));
  assert.ok(w.dirty.has('-1,-2'));
  const restored = new World('seams', JSON.parse(JSON.stringify(w.serialize())));
  assert.equal(restored.get(-1, 65, -16), B.BRICKS);
  assert.equal(restored.get(16, 65, 0), B.GLASS);
  restored.evict(30, 30, 1);
  assert.equal(restored.chunks.size, 0);
  assert.equal(restored.get(-1, 65, -16), B.BRICKS);
});
test('bedrock and world-height limits cannot be edited', () => {
  const w = new World('limits');
  assert.equal(w.set(0, 0, 0, 0), false);
  assert.equal(w.get(0, -1, 0), B.BEDROCK);
  assert.equal(w.set(0, HEIGHT, 0, 1), false);
  assert.equal(w.set(0, 10, 0, 999), false);
});
test('spawn finds a clear spot when a player builds a log column at the original spawn', () => {
  const w = new World('wildflower'),
    start = w.spawn(),
    x = Math.floor(start.x),
    z = Math.floor(start.z),
    y = Math.floor(start.y);
  w.set(x, y, z, B.LOG);
  w.set(x, y + 1, z, B.LOG);
  const safe = w.spawn();
  assert.equal(collides(w, safe.x, safe.y, safe.z), false);
  assert.notDeepEqual([safe.x, safe.z], [start.x, start.z]);
});
test('DDA returns nearest block and the correct placement face', () => {
  const cells = new Map([
      ['0,5,-3', B.STONE],
      ['0,5,-5', B.LOG],
    ]),
    w = { get: (x, y, z) => cells.get(`${x},${y},${z}`) || 0 };
  const hit = raycast(w, { x: 0.5, y: 5.5, z: 0.5 }, { x: 0, y: 0, z: -1 }, 7);
  assert.equal(hit.id, B.STONE);
  assert.deepEqual(hit.normal, { x: 0, y: 0, z: 1 });
  assert.equal(hit.distance, 2.5);
  assert.equal(raycast(w, { x: 0.5, y: 5.5, z: 0.5 }, { x: 1, y: 0, z: 0 }, 7), null);
  const boundary = raycast(
    { get: (x) => (x === -2 ? 1 : 0) },
    { x: -1, y: 65, z: 0 },
    { x: -1, y: 0, z: 0 }
  );
  assert.equal(boundary.x, -2);
  assert.equal(boundary.distance, 0);
});
test('collision supports landing and never auto-jumps a full block', () => {
  const floor = { get: (x, y, z) => (y < 1 ? B.STONE : 0) },
    p = { x: 0.5, y: 10, z: 0.5 },
    v = { x: 0, y: -30, z: 0 };
  const result = moveBody(floor, p, v, 0.5);
  assert.equal(result.grounded, true);
  assert.ok(p.y >= 0.999 && p.y < 1.002);
  assert.equal(v.y, 0);
  assert.equal(collides(floor, p.x, p.y, p.z), false);
  const wall = { get: (x, y, z) => (y < 1 || (x >= 2 && y < 5) ? B.STONE : 0) },
    p2 = { x: 0.5, y: 1.01, z: 0.5 },
    v2 = { x: 40, y: 0, z: 0 };
  moveBody(wall, p2, v2, 0.3, true);
  assert.ok(p2.x < 1.72);
  const step = { get: (x, y, z) => (y < 1 || (x === 1 && y === 1) ? B.STONE : 0) },
    p3 = { x: 0.5, y: 1.01, z: 0.5 },
    v3 = { x: 4, y: 0, z: 0 };
  moveBody(step, p3, v3, 0.25);
  assert.equal(p3.y, 1.01);
  assert.ok(p3.x < 0.712);
  v3.y = 8.3;
  for (let i = 0; i < 22; i++) {
    v3.x = 4;
    v3.y -= 25 / 60;
    moveBody(step, p3, v3, 1 / 60);
  }
  assert.ok(p3.y >= 2, 'a manual jump clears a one-block obstacle');
  assert.ok(p3.x > 1);
});
test('crafting consumes exact resources and failure leaves the bag unchanged', () => {
  const bag = { [B.LOG]: 1 },
    planks = RECIPES.find((r) => r.id === 'planks'),
    sticks = RECIPES.find((r) => r.id === 'sticks');
  assert.equal(craft(bag, planks), true);
  assert.equal(bag[B.LOG], 0);
  assert.equal(bag[B.PLANKS], 4);
  assert.equal(craft(bag, sticks), true);
  assert.equal(bag[B.PLANKS], 2);
  assert.equal(bag[90], 4);
  const before = structuredClone(bag);
  assert.equal(
    craft(
      bag,
      RECIPES.find((r) => r.id === 'ironpick')
    ),
    false
  );
  assert.deepEqual(bag, before);
});
