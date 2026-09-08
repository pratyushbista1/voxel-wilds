import { _electron as electron } from 'playwright';
import { mkdtemp, mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import path from 'node:path';
import { ROOT } from '../server.mjs';

await mkdir(path.join(ROOT, '.cache'), { recursive: true });
await mkdir(path.join(ROOT, 'artifacts'), { recursive: true });
const dataDir = await mkdtemp(path.join(ROOT, '.cache', 'desktop-test-'));
const packaged = process.env.VOXEL_PACKAGED_EXE;
const executablePath =
  packaged || path.join(ROOT, 'node_modules', 'electron', 'dist', 'electron.exe');
const desktop = await electron.launch({
  executablePath,
  args: packaged ? [] : [ROOT],
  timeout: 60000,
  env: {
    ...process.env,
    VOXEL_DATA_DIR: dataDir,
    VOXEL_TEST: '1',
    ELECTRON_ENABLE_SECURITY_WARNINGS: '1',
    TEMP: path.join(ROOT, '.cache'),
    TMP: path.join(ROOT, '.cache'),
  },
});
const page = await desktop.firstWindow();
const errors = [],
  warnings = [];
page.on('pageerror', (e) => errors.push(e.message));
page.on('console', (m) => {
  if (m.type() === 'warning') warnings.push(m.text());
  if (m.type() === 'error') {
    errors.push(m.text());
    console.error(m.text());
  }
});
page.on('response', (r) => {
  if (r.status() >= 400) {
    errors.push(`${r.status()} ${r.url()}`);
    console.error(r.status(), r.url());
  }
});
let finished = false;
try {
  await page.waitForURL(/http:\/\/127\.0\.0\.1:\d+\//);
  await page.waitForSelector('#menu:not(.hidden)', { timeout: 90000 });
  assert.equal(await page.evaluate(() => typeof window.require), 'undefined');
  assert.equal(await page.evaluate(() => typeof window.process), 'undefined');
  assert.equal(await page.evaluate(() => typeof window.desktop.quit), 'function');
  assert.equal(await page.evaluate(() => window.__wilds.inspect().models.length), 19);
  assert.equal(await page.locator('#death-respawn, #respawn-button').count(), 0);
  assert.equal(
    await desktop.evaluate(({ app }) => app.commandLine.hasSwitch('disable-frame-rate-limit')),
    true
  );
  const preferences = await desktop.evaluate(({ BrowserWindow }) => {
    const p = BrowserWindow.getAllWindows()[0].webContents.getLastWebPreferences();
    return {
      sandbox: p.sandbox,
      nodeIntegration: p.nodeIntegration,
      contextIsolation: p.contextIsolation,
    };
  });
  assert.deepEqual(preferences, { sandbox: true, nodeIntegration: false, contextIsolation: true });
  await page.evaluate(() =>
    window.__wildsTest.game.create('Desktop test', 'desktop-seed', 'survival')
  );
  await page.waitForFunction(() => window.__wilds.inspect().active);
  await page.evaluate(() => {
    const t = window.__wildsTest,
      g = t.game;
    for (let x = 18; x <= 23; x++)
      for (let z = 17; z <= 22; z++) {
        t.setBlock(x, 54, z, 3);
        for (let y = 55; y <= 59; y++) t.setBlock(x, y, z, 0);
      }
    t.setBlock(20, 55, 20, 25);
    t.setBlock(20, 55, 19, 26);
    g.bedSpawn = { x: 20, y: 55, z: 20, facing: 0, dimension: 'overworld' };
    g.paused = false;
    g.damageCooldown = 0;
    g.hurt(100, 'Desktop death test.');
  });
  await page.waitForFunction(() => window.__wildsTest.game.player.health === 20, undefined, {
    timeout: 20000,
  });
  assert.ok(
    await page.evaluate(
      () =>
        Math.hypot(
          window.__wildsTest.game.player.x - 20.5,
          window.__wildsTest.game.player.z - 20.5
        ) < 2
    )
  );
  console.log('PASS: Native death automatically respawns at a bed without either button.');
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.player.health = 13;
    g.storage.slots[0] = { id: 100, count: 1, durability: 23 };
    g.world.set(0, 50, 0, 10);
    g.inventoryChanged();
  });
  await page.screenshot({
    path: path.join(ROOT, 'artifacts', packaged ? 'packaged-desktop.png' : 'desktop.png'),
  });
  const closed = desktop.waitForEvent('close', { timeout: 15000 });
  await desktop.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].close());
  await closed;
  finished = true;
  const names = (await readdir(path.join(dataDir, 'saves'))).filter((n) => n.endsWith('.json'));
  assert.equal(names.length, 1);
  const saved = JSON.parse(await readFile(path.join(dataDir, 'saves', names[0]), 'utf8'));
  assert.equal(saved.version, 3);
  assert.equal(saved.player.health, 13);
  assert.equal(saved.inventoryData.slots[0].durability, 23);
  assert.ok(saved.edits.some(([key, id]) => key === '0,50,0' && id === 10));
  assert.deepEqual(errors, []);
  const securityWarnings = warnings.filter((w) =>
    /Security Warning|Content-Security-Policy/.test(w)
  );
  assert.deepEqual(securityWarnings, []);
  console.log(
    'PASS: ' +
      (packaged ? 'Packaged EXE' : 'Desktop app') +
      ' loads, isolates Node access, renders models and saves on window close.'
  );
  console.log('Test saves: ' + dataDir);
  await writeFile(
    process.env.VOXEL_TEST_REPORT || path.join(ROOT, 'artifacts', 'desktop-test-report.json'),
    JSON.stringify(
      {
        passed: true,
        executablePath,
        dataDir,
        saveFile: path.join(dataDir, 'saves', names[0]),
        errors,
        securityWarnings,
      },
      null,
      2
    )
  );
} finally {
  if (!finished) {
    console.error('Desktop test failed:', errors, warnings);
    await desktop.evaluate(({ app }) => app.exit()).catch(() => {});
  }
}
