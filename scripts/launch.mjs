import { spawn } from 'node:child_process';
import { openSync, closeSync, mkdirSync } from 'node:fs';
import net from 'node:net';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
async function health(port) {
  try {
    const response = await fetch(`http://127.0.0.1:${port}/api/health`, {
      signal: AbortSignal.timeout(800),
    });
    return await response.json();
  } catch {
    return null;
  }
}
async function available(port) {
  return new Promise((resolve) => {
    const probe = net.createServer();
    probe.once('error', () => resolve(false));
    probe.listen(port, '127.0.0.1', () => probe.close(() => resolve(true)));
  });
}
let port = null,
  running = false,
  freePort = null;
for (let candidate = 4173; candidate < 4184; candidate++) {
  const h = await health(candidate);
  if (h?.app === 'voxel-wilds' && h.root === root && h.version === '1.1.0') {
    port = candidate;
    running = true;
    break;
  }
  if (freePort === null && (await available(candidate))) freePort = candidate;
}
port ??= freePort;
if (port === null) {
  console.error('Ports 4173-4183 are busy. Close the other local servers and try again.');
  process.exit(1);
}
if (!running) {
  mkdirSync(path.join(root, 'logs'), { recursive: true });
  const log = openSync(path.join(root, 'logs', 'server.log'), 'a');
  const child = spawn(process.execPath, [path.join(root, 'server.mjs')], {
    cwd: root,
    detached: true,
    windowsHide: true,
    stdio: ['ignore', log, log],
    env: { ...process.env, GAME_PORT: String(port) },
  });
  let launchError = null;
  child.on('error', (error) => {
    launchError = error;
  });
  child.unref();
  closeSync(log);
  for (let n = 0; n < 50 && !launchError; n++) {
    const h = await health(port);
    if (h?.app === 'voxel-wilds' && h.root === root) {
      running = true;
      break;
    }
    await new Promise((r) => setTimeout(r, 200));
  }
}
if (!running) {
  console.error('Could not start the game. See logs/server.log.');
  process.exitCode = 1;
} else {
  const url = `http://127.0.0.1:${port}`;
  console.log(`Voxel Wilds is ready at ${url}`);
  if (!process.argv.includes('--no-open')) {
    const opener = spawn('explorer.exe', [url], { windowsHide: true, stdio: 'ignore' });
    opener.on('error', () => console.log('Open the address above in your browser.'));
    opener.unref();
  }
}
