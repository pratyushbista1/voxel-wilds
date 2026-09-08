import { chromium } from 'playwright';
import { mkdir, mkdtemp, copyFile, readFile, readdir, writeFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import net from 'node:net';
import path from 'node:path';
import assert from 'node:assert/strict';
import { ROOT } from '../server.mjs';

await mkdir(path.join(ROOT, '.cache'), { recursive: true });
const testDir = await mkdtemp(path.join(ROOT, '.cache', 'portable-test-'));
const { version } = JSON.parse(await readFile(path.join(ROOT, 'package.json'), 'utf8'));
const executable = path.join(testDir, 'Voxel Wilds Portable.exe');
await copyFile(
  path.join(ROOT, 'release', version, `Voxel-Wilds-Portable-${version}.exe`),
  executable
);
const probe = net.createServer();
await new Promise((resolve) => probe.listen(0, '127.0.0.1', resolve));
const port = probe.address().port;
await new Promise((resolve) => probe.close(resolve));
const env = {
  ...process.env,
  VOXEL_TEST: '1',
  TEMP: path.join(ROOT, '.cache'),
  TMP: path.join(ROOT, '.cache'),
};
delete env.VOXEL_DATA_DIR;
delete env.VOXEL_SAVE_DIR;
const child = spawn(executable, [`--remote-debugging-port=${port}`], {
  cwd: testDir,
  env,
  windowsHide: true,
  stdio: 'ignore',
});
const exited = new Promise((resolve) => child.once('exit', resolve));
let connected = false,
  browser;
try {
  const deadline = Date.now() + 60000;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`http://127.0.0.1:${port}/json/version`, {
        signal: AbortSignal.timeout(800),
      });
      if (response.ok) {
        connected = true;
        break;
      }
    } catch {}
    await new Promise((resolve) => setTimeout(resolve, 300));
  }
  assert.ok(connected, 'portable EXE starts its embedded runtime');
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${port}`);
  const page = browser.contexts()[0].pages()[0];
  await page.waitForSelector('#menu:not(.hidden)', { timeout: 60000 });
  assert.equal(await page.evaluate(() => window.__wilds.inspect().models.length), 19);
  assert.equal(await page.locator('#death-respawn, #respawn-button').count(), 0);
  await page.evaluate(() =>
    window.__wildsTest.game.create('Portable test', 'portable-seed', 'survival')
  );
  await page.evaluate(() => {
    const g = window.__wildsTest.game;
    g.storage.slots[0].durability = 29;
    g.player.health = 17;
    g.inventoryChanged();
  });
  await page.screenshot({ path: path.join(ROOT, 'artifacts', 'portable-desktop.png') });
  await page.evaluate(() => window.desktop.quit());
  await Promise.race([
    exited,
    new Promise((_, reject) =>
      setTimeout(() => reject(Error('Portable app did not close.')), 20000)
    ),
  ]);
  const saveDir = path.join(testDir, 'userdata', 'saves');
  const name = (await readdir(saveDir)).find((n) => n.endsWith('.json'));
  const saved = JSON.parse(await readFile(path.join(saveDir, name), 'utf8'));
  assert.equal(saved.version, 3);
  assert.equal(saved.inventoryData.slots[0].durability, 29);
  assert.equal(saved.player.health, 17);
  await writeFile(
    path.join(ROOT, 'artifacts', 'portable-test-report.json'),
    JSON.stringify({ passed: true, executable, saveDir, version }, null, 2)
  );
  console.log(
    'PASS: Portable EXE launches its embedded runtime, loads all models and saves beside the EXE on close.'
  );
} finally {
  await browser?.close().catch(() => {});
  if (child.exitCode === null) child.kill();
}
