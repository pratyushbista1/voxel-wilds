import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { mkdir, mkdtemp, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { createGameServer, ROOT } from '../server.mjs';

await mkdir(path.join(ROOT, '.cache'), { recursive: true });
await mkdir(path.join(ROOT, 'artifacts'), { recursive: true });
const saveDir = await mkdtemp(path.join(ROOT, '.cache', 'browser-test-'));
const server = createGameServer({ saveDir });
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
const base = `http://127.0.0.1:${server.address().port}`;
const browser = await chromium.launch({
  headless: true,
  executablePath:
    process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  env: { ...process.env, TEMP: path.join(ROOT, '.cache'), TMP: path.join(ROOT, '.cache') },
});
const page = await browser.newPage({
  viewport: { width: 1440, height: 900 },
  deviceScaleFactor: 1,
});
page.setDefaultTimeout(120000);
const errors = [],
  warnings = [],
  failedRequests = [],
  checks = [];
page.on('pageerror', (e) => {
  errors.push(e.message);
  console.error('PAGE ERROR:', e.message);
});
page.on('console', (m) => {
  if (m.type() === 'warning') warnings.push(m.text());
  if (m.type() === 'error') errors.push(m.text());
});
page.on('requestfailed', (r) => failedRequests.push({ url: r.url(), failure: r.failure() }));
page.on('dialog', (d) => d.accept());
const check = (name) => {
  checks.push(name);
  console.log('PASS:', name);
};
const state = () => page.evaluate(() => window.__wilds.inspect());
const shot = (name) => page.screenshot({ path: path.join(ROOT, 'artifacts', name + '.png') });
async function playing() {
  await page.waitForFunction(
    () =>
      window.__wilds.inspect().active &&
      (!window.__wilds.inspect().paused ||
        !document.getElementById('pause-modal').classList.contains('hidden'))
  );
  if ((await state()).paused) await page.locator('#resume-button').click();
  await page.waitForFunction(
    () =>
      !window.__wilds.inspect().paused &&
      document.pointerLockElement === document.getElementById('game-canvas')
  );
}
async function key(code, simulationMs) {
  await page.keyboard.down(code);
  try {
    const until = (await state()).playTime + simulationMs / 1000;
    await page.waitForFunction((until) => window.__wildsTest.game.playTime >= until, until);
  } finally {
    await page.keyboard.up(code);
  }
}
async function clickButton(button) {
  await page.mouse.down({ button });
  await page.mouse.up({ button });
}
async function fixture(block = 3) {
  await page.evaluate((block) => {
    const t = window.__wildsTest;
    for (let x = -4; x <= 4; x++)
      for (let z = -6; z <= 4; z++) {
        t.setBlock(x, 55, z, 3);
        for (let y = 56; y < 60; y++) t.setBlock(x, y, z, 0);
      }
    t.setBlock(0, 57, -3, block);
    t.pose(0.5, 56.02, 0.5, 0, 0);
    t.game.player.flying = false;
    t.game.actionCooldown = 0;
  }, block);
  await page.waitForFunction(
    () => window.__wilds.inspect().target?.x === 0 && window.__wilds.inspect().target?.z === -3
  );
}
async function pause() {
  await page.keyboard.press('Escape');
  await page.waitForSelector('#pause-modal:not(.hidden)');
}
async function quit() {
  await pause();
  await page.locator('#save-quit-button').click();
  await page.waitForSelector('#menu:not(.hidden)');
}

let failure = null;
try {
  await page.goto(base + '/?test=1');
  await page.waitForSelector('#menu:not(.hidden)', { timeout: 60000 });
  if (process.env.CI) {
    await page.evaluate(() => {
      const g = window.__wildsTest.game;
      g.settings.distance = 3;
      g.settings.shadows = false;
      g.renderer.setPixelRatio(0.5);
      g.syncSettingsUI();
      g.applySettings();
    });
  }
  assert.equal((await state()).models.length, 7);
  await shot('title-screen');
  check('Title, terrain and all seven Blender models load');
  await page.locator('#new-world-button').click();
  await page.locator('#world-name').fill('Creative test');
  await page.locator('[data-mode="creative"]').click();
  await page.locator('#create-play-button').click();
  await playing();
  await fixture();
  await page.evaluate(() => window.__wildsTest.setBlock(0, 56, -1, 3));
  const initialY = (await state()).player.y;
  await key('w', 800);
  let p = (await state()).player;
  assert.ok(Math.abs(p.y - initialY) < 0.04, 'walking into a block cannot raise player Y');
  assert.ok(p.z >= 0.288, 'wall collision stops forward movement');
  check('Walking against a full block does not auto-jump');
  await page.keyboard.down('w');
  await key('Space', 340);
  await page.keyboard.up('w');
  assert.ok((await state()).player.y > 56.9);
  check('Manual jump clears a one-block obstacle');
  await page.keyboard.press('g');
  const beforeFly = (await state()).player.y;
  await key('Space', 600);
  assert.ok((await state()).player.y > beforeFly + 2);
  check('Creative flight ascends smoothly');
  await fixture(3);
  await page.mouse.down({ button: 'left' });
  await page.waitForFunction(() => window.__wildsTest.block(0, 57, -3) === 0);
  await page.mouse.up({ button: 'left' });
  check('Mining changes terrain');
  await fixture(3);
  await page.keyboard.press('1');
  await clickButton('right');
  await page.waitForFunction(() => window.__wildsTest.block(0, 57, -2) === 1);
  check('Block placement respects selected hotbar stack');
  await page.keyboard.press('e');
  await page.waitForSelector('#inventory-modal:not(.hidden)');
  await page.locator('#creative-search').fill('bricks');
  await page.locator('[data-catalogue="10"]').click();
  await page.locator('[data-region="bag"][data-index="0"]').click();
  assert.equal((await state()).inventoryData.slots[0].id, 10);
  assert.equal((await state()).inventoryData.slots[0].count, 64);
  await shot('creative-inventory');
  await page.keyboard.press('e');
  await playing();
  await page.keyboard.press('f');
  assert.equal((await state()).inventoryData.offhand[0].id, 10);
  check('Creative catalogue equips stacks and F swaps offhand');
  await quit();
  await page.locator('#new-world-button').click();
  await page.locator('#world-name').fill('Survival test');
  await page.locator('[data-mode="survival"]').click();
  await page.locator('#create-play-button').click();
  await playing();
  await fixture(3);
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.player.saturation = 0;
    g.player.hunger = 14;
    g.hurt(1, 'Test damage.');
  });
  assert.equal((await state()).player.health, 19);
  assert.equal(await page.locator('#health .heart').last().getAttribute('data-fill'), '1');
  check('Damage immediately displays a half-empty heart');
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.storage.slots[0].durability = 2;
    g.selected = 0;
    g.inventoryChanged();
  });
  await page.mouse.down({ button: 'left' });
  await page.waitForFunction(() => window.__wildsTest.block(0, 57, -3) === 0);
  await page.mouse.up({ button: 'left' });
  assert.equal((await state()).inventoryData.slots[0].durability, 1);
  assert.equal(await page.locator('#hotbar .durability').count(), 1);
  await page.evaluate(() => window.__wildsTest.pose(0.5, 56.02, -2.3));
  await page.waitForFunction(() => window.__wilds.inspect().inventory[8] >= 1);
  check('Pickaxe loses durability and mined blocks become collectible drops');
  await fixture(3);
  await page.mouse.down({ button: 'left' });
  await page.waitForFunction(() => window.__wildsTest.block(0, 57, -3) === 0);
  await page.mouse.up({ button: 'left' });
  assert.equal((await state()).inventoryData.slots[0], null);
  check('Exhausted pickaxe breaks and clears its slot');
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.storage.slots.fill(null);
    g.addItem(5, 9, false);
  });
  await page.keyboard.press('e');
  await page.waitForSelector('#inventory-modal:not(.hidden)');
  const slot = (name, index) =>
    page.locator('[data-region="' + name + '"][data-index="' + index + '"]');
  await slot('bag', 0).click({ button: 'right' });
  assert.equal((await state()).inventoryData.cursor.count, 5);
  assert.equal((await state()).inventoryData.slots[0].count, 4);
  await slot('grid', 3).click({ button: 'right' });
  assert.equal((await state()).inventoryData.grid[3].count, 1);
  await slot('bag', 1).click();
  await slot('output', 0).click();
  assert.equal((await state()).inventoryData.cursor.id, 7);
  assert.equal((await state()).inventoryData.cursor.count, 4);
  await slot('bag', 2).click();
  check('Right-click splitting and translated 2x2 log recipe work through the UI');
  await slot('bag', 2).click();
  const first = await slot('grid', 0).boundingBox();
  await page.mouse.move(first.x + 22, first.y + 22);
  await page.mouse.down();
  for (const i of [1, 3, 2]) {
    const box = await slot('grid', i).boundingBox();
    await page.mouse.move(box.x + 22, box.y + 22, { steps: 5 });
  }
  await page.mouse.up();
  assert.deepEqual(
    (await state()).inventoryData.grid.map((s) => s?.count),
    [1, 1, 1, 1]
  );
  await slot('output', 0).click({ modifiers: ['Shift'] });
  assert.equal((await state()).inventory[16], 1);
  check('Dragging distributes stacks and Shift-click crafts a table');
  await page.locator('#recipe-toggle').click();
  await shot('survival-inventory');
  assert.equal(await page.locator('[data-recipe="woodpick"]').count(), 0);
  check('Personal recipe book excludes recipes requiring a crafting table');
  await page.keyboard.press('e');
  await playing();
  await fixture(16);
  await clickButton('right');
  await page.waitForSelector('#inventory-modal:not(.hidden)');
  assert.equal(await page.locator('[data-region="grid"]').count(), 9);
  await page.evaluate(() => {
    window.__wildsTest.give(7, 3);
    window.__wildsTest.give(90, 2);
  });
  if (await page.locator('#recipe-book').isHidden()) await page.locator('#recipe-toggle').click();
  await page.locator('[data-recipe="woodpick"]').click();
  assert.equal((await state()).inventoryData.grid.filter(Boolean).length, 5);
  await slot('output', 0).click({ modifiers: ['Shift'] });
  assert.equal((await state()).inventory[100], 1);
  await shot('crafting-table');
  check('Right-click table opens 3x3 crafting and crafts a durable pickaxe');
  await page.keyboard.press('e');
  await playing();
  await fixture(23);
  await clickButton('right');
  await page.waitForSelector('#inventory-modal:not(.hidden)');
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.storage.furnace.slots[0] = { id: 95, count: 2 };
    g.storage.furnace.slots[1] = { id: 91, count: 1 };
    g.inventoryChanged();
  });
  await page.waitForFunction(
    () => window.__wildsTest.game.storage.furnace.slots[2]?.id === 92,
    undefined,
    { timeout: 300000 }
  );
  await shot('furnace');
  await slot('furnace', 2).click({ modifiers: ['Shift'] });
  assert.ok((await state()).inventory[92] >= 1);
  check('Furnace keeps ticking while open and smelts raw iron using coal');
  await page.keyboard.press('e');
  await playing();
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.player.hunger = 12;
    g.player.saturation = 0;
    g.storage.slots[8] = { id: 93, count: 2 };
    g.selected = 8;
    g.inventoryChanged();
    window.__wildsTest.setBlock(0, 57, -3, 0);
  });
  await page.mouse.down({ button: 'right' });
  await page.waitForFunction(() => window.__wilds.inspect().player.hunger === 14, undefined, {
    timeout: 120000,
  });
  await page.mouse.up({ button: 'right' });
  assert.equal((await state()).player.hunger, 14);
  assert.equal((await state()).inventoryData.slots[8].count, 1);
  check('Eating takes time and consumes one berry stack item');
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.damageCooldown = 0;
    g.player.health = 20;
    window.__wildsTest.pose(0.5, 62, 0.5);
  });
  await page.waitForFunction(() => window.__wilds.inspect().player.grounded);
  assert.ok((await state()).player.health < 20);
  check('Falls above three blocks cause damage');
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.storage.slots[0] = { id: 101, count: 1, durability: 42 };
    g.storage.armor[1] = { id: 111, count: 1, durability: 222 };
    g.inventoryChanged();
  });
  await shot('survival-gameplay');
  const saved = await state();
  await quit();
  await page.locator('#continue-button').click();
  await playing();
  assert.equal((await state()).inventoryData.slots[0].durability, 42);
  assert.equal((await state()).inventoryData.armor[1].durability, 222);
  assert.equal((await state()).player.health, saved.player.health);
  assert.ok(await page.evaluate(() => window.__wildsTest.game.furnaces.size > 0));
  check('Reload preserves health, stacks, tool durability, armor and furnace contents');
  await pause();
  await page.locator('#pause-settings').click();
  await page.locator('#show-fps').check();
  await page.locator('#shadows').check();
  assert.equal(await page.evaluate(() => window.__wildsTest.game.renderer.shadowMap.enabled), true);
  await page.locator('#shadows').uncheck();
  assert.equal(
    await page.evaluate(() => window.__wildsTest.game.renderer.shadowMap.enabled),
    false
  );
  check('Settings still apply correctly');
  await page.keyboard.press('Escape');
  await page.locator('#resume-button').click();
  await playing();
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.damageCooldown = 0;
    g.hurt(100, 'Test death.');
  });
  await page.waitForSelector('#death-modal:not(.hidden)');
  assert.equal(Object.keys((await state()).inventory).length, 0);
  assert.ok(await page.evaluate(() => window.__wildsTest.game.drops.list.length > 0));
  await page.locator('#death-respawn').click();
  await playing();
  assert.equal((await state()).player.health, 20);
  check('Death drops inventory and respawn restores health');
  assert.deepEqual(errors, []);
  assert.deepEqual(failedRequests, []);
  check('No browser errors or failed requests');
} catch (error) {
  failure = error;
  console.error(error.stack);
  await shot('test-failure');
  const lastState = await state();
  delete lastState.edits;
  console.error(JSON.stringify(lastState, null, 2));
} finally {
  await writeFile(
    path.join(ROOT, 'artifacts', 'test-report.json'),
    JSON.stringify(
      {
        checks,
        errors,
        warnings,
        failedRequests,
        passed: !failure,
        failure: failure?.message,
        saveDir,
      },
      null,
      2
    )
  );
  await browser.close();
  server.close();
}
if (failure) process.exitCode = 1;
