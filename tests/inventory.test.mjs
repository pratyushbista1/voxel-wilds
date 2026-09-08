import test from 'node:test';
import assert from 'node:assert/strict';
import { Inventory, makeStack, maxStack } from '../src/inventory.js';
import { RECIPES, matchRecipe } from '../src/recipes.js';
import { tickFurnace, makeFurnace } from '../src/furnace.js';
import { validateSave } from '../server.mjs';

test('legacy bags migrate without losing counts or hotbar order', () => {
  const bag = { 2: 1, 5: 2, 6: 8, 7: 4, 20: 6, 93: 0, 100: 1 };
  const inv = Inventory.migrate(bag, [100, 5, 2, 6, 20, 7, 0, 0, 93]);
  assert.deepEqual(inv.totals(true), { 2: 1, 5: 2, 6: 8, 7: 4, 20: 6, 100: 1 });
  assert.equal(inv.slots[0].durability, 59);
  assert.deepEqual(Inventory.migrate({ 5: 260, 101: 2 }, [5, 5, 101]).totals(true), {
    5: 260,
    101: 2,
  });
  const huge = Inventory.migrate({ 5: 2400 }, [5]);
  assert.equal(huge.totals(true)[5], 2400);
  assert.ok(huge.overflow.length);
});

test('right-click splits odd stacks and places one; left-click merges with a 64 limit', () => {
  const s = new Inventory();
  s.slots[0] = makeStack(5, 9);
  s.click('bag', 0, 2);
  assert.equal(s.cursor.count, 5);
  assert.equal(s.slots[0].count, 4);
  s.click('bag', 1, 2);
  assert.equal(s.slots[1].count, 1);
  assert.equal(s.cursor.count, 4);
  s.slots[2] = makeStack(5, 63);
  s.click('bag', 2);
  assert.equal(s.slots[2].count, 64);
  assert.equal(s.cursor.count, 3);
  s.click('bag', 1);
  assert.equal(s.cursor, null);
  assert.equal(s.slots[1].count, 4);
});

test('drag distributes evenly, deduplicates crossed slots and respects equipment slots', () => {
  const s = new Inventory();
  s.cursor = makeStack(5, 10);
  s.distribute([
    { name: 'bag', index: 0 },
    { name: 'bag', index: 1 },
    { name: 'bag', index: 0 },
    { name: 'bag', index: 2 },
    { name: 'armor', index: 0 },
  ]);
  assert.deepEqual(
    s.slots.slice(0, 3).map((v) => v.count),
    [3, 3, 3]
  );
  assert.equal(s.cursor.count, 1);
  s.distribute([{ name: 'bag', index: 1 }], true);
  assert.equal(s.cursor, null);
  assert.equal(s.armor[0], null);
});

test('shift-click, number-key swaps and double-click collection conserve items', () => {
  const s = new Inventory();
  s.slots[0] = makeStack(5, 50);
  s.slots[10] = makeStack(5, 20);
  s.quickMove('bag', 0);
  assert.equal(s.slots[10].count, 64);
  assert.equal(s.slots[9].count, 6);
  assert.equal(s.slots[0], null);
  s.swapHotbar('bag', 10, 3);
  assert.equal(s.slots[3].count, 64);
  s.click('bag', 9);
  s.collect();
  assert.equal(s.cursor.count, 64);
  assert.equal(s.slots[3].count, 6);
  s.close();
  assert.equal(s.totals(true)[5], 70);
});

test('crafting accepts shifted shapes, rejects wrong arrangements and requires a 3x3 table for tools', () => {
  const s = new Inventory();
  s.grid[3] = makeStack(5);
  assert.equal(s.recipe().recipe.id, 'planks');
  s.craftOutput();
  assert.deepEqual(s.cursor, makeStack(7, 4));
  assert.ok(s.grid.every((v) => !v));
  s.close();
  s.grid[0] = makeStack(7);
  s.grid[1] = makeStack(7);
  assert.equal(s.recipe(), null);
  const pick = RECIPES.find((r) => r.id === 'woodpick');
  assert.equal(s.fillRecipe(pick), false);
  s.close();
  s.insert(makeStack(7, 3));
  s.insert(makeStack(90, 2));
  s.openGrid(3);
  assert.equal(s.fillRecipe(pick), true);
  assert.equal(s.recipe().recipe.id, 'woodpick');
  assert.equal(s.craftOutput(), 1);
  assert.equal(s.cursor.durability, 59);
  const boots = RECIPES.find((r) => r.id === 'boots');
  assert.equal(
    matchRecipe(
      boots.pattern.map((id) => makeStack(id)),
      3
    ).recipe.id,
    'boots'
  );
});

test('craft output never consumes input when cursor or inventory cannot accept it', () => {
  const s = new Inventory();
  s.grid[0] = makeStack(5, 3);
  s.cursor = makeStack(7, 63);
  assert.equal(s.craftOutput(), 0);
  assert.equal(s.grid[0].count, 3);
  s.cursor = null;
  s.slots = s.slots.map(() => makeStack(8, 64));
  assert.equal(s.craftOutput(true), 0);
  assert.equal(s.grid[0].count, 3);
  s.slots[0] = null;
  assert.equal(s.craftOutput(true), 12);
  assert.equal(s.slots[0].count, 12);
});

test('failed recipe fill is transactional, including cursor and overflow', () => {
  const s = new Inventory();
  s.grid[0] = makeStack(5);
  s.cursor = makeStack(8, 2);
  const before = s.serialize();
  assert.equal(s.fillRecipe(RECIPES.find((r) => r.id === 'workbench')), false);
  assert.deepEqual(s.serialize(), before);
});

test('tools have independent durability, do not stack, break at zero and survive serialization', () => {
  const s = new Inventory();
  s.insert(makeStack(100));
  s.insert(makeStack(100));
  assert.equal(s.slots[1].count, 1);
  assert.equal(s.damageTool(0, 58), false);
  assert.equal(s.slots[0].durability, 1);
  assert.equal(s.slots[1].durability, 59);
  const restored = new Inventory(JSON.parse(JSON.stringify(s.serialize())));
  assert.equal(restored.slots[0].durability, 1);
  assert.equal(restored.damageTool(0), true);
  assert.equal(restored.slots[0], null);
  assert.equal(restored.slots[1].durability, 59);
  restored.insert(makeStack(110));
  restored.quickMove('bag', 0);
  assert.equal(restored.armor[0].id, 110);
  restored.cursor = makeStack(112);
  assert.equal(restored.click('armor', 0), false);
});

test('closing inventory returns grid and cursor and exposes excess as recoverable drops', () => {
  const s = new Inventory();
  s.slots = s.slots.map(() => makeStack(8, 64));
  s.grid[0] = makeStack(5, 6);
  s.cursor = makeStack(100, 1, 17);
  const saved = new Inventory(JSON.parse(JSON.stringify(s.serialize())));
  const drops = saved.close();
  assert.deepEqual(drops, [makeStack(5, 6), makeStack(100, 1, 17)]);
  assert.equal(saved.cursor, null);
  assert.ok(saved.grid.every((v) => !v));
  assert.equal(saved.overflow.length, 0);
});

test('random slot operations preserve item totals and valid stack sizes', () => {
  const s = Inventory.migrate({ 5: 153, 7: 201, 8: 79, 100: 3, 110: 1 }, [5, 7, 100]);
  const expected = s.totals(true);
  let seed = 34919;
  const rand = (n) => {
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    return seed % n;
  };
  for (let i = 0; i < 2500; i++) {
    const region = rand(3) === 0 ? 'grid' : 'bag',
      size = region === 'grid' ? 4 : 36;
    if (i % 5 === 0)
      s.distribute(
        Array.from({ length: 3 }, () => ({ name: region, index: rand(size) })),
        !!rand(2)
      );
    else if (i % 7 === 0) s.collect();
    else s.click(region, rand(size), rand(2) * 2, rand(5) === 0);
    assert.deepEqual(s.totals(true), expected);
    for (const stack of [...s.slots, ...s.grid, s.cursor])
      if (stack) assert.ok(stack.count >= 1 && stack.count <= maxStack(stack.id));
  }
});

test('furnaces burn fuel, smelt one input in ten seconds and preserve progress on save', () => {
  const f = makeFurnace();
  f.slots[0] = makeStack(95, 2);
  f.slots[1] = makeStack(91);
  for (let i = 0; i < 500; i++) tickFurnace(f, 0.01);
  assert.equal(f.slots[1], null);
  assert.equal(f.slots[2], null);
  const restored = JSON.parse(JSON.stringify(f));
  for (let i = 0; i < 501; i++) tickFurnace(restored, 0.01);
  assert.equal(restored.slots[2].id, 92);
  assert.equal(restored.slots[2].count, 1);
  assert.equal(restored.slots[0].count, 1);
  restored.slots[2].count = 64;
  const before = restored.slots[0].count;
  for (let i = 0; i < 1200; i++) tickFurnace(restored, 0.01);
  assert.equal(restored.slots[0].count, before);
});

test('version 2 validates inventory, durability, furnace and drop state', () => {
  const s = new Inventory();
  s.insert(makeStack(101, 1, 54));
  const data = {
    version: 2,
    id: 'v2',
    name: 'World',
    seed: 'test',
    mode: 'survival',
    edits: [['0,40,0', 23]],
    player: {
      x: 0,
      y: 41,
      z: 0,
      yaw: 0,
      pitch: 0,
      health: 19,
      hunger: 18,
      saturation: 2,
      exhaustion: 1,
    },
    inventory: s.totals(),
    hotbar: s.slots.slice(0, 9).map((v) => v?.id || 0),
    inventoryData: s.serialize(),
    drops: [{ x: 1, y: 42, z: 1, age: 2, stack: makeStack(5, 6) }],
    furnaces: [['0,40,0', makeFurnace()]],
    time: 400,
    playTime: 1,
  };
  assert.equal(validateSave(data, 'v2'), data);
  data.inventoryData.slots[0].durability = -1;
  assert.throws(() => validateSave(data, 'v2'), /durability/);
  data.inventoryData.slots[0].durability = 54;
  data.inventoryData.slots[0].count = 2;
  assert.throws(() => validateSave(data, 'v2'), /stack/);
});
