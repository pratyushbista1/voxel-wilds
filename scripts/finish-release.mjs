import { createHash } from 'node:crypto';
import { readFile, readdir, writeFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import path from 'node:path';
import { ROOT } from '../server.mjs';

const { version } = JSON.parse(await readFile(path.join(ROOT, 'package.json'), 'utf8'));
const cache = path.join(ROOT, '.cache', 'electron-builder');
let compiler;
async function findCompiler(dir, depth = 0) {
  if (depth > 4 || compiler) return;
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const absolute = path.join(dir, entry.name);
    if (entry.isFile() && entry.name === 'makensis.exe') {
      compiler = absolute;
      return;
    }
    if (entry.isDirectory() && (depth > 0 || entry.name.startsWith('nsis-')))
      await findCompiler(absolute, depth + 1);
  }
}
await findCompiler(cache);
if (!compiler) throw Error('NSIS compiler was not found. Run the Windows build first.');
await new Promise((resolve, reject) => {
  const child = spawn(
    compiler,
    [
      '/V2',
      `/DPROJECT_DIR=${ROOT}`,
      `/DOUTPUT=${path.join(ROOT, 'release', `Voxel-Wilds-Uninstall-${version}.exe`)}`,
      path.join(ROOT, 'build', 'uninstall.nsi'),
    ],
    { cwd: ROOT, stdio: 'inherit', windowsHide: true }
  );
  child.on('error', reject);
  child.on('exit', (code) =>
    code === 0 ? resolve() : reject(Error('Uninstaller launcher build failed.'))
  );
});
const names = (await readdir(path.join(ROOT, 'release')))
  .filter((name) => /^Voxel-Wilds-(Setup|Portable|Uninstall)-[\d.]+\.exe$/.test(name))
  .sort();
const hashes = [];
for (const name of names) {
  const bytes = await readFile(path.join(ROOT, 'release', name));
  hashes.push(createHash('sha256').update(bytes).digest('hex') + '  ' + name);
}
await writeFile(path.join(ROOT, 'release', 'SHA256SUMS.txt'), hashes.join('\n') + '\n');
await writeFile(
  path.join(ROOT, 'release', 'README.txt'),
  `Voxel Wilds ${version}\n\nPortable: run Voxel-Wilds-Portable-${version}.exe in a writable folder.\nSetup: run Voxel-Wilds-Setup-${version}.exe to install shortcuts and an uninstaller.\nUninstall: run Voxel-Wilds-Uninstall-${version}.exe to open the installed uninstaller.\n\nShare the portable EXE or setup EXE with friends. No Node.js, browser or Blender installation is needed.\nThese are unsigned 64-bit Windows builds. Windows may display a publisher warning.\nSHA256SUMS.txt contains checksums for the executables.\n\nWASD moves, Space jumps, Ctrl sprints, Shift sneaks, E opens inventory, Esc pauses.\nLeft click mines. Right click places or opens a station. Hold right click to eat.\nF swaps offhand. Q drops an item. Double-tap Space or G toggles Creative flight.\nCreative has no damage or tool wear. Use Survival for health and durability.\n\nUninstalling the installed game keeps your worlds in %APPDATA%\\Voxel Wilds\\saves.\nPortable worlds are in userdata\\saves beside the EXE. Keep that folder to retain worlds.\nThe portable version does not register an installed application. Remove its EXE to stop using it.\n`
);
console.log('Release files, uninstall launcher and SHA-256 checksums are ready.');
