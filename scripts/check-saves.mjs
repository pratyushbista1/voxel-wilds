import { readFile, readdir, mkdir, mkdtemp } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';
import assert from 'node:assert/strict';
import { ROOT, createGameServer, validateSave } from '../server.mjs';
import { Inventory, maxStack } from '../src/inventory.js';
import { World } from '../src/world.js';

await mkdir(path.join(ROOT, '.cache'), { recursive: true });
const temporarySaves = await mkdtemp(path.join(ROOT, '.cache', 'migration-test-'));
const server = createGameServer({ saveDir: temporarySaves });
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
const url = `http://127.0.0.1:${server.address().port}/api/worlds/`;
const hash = (bytes) => createHash('sha256').update(bytes).digest('hex');
try {
  const files = (await readdir(path.join(ROOT, 'saves'))).filter((name) => name.endsWith('.json'));
  for (const file of files) {
    const originalPath = path.join(ROOT, 'saves', file);
    const original = await readFile(originalPath);
    const saved = validateSave(JSON.parse(original), file.slice(0, -5));
    const inventory = saved.inventoryData
      ? new Inventory(saved.inventoryData)
      : Inventory.migrate(
          saved.mode === 'creative'
            ? Object.fromEntries(saved.hotbar.filter(Boolean).map((id) => [id, maxStack(id)]))
            : saved.inventory,
          saved.hotbar
        );
    if (saved.mode === 'survival' && saved.version === 1)
      assert.deepEqual(
        inventory.totals(true),
        Object.fromEntries(Object.entries(saved.inventory).filter(([, n]) => n > 0))
      );
    const world = new World(saved.seed, saved.edits);
    assert.deepEqual(world.serialize(), saved.edits);
    const upgraded = {
      ...saved,
      version: 2,
      inventory: inventory.totals(),
      hotbar: inventory.slots.slice(0, 9).map((s) => s?.id || 0),
      inventoryData: inventory.serialize(),
      player: {
        ...saved.player,
        saturation: saved.player.saturation ?? 5,
        exhaustion: saved.player.exhaustion ?? 0,
      },
      drops: saved.drops || [],
      furnaces: saved.furnaces || [],
    };
    validateSave(upgraded, saved.id);
    for (const data of [saved, upgraded]) {
      const response = await fetch(url + saved.id, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      });
      assert.equal(response.status, 200);
    }
    const restored = await (await fetch(url + saved.id)).json();
    assert.deepEqual(restored.inventoryData, upgraded.inventoryData);
    assert.deepEqual(restored.edits, saved.edits);
    assert.equal(restored.player.health, saved.player.health);
    assert.equal(hash(await readFile(originalPath)), hash(original));
    console.log(`PASS: ${file} migrates and reloads without altering the original.`);
  }
  console.log(`Checked ${files.length} existing worlds. Test copies: ${temporarySaves}`);
} finally {
  server.close();
}
