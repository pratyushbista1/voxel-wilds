import { B, BLOCKS, isSolid } from './blocks.js';

export const CHUNK = 16,
  HEIGHT = 72,
  SEA = 22;
export function seedNumber(text) {
  let h = 2166136261;
  for (const c of String(text)) {
    h ^= c.charCodeAt(0);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}
export function hash(x, z, seed = 0) {
  let h = Math.imul(x, 374761393) ^ Math.imul(z, 668265263) ^ seed;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}
const smooth = (t) => t * t * (3 - 2 * t);
export function noise(x, z, seed) {
  const ix = Math.floor(x),
    iz = Math.floor(z),
    fx = smooth(x - ix),
    fz = smooth(z - iz);
  const a = hash(ix, iz, seed),
    b = hash(ix + 1, iz, seed),
    c = hash(ix, iz + 1, seed),
    d = hash(ix + 1, iz + 1, seed);
  return (a + (b - a) * fx) * (1 - fz) + (c + (d - c) * fx) * fz;
}
const mod = (n, m) => ((n % m) + m) % m;
export const index = (x, y, z) => x + z * CHUNK + y * CHUNK * CHUNK;

export class World {
  constructor(seed = 'wildflower', edits = []) {
    this.seed = String(seed).slice(0, 64);
    this.seedHash = seedNumber(seed);
    this.chunks = new Map();
    this.edits = new Map(edits);
    this.dirty = new Set();
    this.revision = 0;
    this.editChunks = new Map();
    for (const [key, id] of this.edits) {
      const [x, y, z] = key.split(',').map(Number),
        ck = this.key(Math.floor(x / CHUNK), Math.floor(z / CHUNK));
      if (!this.editChunks.has(ck)) this.editChunks.set(ck, new Map());
      this.editChunks.get(ck).set(key, id);
    }
  }
  key(cx, cz) {
    return `${cx},${cz}`;
  }
  terrain(x, z) {
    const s = this.seedHash;
    const broad = noise(x / 85, z / 85, s) * 14;
    const detail = noise(x / 23, z / 23, s + 7) * 5 + noise(x / 9, z / 9, s + 12) * 1.8;
    const distance = Math.hypot(x + 3, z - 3);
    const ridge =
      Math.max(0, noise(x / 74, z / 74, s + 41) - 0.53) * 65 * smooth(Math.min(1, distance / 65));
    let h = 20 + broad + detail + ridge;
    const clearing = 1 - smooth(Math.max(0, Math.min(1, (distance - 10) / 26)));
    h = h * (1 - clearing) + 28 * clearing;
    const riverX = 19 + Math.sin(z / 39) * 9 + Math.sin(z / 84) * 13;
    const river = Math.abs(x - riverX);
    const riverMix = 1 - smooth(Math.max(0, Math.min(1, (river - 3) / 14)));
    h = h * (1 - riverMix) + 18 * riverMix;
    return Math.max(6, Math.min(59, Math.floor(h)));
  }
  biome(x, z) {
    const h = this.terrain(x, z);
    return h >= 42
      ? 'Highlands'
      : h <= SEA + 1
        ? 'Riverlands'
        : noise(x / 55, z / 55, this.seedHash + 9) > 0.55
          ? 'Oak woodland'
          : 'Sunlit meadow';
  }
  getChunk(cx, cz) {
    const key = this.key(cx, cz);
    if (this.chunks.has(key)) return this.chunks.get(key);
    const data = new Uint8Array(CHUNK * CHUNK * HEIGHT);
    const chunk = { cx, cz, key, data, decorations: [] };
    this.chunks.set(key, chunk);
    for (let z = 0; z < CHUNK; z++)
      for (let x = 0; x < CHUNK; x++) {
        const wx = cx * CHUNK + x,
          wz = cz * CHUNK + z,
          h = this.terrain(wx, wz);
        for (let y = 0; y < Math.max(h + 1, SEA + 1); y++) {
          let id = B.STONE;
          if (y === 0) id = B.BEDROCK;
          else if (y > h) id = B.WATER;
          else if (y === h) id = h < SEA + 2 ? B.SAND : h > 44 ? B.SNOW : B.GRASS;
          else if (y > h - 4)
            id =
              h < SEA + 2 ? (hash(wx, wz, this.seedHash + 301) > 0.91 ? B.CLAY : B.SAND) : B.DIRT;
          else {
            // Continuous tunnels, with solid ceilings and a bedrock floor.
            const cave = noise(wx / 17 + y * 0.09, wz / 17 - y * 0.073, this.seedHash + 66);
            const cave2 = noise(wx / 10 - y * 0.12, wz / 10 + y * 0.1, this.seedHash + 90);
            if (y > 3 && y < h - 5 && cave > 0.53 && cave < 0.64 && cave2 > 0.52 && cave2 < 0.68)
              id = B.AIR;
            else {
              const ore = hash(wx + y * 57, wz - y * 21, this.seedHash + 140);
              if (y < 16 && ore > 0.985) id = B.CRYSTAL_ORE;
              else if (y < 27 && ore > 0.963) id = B.IRON_ORE;
              else if (ore > 0.943) id = B.COAL_ORE;
            }
          }
          data[index(x, y, z)] = id;
        }
      }
    const put = (wx, y, wz, id, overwrite = false) => {
      const x = wx - cx * CHUNK,
        z = wz - cz * CHUNK;
      if (x < 0 || z < 0 || x >= CHUNK || z >= CHUNK || y < 1 || y >= HEIGHT) return;
      const i = index(x, y, z);
      if (overwrite || data[i] === B.AIR || data[i] === B.LEAVES) data[i] = id;
    };
    // Trees are evaluated across chunk boundaries, independent of generation order.
    for (
      let gz = Math.floor((cz * CHUNK - 3) / 7);
      gz <= Math.floor((cz * CHUNK + CHUNK + 3) / 7);
      gz++
    )
      for (
        let gx = Math.floor((cx * CHUNK - 3) / 7);
        gx <= Math.floor((cx * CHUNK + CHUNK + 3) / 7);
        gx++
      ) {
        const chance = hash(gx, gz, this.seedHash + 211);
        if (chance > 0.5) continue;
        const tx = gx * 7 + Math.floor(hash(gx, gz, this.seedHash + 22) * 5),
          tz = gz * 7 + Math.floor(hash(gx, gz, this.seedHash + 23) * 5),
          h = this.terrain(tx, tz);
        if (h < SEA + 3 || h > 44 || Math.hypot(tx + 3, tz - 3) < 9) continue;
        const tall = 4 + Math.floor(hash(gx, gz, this.seedHash + 24) * 3);
        for (let y = h + tall - 2; y <= h + tall + 1; y++) {
          const r = y >= h + tall + 1 ? 1 : 2;
          for (let dz = -r; dz <= r; dz++)
            for (let dx = -r; dx <= r; dx++) {
              if (
                Math.abs(dx) === r &&
                Math.abs(dz) === r &&
                hash(tx + dx, tz + dz, safeSeed(this.seedHash, y)) < 0.7
              )
                continue;
              put(tx + dx, y, tz + dz, B.LEAVES);
            }
        }
        for (let y = h + 1; y <= h + tall - 1; y++) put(tx, y, tz, B.LOG, true);
      }
    for (const [editKey, id] of this.editChunks.get(key) || []) {
      const [x, y, z] = editKey.split(',').map(Number);
      if (y >= 0 && y < HEIGHT) data[index(mod(x, CHUNK), y, mod(z, CHUNK))] = id;
    }
    return chunk;
  }
  get(x, y, z) {
    x = Math.floor(x);
    y = Math.floor(y);
    z = Math.floor(z);
    if (y < 0) return B.BEDROCK;
    if (y >= HEIGHT) return B.AIR;
    const chunk = this.getChunk(Math.floor(x / CHUNK), Math.floor(z / CHUNK));
    return chunk.data[index(mod(x, CHUNK), y, mod(z, CHUNK))];
  }
  set(x, y, z, id) {
    if (
      ![x, y, z, id].every(Number.isInteger) ||
      y <= 0 ||
      y >= HEIGHT ||
      !BLOCKS[id] ||
      Math.abs(x) > 1000000 ||
      Math.abs(z) > 1000000
    )
      return false;
    if (this.get(x, y, z) === id) return false;
    const cx = Math.floor(x / CHUNK),
      cz = Math.floor(z / CHUNK),
      key = this.key(cx, cz),
      editKey = `${x},${y},${z}`;
    this.getChunk(cx, cz).data[index(mod(x, CHUNK), y, mod(z, CHUNK))] = id;
    this.edits.set(editKey, id);
    if (!this.editChunks.has(key)) this.editChunks.set(key, new Map());
    this.editChunks.get(key).set(editKey, id);
    this.dirty.add(key);
    // Ambient occlusion samples diagonal neighbors too.
    for (let dz = -1; dz <= 1; dz++)
      for (let dx = -1; dx <= 1; dx++)
        if (
          (!dx || mod(x, CHUNK) === (dx < 0 ? 0 : 15)) &&
          (!dz || mod(z, CHUNK) === (dz < 0 ? 0 : 15))
        )
          this.dirty.add(this.key(cx + dx, cz + dz));
    this.revision++;
    return true;
  }
  surface(x, z) {
    for (let y = HEIGHT - 1; y >= 0; y--) {
      const b = this.get(x, y, z);
      if (isSolid(b) && b !== B.LEAVES && b !== B.LOG) return y + 1;
    }
    return 1;
  }
  spawn() {
    for (let radius = 0; radius <= 10; radius++)
      for (let dz = -radius; dz <= radius; dz++)
        for (let dx = -radius; dx <= radius; dx++) {
          if (Math.max(Math.abs(dx), Math.abs(dz)) !== radius) continue;
          const x = -4 + dx,
            z = 4 + dz,
            y = this.surface(x, z);
          if (
            !isSolid(this.get(x, y, z)) &&
            !isSolid(this.get(x, y + 1, z)) &&
            this.get(x, y, z) !== B.WATER
          )
            return { x: x + 0.5, y: y + 0.05, z: z + 0.5 };
        }
    // If the whole clearing was built over, arrive safely above its highest block.
    let y = HEIGHT;
    while (y > 1 && !isSolid(this.get(-4, y - 1, 4))) y--;
    return { x: -3.5, y: y + 0.05, z: 4.5 };
  }
  serialize() {
    return [...this.edits];
  }
  evict(centerX, centerZ, radius = 10) {
    for (const [key, c] of this.chunks)
      if (Math.abs(c.cx - centerX) > radius || Math.abs(c.cz - centerZ) > radius)
        this.chunks.delete(key);
  }
}
function safeSeed(seed, y) {
  return (seed + y * 123) >>> 0;
}

// Exact voxel DDA. Works in negative coordinates and from inside blocks.
export function raycast(world, origin, direction, maxDistance = 7) {
  let x = Math.floor(origin.x),
    y = Math.floor(origin.y),
    z = Math.floor(origin.z),
    distance = 0,
    normal = { x: 0, y: 0, z: 0 };
  const sx = Math.sign(direction.x),
    sy = Math.sign(direction.y),
    sz = Math.sign(direction.z);
  const dx = Math.abs(1 / direction.x),
    dy = Math.abs(1 / direction.y),
    dz = Math.abs(1 / direction.z);
  let tx = sx === 0 ? Infinity : (sx > 0 ? x + 1 - origin.x : origin.x - x) * dx;
  let ty = sy === 0 ? Infinity : (sy > 0 ? y + 1 - origin.y : origin.y - y) * dy;
  let tz = sz === 0 ? Infinity : (sz > 0 ? z + 1 - origin.z : origin.z - z) * dz;
  for (let n = 0; n < 160 && distance <= maxDistance; n++) {
    const id = world.get(x, y, z);
    if (id !== B.AIR && id !== B.WATER) return { x, y, z, id, normal, distance };
    if (tx < ty && tx < tz) {
      x += sx;
      distance = tx;
      tx += dx;
      normal = { x: -sx, y: 0, z: 0 };
    } else if (ty < tz) {
      y += sy;
      distance = ty;
      ty += dy;
      normal = { x: 0, y: -sy, z: 0 };
    } else {
      z += sz;
      distance = tz;
      tz += dz;
      normal = { x: 0, y: 0, z: -sz };
    }
  }
  return null;
}

export function collides(world, x, y, z, radius = 0.29, height = 1.78) {
  for (let iy = Math.floor(y + 0.001); iy <= Math.floor(y + height - 0.001); iy++)
    for (let iz = Math.floor(z - radius + 0.001); iz <= Math.floor(z + radius - 0.001); iz++)
      for (let ix = Math.floor(x - radius + 0.001); ix <= Math.floor(x + radius - 0.001); ix++)
        if (isSolid(world.get(ix, iy, iz))) return true;
  return false;
}

export function moveBody(world, position, velocity, dt, fly = false) {
  const result = { grounded: false, impact: 0 };
  // Small substeps prevent sprinting, low frame rates and long falls tunnelling.
  const steps = Math.max(
    1,
    Math.ceil(
      (Math.max(Math.abs(velocity.x), Math.abs(velocity.y), Math.abs(velocity.z)) * dt) / 0.22
    )
  );
  for (let step = 0; step < steps; step++) {
    for (const axis of ['x', 'z', 'y']) {
      const old = position[axis],
        amount = (velocity[axis] * dt) / steps;
      if (!amount) continue;
      position[axis] += amount;
      if (collides(world, position.x, position.y, position.z)) {
        position[axis] = old;
        let low = 0,
          high = 1;
        for (let attempt = 0; attempt < 10; attempt++) {
          const fraction = (low + high) / 2;
          position[axis] = old + amount * fraction;
          if (collides(world, position.x, position.y, position.z)) high = fraction;
          else low = fraction;
        }
        position[axis] = old + amount * low;
        if (axis === 'y' && amount < 0) {
          position.y = Math.ceil(position.y);
          result.grounded = true;
          result.impact = Math.min(result.impact, velocity.y);
        }
        velocity[axis] = 0;
      }
    }
  }
  return result;
}
