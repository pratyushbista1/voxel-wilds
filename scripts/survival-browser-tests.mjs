import assert from 'node:assert/strict';

export async function survivalChecks({ page, check, shot, playing, pause, quit, state }) {
  const slot = (name, index) => page.locator(`[data-region="${name}"][data-index="${index}"]`);
  const simulate = async (seconds) => {
    const until = (await state()).playTime + seconds;
    await page.waitForFunction((until) => window.__wildsTest.game.playTime >= until, until);
  };
  const aim = async (x, y, z) => {
    await page.evaluate(
      ([x, y, z]) => {
        const g = window.__wildsTest.game,
          p = g.player;
        p.yaw = Math.atan2(p.x - x, p.z - z);
        p.pitch = Math.atan2(y - p.y - p.eyeHeight, Math.hypot(x - p.x, z - p.z));
        g.actionCooldown = 0;
      },
      [x, y, z]
    );
    await simulate(0.1);
  };
  const right = async () => {
    await page.mouse.down({ button: 'right' });
    await simulate(0.1);
    await page.mouse.up({ button: 'right' });
  };
  const kill = async () => {
    await page.evaluate(() => {
      const t = window.__wildsTest,
        g = t.game;
      t.pose(9.5, 56.02, 5.5);
      g.damageCooldown = 0;
      g.hurt(100, 'Respawn test.');
    });
    await page.waitForSelector('#death-modal:not(.hidden)');
    assert.equal(await page.locator('#death-respawn, #respawn-button').count(), 0);
    await page.waitForFunction(() => window.__wildsTest.game.player.health === 20);
    await playing();
  };
  await page.evaluate(() => {
    const t = window.__wildsTest,
      g = t.game;
    for (const m of [...g.creatures.list]) g.creatures.remove(m);
    g.creatures.spawnTimer = -1000;
    for (let x = -8; x <= 12; x++)
      for (let z = -12; z <= 8; z++) {
        t.setBlock(x, 55, z, 3);
        for (let y = 56; y <= 64; y++) t.setBlock(x, y, z, 0);
      }
    t.pose(0.5, 56.02, 3.5);
    g.time = 600;
    g.storage.slots.fill(null);
    g.storage.armor.fill(null);
    g.storage.offhand.fill(null);
    g.storage.slots[0] = { id: 25, count: 1 };
    g.selected = 0;
    g.inventoryChanged();
  });
  await aim(0.5, 55.99, 0.5);
  await right();
  await page.waitForFunction(() => window.__wildsTest.block(0, 56, 0) === 25);
  assert.equal(await page.evaluate(() => window.__wildsTest.block(0, 56, -1)), 26);
  assert.equal((await state()).inventoryData.slots[0], null);
  await aim(0.5, 56.3, 0.5);
  await right();
  assert.deepEqual((await state()).bedSpawn, {
    x: 0,
    y: 56,
    z: 0,
    facing: 0,
    dimension: 'overworld',
  });
  assert.equal(await page.evaluate(() => window.__wildsTest.game.systems.sleepTimer), 0);
  check('Right-click places both bed halves, consumes one bed and sets spawn during the day');

  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.time = 1000;
    g.creatures.spawn('zombie', 3.5, 0.5, 56);
  });
  await aim(0.5, 56.3, 0.5);
  await right();
  assert.equal(await page.evaluate(() => window.__wildsTest.game.systems.sleepTimer), 0);
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    for (const m of [...g.creatures.list]) g.creatures.remove(m);
  });
  await aim(0.5, 56.3, 0.5);
  await right();
  await page.waitForSelector('#sleep-overlay:not(.hidden)');
  await shot('sleeping');
  await page.waitForFunction(() => window.__wildsTest.game.time >= 1500);
  assert.ok(await page.locator('#sleep-overlay').isHidden());
  check('Nearby monsters prevent sleeping; a safe bed skips the night to dawn');

  await kill();
  assert.ok(Math.hypot((await state()).player.x - 0.5, (await state()).player.z - 0.5) < 2);
  check('Death automatically returns to the bed with full health and no respawn controls');
  await page.evaluate(async () => {
    const t = window.__wildsTest,
      g = t.game;
    t.pose(9.5, 56.02, 5.5);
    g.damageCooldown = 0;
    g.hurt(100, 'Saved death test.');
    await g.save(true);
  });
  await page.reload();
  await page.waitForSelector('#menu:not(.hidden)');
  await page.locator('#continue-button').click();
  await page.waitForFunction(
    () => window.__wilds.inspect().active && window.__wilds.inspect().player.health === 20
  );
  await playing();
  assert.ok(Math.hypot((await state()).player.x - 0.5, (await state()).player.z - 0.5) < 2);
  check('Reloading a world saved during death still automatically respawns at its bed');

  await page.evaluate(() => {
    const t = window.__wildsTest;
    for (let x = -1; x <= 1; x++)
      for (let z = -2; z <= 1; z++)
        for (let y = 56; y <= 59; y++)
          if (!(x === 0 && (z === 0 || z === -1) && y === 56)) t.setBlock(x, y, z, 3);
  });
  await kill();
  assert.ok(Math.hypot((await state()).player.x - 0.5, (await state()).player.z - 0.5) > 3);
  await page.evaluate(() => {
    const t = window.__wildsTest;
    for (let x = -1; x <= 1; x++)
      for (let z = -2; z <= 1; z++) for (let y = 56; y <= 59; y++) t.setBlock(x, y, z, 0);
  });
  await kill();
  assert.ok(Math.hypot((await state()).player.x - 0.5, (await state()).player.z - 0.5) > 3);
  check('Blocked or missing beds fall back to a safe world spawn');

  await page.evaluate(() => {
    const t = window.__wildsTest,
      g = t.game;
    for (const m of [...g.creatures.list]) g.creatures.remove(m);
    g.time = 1000;
    g.creatures.spawnTimer = -1000;
    t.pose(0.5, 56.02, 3.5);
    g.player.hunger = 12;
    g.damageCooldown = 0;
    const m = g.creatures.spawn('zombie', 0.5, 2, 56);
    if (!m) throw Error('Zombie fixture could not spawn.');
  });
  await page.waitForFunction(() => window.__wildsTest.game.player.health < 20);
  assert.ok(Number(await page.locator('#health .heart').last().getAttribute('data-fill')) < 2);
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    for (const m of [...g.creatures.list]) g.creatures.remove(m);
    g.player.health = 20;
    g.storage.slots[0] = { id: 103, count: 1, durability: 250 };
    g.selected = 0;
    g.inventoryChanged();
    const cow = g.creatures.spawn('cow', 0.5, 1.2, 56);
    cow.health = 6;
    cow.walking = false;
    cow.timer = 100;
  });
  await aim(0.5, 56.8, 1.2);
  await page.mouse.down();
  await page.waitForFunction(
    () => !window.__wildsTest.game.creatures.list.some((m) => m.kind === 'cow')
  );
  await page.mouse.up();
  assert.ok(
    await page.evaluate(() => window.__wildsTest.game.drops.list.some((d) => d.stack.id === 122))
  );
  assert.equal((await state()).inventoryData.slots[0].durability, 249);
  await page.evaluate(() => window.__wildsTest.pose(0.5, 56.02, 1.2));
  await page.waitForFunction(() => window.__wilds.inspect().inventory[122] >= 1);
  check('Zombies inflict visible damage; sword attacks kill animals and drop collectible meat');

  await page.evaluate(() => {
    const t = window.__wildsTest,
      g = t.game;
    g.time = 600;
    t.setBlock(0, 56, -1, 23);
    t.pose(0.5, 56.02, 2.5);
  });
  await aim(0.5, 56.5, -0.5);
  await right();
  await page.waitForSelector('#inventory-modal:not(.hidden)');
  await page.evaluate(() => {
    const g = window.__wildsTest.game,
      i = g.storage.slots.findIndex((s) => s?.id === 122);
    g.storage.furnace.slots[0] = g.storage.slots[i];
    g.storage.slots[i] = null;
    g.storage.furnace.slots[1] = { id: 91, count: 1 };
    g.inventoryChanged();
  });
  await page.waitForFunction(
    () => window.__wildsTest.game.storage.furnace.slots[2]?.id === 123,
    undefined,
    { timeout: 300000 }
  );
  await slot('furnace', 2).click({ modifiers: ['Shift'] });
  await page.keyboard.press('e');
  await playing();
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.selected = g.storage.slots.findIndex((s) => s?.id === 123);
    g.player.hunger = 10;
    g.inventoryChanged();
  });
  await aim(0.5, 60, 0.5);
  await page.mouse.down({ button: 'right' });
  await page.waitForFunction(() => window.__wildsTest.game.player.hunger === 18);
  await page.mouse.up({ button: 'right' });
  check('Animal meat cooks in a furnace and eating steak restores hunger');

  await page.evaluate(() => {
    const t = window.__wildsTest,
      g = t.game;
    t.setBlock(0, 56, -1, 41);
    g.selected = 8;
    g.storage.slots[8] = null;
    g.inventoryChanged();
  });
  await aim(0.5, 56.5, -0.5);
  await right();
  await page.waitForSelector('#inventory-modal:not(.hidden)');
  assert.equal(await page.locator('[data-region="chest"]').count(), 27);
  await slot('bag', 0).click({ modifiers: ['Shift'] });
  assert.equal(await page.evaluate(() => window.__wildsTest.game.storage.chest[0]?.id), 103);
  await shot('chest-inventory');
  await page.keyboard.press('e');
  await playing();
  await quit();
  await page.locator('#continue-button').click();
  await playing();
  await aim(0.5, 56.5, -0.5);
  await right();
  await page.waitForSelector('#inventory-modal:not(.hidden)');
  assert.equal(
    await page.evaluate(() => window.__wildsTest.game.storage.chest[0]?.durability),
    249
  );
  await slot('chest', 0).click({ modifiers: ['Shift'] });
  await page.keyboard.press('e');
  await playing();
  check('Chest UI has 27 slots, supports transfers and preserves equipment through reload');

  await pause();
  await page.locator('#pause-settings').click();
  await page.locator('#max-fps').selectOption('0');
  await page.locator('#shadow-size').selectOption('1024');
  await page.locator('#particles').selectOption('60');
  await page.locator('#clouds').uncheck();
  await page.locator('#view-bobbing').uncheck();
  await page.locator('#brightness').fill('120');
  await page.locator('#render-scale').fill('50');
  assert.deepEqual(
    await page.evaluate(() => {
      const g = window.__wildsTest.game;
      return [
        g.settings.maxFps,
        g.renderer.getPixelRatio(),
        g.settings.brightness,
        g.particles.maxCount,
        g.atmosphere.cloudGroup.visible,
      ];
    }),
    [0, 0.5, 120, 60, false]
  );
  await shot('graphics-settings');
  await page.keyboard.press('Escape');
  await page.locator('#resume-button').click();
  await playing();
  check('Graphics menu applies Unlimited FPS, render scale, brightness and quality controls');

  await page.evaluate(() => {
    const t = window.__wildsTest,
      g = t.game;
    t.setBlock(0, 56, -1, 0);
    for (let u = 0; u < 4; u++)
      for (let v = 0; v < 5; v++)
        t.setBlock(-1 + u, 55 + v, -5, u === 0 || u === 3 || v === 0 || v === 4 ? 33 : 0);
    t.pose(0.5, 56.02, -1.5);
    g.storage.slots[8] = { id: 130, count: 1, durability: 64 };
    g.selected = 8;
    g.player.health = 18;
    g.player.hunger = 12;
    g.inventoryChanged();
  });
  await aim(0.5, 55.99, -4.5);
  await right();
  await page.waitForFunction(() => window.__wildsTest.block(0, 56, -5) === 34);
  assert.equal((await state()).inventoryData.slots[8].durability, 63);
  await shot('nether-portal');
  await page.evaluate(() => window.__wildsTest.pose(0.5, 56.02, -4.5));
  await page.waitForFunction(
    () =>
      window.__wilds.inspect().dimension === 'nether' && !window.__wildsTest.game.systems.transition
  );
  await playing();
  assert.equal((await state()).player.health, 18);
  assert.equal((await state()).inventoryData.slots[8].durability, 63);
  check(
    'Flint and steel ignites obsidian; standing inside travels to the Nether with inventory intact'
  );

  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.world.set(5, 50, 5, 40);
    g.creatures.spawnTimer = -1000;
  });
  await quit();
  await page.locator('#continue-button').click();
  await playing();
  assert.equal((await state()).dimension, 'nether');
  assert.equal(await page.evaluate(() => window.__wildsTest.block(5, 50, 5)), 40);
  assert.equal(await page.evaluate(() => window.__wildsTest.game.settings.maxFps), 0);
  await page.evaluate(() => {
    const t = window.__wildsTest,
      g = t.game;
    g.mode = 'creative';
    t.pose(48.5, 40, 82, 0, -0.2);
    g.player.flying = true;
    g.creatures.scanTimer = 4;
  });
  await simulate(0.2);
  assert.equal(await page.evaluate(() => window.__wildsTest.block(48, 35, 63)), 47);
  assert.ok(
    await page.evaluate(() =>
      window.__wildsTest.game.creatures.list.some((m) => m.kind === 'wither_skeleton')
    )
  );
  await shot('nether-fortress');
  await page.evaluate(() => {
    const t = window.__wildsTest,
      g = t.game;
    t.pose(108.5, 43, 135, 0, -0.2);
    g.player.flying = true;
    g.creatures.scanTimer = 4;
  });
  await simulate(0.2);
  assert.equal(await page.evaluate(() => window.__wildsTest.block(108, 34, 108)), 41);
  assert.ok(
    await page.evaluate(() =>
      window.__wildsTest.game.creatures.list.some((m) => m.kind === 'brute')
    )
  );
  await shot('nether-bastion');
  const loot = await page.evaluate(() => {
    const g = window.__wildsTest.game;
    return g.systems.chest({ x: 108, y: 34, z: 108 }).filter(Boolean);
  });
  assert.ok(loot.some((s) => s.id === 131));
  check('Nether saves reload; fortresses, bastions, guards and treasure generate in-world');

  await page.evaluate(() => {
    const t = window.__wildsTest,
      g = t.game,
      p = g.portalLinks[0].nether;
    g.mode = 'survival';
    g.player.flying = false;
    g.systems.portalCooldown = 0;
    t.pose(p.x + p.dx + 0.5, p.y + 1.02, p.z + p.dz + 0.5);
  });
  await page.waitForFunction(
    () =>
      window.__wilds.inspect().dimension === 'overworld' &&
      !window.__wildsTest.game.systems.transition
  );
  await playing();
  assert.equal(await page.evaluate(() => window.__wildsTest.block(0, 56, -5)), 34);
  assert.ok(await page.evaluate(() => window.__wildsTest.game.furnaces.size > 0));
  check('A paired portal returns to the original Overworld and retains its blocks and stations');

  await page.evaluate(() => {
    const t = window.__wildsTest,
      g = t.game,
      p = g.portalLinks[0].overworld;
    g.systems.portalCooldown = 0;
    t.pose(p.x + p.dx + 0.5, p.y + 1.02, p.z + p.dz + 0.5);
  });
  await page.waitForFunction(
    () =>
      window.__wilds.inspect().dimension === 'nether' && !window.__wildsTest.game.systems.transition
  );
  await playing();
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.damageCooldown = 0;
    g.hurt(100, 'Nether death test.');
  });
  await page.waitForFunction(
    () =>
      window.__wilds.inspect().dimension === 'overworld' &&
      window.__wilds.inspect().player.health === 20 &&
      !window.__wildsTest.game.systems.transition
  );
  await playing();
  check('Dying in the Nether automatically respawns in the Overworld without buttons');
}
