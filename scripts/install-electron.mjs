import { spawn } from 'node:child_process';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { ROOT } from '../server.mjs';

const cache = path.join(ROOT, '.cache', 'electron');
await mkdir(cache, { recursive: true });
const child = spawn(process.execPath, [path.join(ROOT, 'node_modules', 'electron', 'install.js')], {
  cwd: ROOT,
  stdio: 'inherit',
  windowsHide: true,
  env: {
    ...process.env,
    ELECTRON_CACHE: cache,
    electron_config_cache: cache,
    TEMP: path.join(ROOT, '.cache'),
    TMP: path.join(ROOT, '.cache'),
  },
});
child.on('error', (error) => {
  console.error(error.message);
  process.exitCode = 1;
});
child.on('exit', (code) => {
  process.exitCode = code || 0;
});
