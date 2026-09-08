import http from 'node:http';
import { readFile, writeFile, rename, copyFile, readdir, mkdir, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';
import { ITEMS, BLOCKS } from './src/blocks.js';
import { createHash } from 'node:crypto';

export const ROOT = path.dirname(fileURLToPath(import.meta.url));
const html = (await readFile(path.join(ROOT, 'index.html'), 'utf8')).replace(/\r\n/g, '\n');
const importMap = html.match(/<script type="importmap">([\s\S]*?)<\/script>/)?.[1] || '';
const importMapHash = createHash('sha256').update(importMap).digest('base64');
const contentPolicy = `default-src 'self'; script-src 'self' 'sha256-${importMapHash}'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'`;
const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.glb': 'model/gltf-binary',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
};
const validId = (id) => /^[a-zA-Z0-9_-]{1,64}$/.test(id);
export function validateSave(data, id) {
  if (!data || ![1, 2, 3].includes(data.version) || data.id !== id || !validId(id))
    throw Error('Invalid world identity.');
  if (typeof data.name !== 'string' || !data.name.trim() || data.name.length > 48)
    throw Error('World name must be 1-48 characters.');
  if (
    typeof data.seed !== 'string' ||
    data.seed.length > 64 ||
    !['creative', 'survival'].includes(data.mode)
  )
    throw Error('Invalid seed or game mode.');
  if (!Array.isArray(data.edits) || data.edits.length > 150000)
    throw Error('Invalid block changes.');
  const validBlocks = new Set(Object.keys(BLOCKS).map(Number));
  for (const pair of data.edits) {
    if (
      !Array.isArray(pair) ||
      pair.length !== 2 ||
      typeof pair[0] !== 'string' ||
      !validBlocks.has(pair[1])
    )
      throw Error('Invalid block.');
    const coords = pair[0].split(',').map(Number);
    if (
      coords.length !== 3 ||
      !coords.every(Number.isInteger) ||
      Math.abs(coords[0]) > 1000000 ||
      Math.abs(coords[2]) > 1000000 ||
      coords[1] <= 0 ||
      coords[1] >= 72
    )
      throw Error('Invalid block position.');
  }
  if (
    !data.player ||
    !['x', 'y', 'z', 'yaw', 'pitch', 'health', 'hunger'].every((k) =>
      Number.isFinite(data.player[k])
    ) ||
    Math.abs(data.player.x) > 1000000 ||
    Math.abs(data.player.z) > 1000000 ||
    Math.abs(data.player.y) > 10000
  )
    throw Error('Invalid player state.');
  if (
    !Number.isFinite(data.time) ||
    data.time < 0 ||
    !Number.isFinite(data.playTime) ||
    data.playTime < 0
  )
    throw Error('Invalid world time.');
  if (
    !Array.isArray(data.hotbar) ||
    data.hotbar.length !== 9 ||
    !data.hotbar.every((n) => Number.isInteger(n) && (n === 0 || !!ITEMS[n]))
  )
    throw Error('Invalid hotbar.');
  if (
    !data.inventory ||
    typeof data.inventory !== 'object' ||
    Array.isArray(data.inventory) ||
    !Object.entries(data.inventory).every(
      ([key, n]) =>
        /^\d+$/.test(key) &&
        (Number(key) === 0 || !!ITEMS[key]) &&
        Number.isInteger(n) &&
        n >= 0 &&
        n <= 10000000
    )
  )
    throw Error('Invalid inventory.');
  if (data.version >= 2) {
    const checkStack = (s) => {
      if (s === null) return;
      if (
        !s ||
        typeof s !== 'object' ||
        !Number.isInteger(s.id) ||
        !s.id ||
        !ITEMS[s.id] ||
        !Number.isInteger(s.count) ||
        s.count < 1 ||
        s.count > (ITEMS[s.id].stackSize || (ITEMS[s.id].durability ? 1 : 64))
      )
        throw Error('Invalid item stack.');
      if (
        ITEMS[s.id].durability &&
        (!Number.isInteger(s.durability) ||
          s.durability < 1 ||
          s.durability > ITEMS[s.id].durability)
      )
        throw Error('Invalid durability.');
    };
    const slots = (list, size) => {
      if (!Array.isArray(list) || list.length !== size) throw Error('Invalid inventory slots.');
      list.forEach(checkStack);
    };
    const inv = data.inventoryData;
    if (!inv || ![2, 3].includes(inv.gridSize)) throw Error('Invalid crafting grid.');
    slots(inv.slots, 36);
    slots(inv.armor, 4);
    slots(inv.offhand, 1);
    slots(inv.grid, inv.gridSize ** 2);
    checkStack(inv.cursor);
    inv.armor.forEach((s, i) => {
      if (s && ITEMS[s.id].armor !== i) throw Error('Invalid armor slot.');
    });
    if (!Array.isArray(inv.overflow) || inv.overflow.length > 5000)
      throw Error('Invalid overflow.');
    inv.overflow.forEach(checkStack);
    if (!Array.isArray(data.drops) || data.drops.length > 5000)
      throw Error('Invalid dropped items.');
    for (const d of data.drops) {
      if (
        !d ||
        !['x', 'y', 'z', 'age'].every((k) => Number.isFinite(d[k])) ||
        Math.abs(d.x) > 1000000 ||
        Math.abs(d.z) > 1000000 ||
        Math.abs(d.y) > 10000 ||
        d.age < 0 ||
        d.age > 301
      )
        throw Error('Invalid dropped item position.');
      checkStack(d.stack);
      if (!d.stack) throw Error('Empty dropped item.');
    }
    if (!Array.isArray(data.furnaces) || data.furnaces.length > 5000)
      throw Error('Invalid furnaces.');
    for (const entry of data.furnaces) {
      if (
        !Array.isArray(entry) ||
        entry.length !== 2 ||
        typeof entry[0] !== 'string' ||
        !/^-?\d+,\d+,-?\d+$/.test(entry[0])
      )
        throw Error('Invalid furnace position.');
      const [x, y, z] = entry[0].split(',').map(Number),
        f = entry[1];
      if (
        Math.abs(x) > 1000000 ||
        Math.abs(z) > 1000000 ||
        y < 1 ||
        y > 71 ||
        !f ||
        !['burn', 'burnTotal', 'progress', 'inputId'].every(
          (k) => Number.isFinite(f[k]) && f[k] >= 0
        ) ||
        f.burn > 80 ||
        f.burnTotal > 80 ||
        f.progress > 10
      )
        throw Error('Invalid furnace state.');
      slots(f.slots, 3);
    }
    for (const key of ['saturation', 'exhaustion'])
      if (!Number.isFinite(data.player[key]) || data.player[key] < 0 || data.player[key] > 30)
        throw Error('Invalid food state.');
    if (data.version === 3) {
      const coordinate = (p) =>
        p &&
        ['x', 'y', 'z'].every((k) => Number.isFinite(p[k])) &&
        Math.abs(p.x) <= 1000000 &&
        Math.abs(p.z) <= 1000000 &&
        p.y >= 0 &&
        p.y <= 150;
      const blockPosition = (p) =>
        coordinate(p) &&
        ['x', 'y', 'z'].every((k) => Number.isInteger(p[k])) &&
        p.y > 0 &&
        p.y < 72;
      const dimensions = ['overworld', 'nether'];
      if (
        !dimensions.includes(data.dimension) ||
        !data.dimensions ||
        Array.isArray(data.dimensions) ||
        Object.keys(data.dimensions).some((key) => !dimensions.includes(key)) ||
        !data.dimensions[data.dimension]
      )
        throw Error('Invalid dimension state.');
      if (
        data.bedSpawn !== null &&
        (!blockPosition(data.bedSpawn) ||
          data.bedSpawn.dimension !== 'overworld' ||
          ![0, 1, 2, 3].includes(data.bedSpawn.facing))
      )
        throw Error('Invalid bed spawn.');
      if (!Array.isArray(data.portalLinks) || data.portalLinks.length > 512)
        throw Error('Invalid portal links.');
      for (const link of data.portalLinks)
        for (const dimension of dimensions) {
          const p = link?.[dimension];
          if (
            !blockPosition(p) ||
            p.y > 66 ||
            ![0, 1].includes(p.dx) ||
            ![0, 1].includes(p.dz) ||
            p.dx + p.dz !== 1
          )
            throw Error('Invalid portal position.');
        }
      const kinds = new Set([
        'fox',
        'sentinel',
        ...Object.values(ITEMS)
          .map((item) => item.spawn)
          .filter(Boolean),
      ]);
      for (const state of Object.values(data.dimensions)) {
        if (!state || typeof state !== 'object') throw Error('Invalid dimension contents.');
        validateSave(
          { ...data, version: 2, edits: state.edits, drops: state.drops, furnaces: state.furnaces },
          id
        );
        if (!Array.isArray(state.mobs) || state.mobs.length > 48) throw Error('Invalid mobs.');
        for (const m of state.mobs)
          if (
            !coordinate(m) ||
            !kinds.has(m.kind) ||
            !Number.isFinite(m.health) ||
            m.health <= 0 ||
            m.health > 50 ||
            !Number.isFinite(m.yaw)
          )
            throw Error('Invalid mob state.');
        if (!Array.isArray(state.containers) || state.containers.length > 5000)
          throw Error('Invalid chests.');
        for (const entry of state.containers) {
          if (!Array.isArray(entry) || entry.length !== 2 || typeof entry[0] !== 'string')
            throw Error('Invalid chest position.');
          const parts = entry[0].split(',').map(Number);
          if (parts.length !== 3 || !blockPosition({ x: parts[0], y: parts[1], z: parts[2] }))
            throw Error('Invalid chest position.');
          slots(entry[1], 27);
        }
        if (
          !Array.isArray(state.mobSites) ||
          state.mobSites.length > 10000 ||
          state.mobSites.some((s) => typeof s !== 'string' || s.length > 64)
        )
          throw Error('Invalid structure state.');
      }
      for (const key of ['edits', 'furnaces', 'drops', 'mobs', 'containers', 'mobSites'])
        if (JSON.stringify(data[key]) !== JSON.stringify(data.dimensions[data.dimension][key]))
          throw Error('Active dimension contents do not match.');
    }
  }
  return data;
}

export function createGameServer({
  saveDir = path.join(ROOT, 'saves'),
  addonDir = path.join(ROOT, 'node_modules', 'three', 'examples', 'jsm'),
} = {}) {
  const writes = new Map();
  const send = (res, status, data) => {
    res.writeHead(status, {
      'Content-Type': 'application/json; charset=utf-8',
      'Cache-Control': 'no-store',
    });
    res.end(JSON.stringify(data));
  };
  const body = async (req) => {
    let text = '';
    for await (const part of req) {
      text += part;
      if (text.length > 24 * 1024 * 1024) throw Error('Save too large.');
    }
    return JSON.parse(text);
  };
  const readSave = async (id) => {
    try {
      return validateSave(JSON.parse(await readFile(path.join(saveDir, `${id}.json`), 'utf8')), id);
    } catch (e) {
      try {
        return validateSave(
          JSON.parse(await readFile(path.join(saveDir, `${id}.bak`), 'utf8')),
          id
        );
      } catch {
        throw e;
      }
    }
  };
  const server = http.createServer(async (req, res) => {
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('Content-Security-Policy', contentPolicy);
    res.setHeader('X-Frame-Options', 'DENY');
    try {
      const host = req.headers.host || '';
      if (!/^(127\.0\.0\.1|localhost)(:\d+)?$/.test(host)) {
        send(res, 403, { error: 'Local connections only.' });
        return;
      }
      const url = new URL(req.url, `http://${host}`),
        pathname = decodeURIComponent(url.pathname);
      if (pathname.startsWith('/api/')) {
        if (req.headers.origin && req.headers.origin !== `http://${host}`) {
          send(res, 403, { error: 'Origin not allowed.' });
          return;
        }
        if (pathname === '/api/health' && req.method === 'GET') {
          send(res, 200, { app: 'voxel-wilds', version: '1.2.0', root: ROOT });
          return;
        }
        if (pathname === '/api/worlds' && req.method === 'GET') {
          await mkdir(saveDir, { recursive: true });
          const files = await readdir(saveDir),
            worlds = [];
          for (const file of files.filter((f) => f.endsWith('.json') && validId(f.slice(0, -5)))) {
            try {
              const d = await readSave(file.slice(0, -5));
              worlds.push({
                id: d.id,
                name: d.name,
                seed: d.seed,
                mode: d.mode,
                updatedAt: d.updatedAt,
                playTime: d.playTime,
                blocks: d.edits.length,
              });
            } catch {}
          }
          worlds.sort((a, b) => String(b.updatedAt).localeCompare(String(a.updatedAt)));
          send(res, 200, worlds);
          return;
        }
        const match = pathname.match(/^\/api\/worlds\/([a-zA-Z0-9_-]{1,64})$/);
        if (match && req.method === 'GET') {
          try {
            send(res, 200, await readSave(match[1]));
          } catch {
            send(res, 404, { error: 'World not found or save is unreadable.' });
          }
          return;
        }
        if (match && req.method === 'PUT') {
          const id = match[1],
            data = validateSave(await body(req), id);
          data.updatedAt = new Date().toISOString();
          const previous = writes.get(id) || Promise.resolve();
          const operation = previous
            .catch(() => {})
            .then(async () => {
              await mkdir(saveDir, { recursive: true });
              const file = path.join(saveDir, `${id}.json`),
                temp = path.join(saveDir, `${id}.tmp`);
              await writeFile(temp, JSON.stringify(data), 'utf8');
              try {
                validateSave(JSON.parse(await readFile(file, 'utf8')), id);
                await copyFile(file, path.join(saveDir, `${id}.bak`));
              } catch (e) {
                if (e.code && e.code !== 'ENOENT') throw e;
              }
              await rename(temp, file);
            });
          writes.set(id, operation);
          await operation;
          if (writes.get(id) === operation) writes.delete(id);
          send(res, 200, { ok: true, updatedAt: data.updatedAt });
          return;
        }
        send(res, 404, { error: 'Unknown endpoint.' });
        return;
      }
      if (req.method !== 'GET' && req.method !== 'HEAD') {
        send(res, 405, { error: 'Method not allowed.' });
        return;
      }
      const relative = pathname === '/' ? 'index.html' : pathname.slice(1);
      const allowed =
        relative === 'index.html' ||
        relative === 'style.css' ||
        relative === 'favicon.svg' ||
        relative.startsWith('src/') ||
        relative.startsWith('assets/models/') ||
        relative === 'assets/model-gallery.png' ||
        relative.startsWith('node_modules/three/build/') ||
        relative.startsWith('node_modules/three/examples/jsm/');
      const addonPrefix = 'node_modules/three/examples/jsm/';
      const staticRoot = relative.startsWith(addonPrefix) ? addonDir : ROOT;
      const staticRelative = relative.startsWith(addonPrefix)
        ? relative.slice(addonPrefix.length)
        : relative;
      const absolute = path.resolve(staticRoot, staticRelative);
      if (
        !allowed ||
        !absolute.startsWith(staticRoot + path.sep) ||
        relative.includes('..') ||
        relative.includes('\\')
      ) {
        send(res, 403, { error: 'Forbidden.' });
        return;
      }
      try {
        const info = await stat(absolute);
        if (!info.isFile()) throw Error();
        const bytes = await readFile(absolute);
        res.writeHead(200, {
          'Content-Type': MIME[path.extname(absolute)] || 'application/octet-stream',
          'Content-Length': bytes.length,
          'Cache-Control': 'no-cache',
        });
        res.end(req.method === 'HEAD' ? undefined : bytes);
      } catch {
        send(res, 404, { error: 'File not found.' });
      }
    } catch (error) {
      send(res, 400, { error: error.message || 'Request failed.' });
    }
  });
  return server;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const port = Number(process.env.GAME_PORT) || 4173;
  const server = createGameServer();
  server.on('error', (error) => {
    console.error(
      error.code === 'EADDRINUSE'
        ? `Port ${port} is already in use. Use the launcher or set GAME_PORT.`
        : error
    );
    process.exitCode = 1;
  });
  server.listen(port, '127.0.0.1', () => {
    const url = `http://127.0.0.1:${port}`;
    console.log(
      `Voxel Wilds is ready: ${url}\nWorlds are saved in ${path.join(ROOT, 'saves')}\nPress Ctrl+C to stop.`
    );
    if (process.argv.includes('--open'))
      spawn('explorer.exe', [url], { windowsHide: true, stdio: 'ignore' }).unref();
  });
}
