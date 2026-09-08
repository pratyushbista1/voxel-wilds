import { spawn } from 'node:child_process';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { ROOT } from '../server.mjs';

if (process.platform !== 'win32') throw Error('Build the Windows releases on Windows.');
const env = {
  ...process.env,
  ELECTRON_CACHE: path.join(ROOT, '.cache', 'electron'),
  ELECTRON_BUILDER_CACHE: path.join(ROOT, '.cache', 'electron-builder'),
  CSC_IDENTITY_AUTO_DISCOVERY: 'false',
  TEMP: path.join(ROOT, '.cache', 'build-temp'),
  TMP: path.join(ROOT, '.cache', 'build-temp'),
};
await mkdir(env.TEMP, { recursive: true });
const child = spawn(
  process.execPath,
  [
    path.join(ROOT, 'node_modules', 'electron-builder', 'cli.js'),
    '--win',
    'nsis',
    'portable',
    '--x64',
    '--publish',
    'never',
  ],
  { cwd: ROOT, env, stdio: 'inherit', windowsHide: true }
);
child.on('error', (error) => {
  console.error(error.message);
  process.exitCode = 1;
});
child.on('exit', async (code) => {
  if (code) {
    process.exitCode = code;
    return;
  }
  try {
    await import('./finish-release.mjs');
  } catch (error) {
    console.error(error);
    process.exitCode = 1;
  }
});
