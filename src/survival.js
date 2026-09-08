import { B, BLOCKS, ITEMS, isSolid } from './blocks.js';
import { collides, hash } from './world.js';
import { makeStack } from './inventory.js';
import {
  bedDirection,
  isPortal,
  findPortalFrame,
  lightPortal,
  buildPortal,
  portalExit,
  collapsePortals,
} from './portals.js';

const $ = (id) => document.getElementById(id);
const frameKey = (p) => `${p.x},${p.y},${p.z},${p.dx}`;
export class Survival {
  constructor(game) {
    this.g = game;
    this.reset();
  }
  reset(saved = {}) {
    const g = this.g;
    g.dimension = saved.dimension || 'overworld';
    this.states = structuredClone(saved.dimensions || {});
    g.bedSpawn = saved.bedSpawn || null;
    g.portalLinks = saved.portalLinks || [];
    g.containers = new Map(saved.containers || []);
    g.mobSites = new Set(saved.mobSites || []);
    g.burning = 0;
    g.deathCountdown = 0;
    this.portalTime = 0;
    this.portalCooldown = 0;
    this.sleepTimer = 0;
    this.transition = false;
  }
  capture() {
    const g = this.g;
    return {
      edits: g.world.serialize(),
      furnaces: structuredClone([...g.furnaces]),
      drops: g.drops.serialize(),
      mobs: g.creatures.serialize(),
      containers: structuredClone([...g.containers]),
      mobSites: [...g.mobSites],
    };
  }
  snapshot() {
    const g = this.g;
    return {
      dimension: g.dimension,
      dimensions: { ...this.states, [g.dimension]: this.capture() },
      bedSpawn: g.bedSpawn,
      portalLinks: g.portalLinks,
      mobs: g.creatures.serialize(),
      containers: [...g.containers],
      mobSites: [...g.mobSites],
    };
  }
  isNight() {
    const t = this.g.time % 1200;
    return t < 300 || t >= 900;
  }
  blockLight(x, y, z) {
    let light = 0;
    for (const [key, id] of this.g.world.edits) {
      if (![B.TORCH, B.LANTERN, B.CAMPFIRE, B.GLOWSTONE, B.LAVA].includes(id)) continue;
      const [lx, ly, lz] = key.split(',').map(Number);
      light = Math.max(light, 15 - Math.abs(x - lx) - Math.abs(y - ly) - Math.abs(z - lz));
    }
    return light;
  }
  bedFoot(x, y, z) {
    const b = BLOCKS[this.g.world.get(x, y, z)];
    if (!b?.bed) return null;
    const [dx, dz] = bedDirection(b.facing);
    return { x: x - (b.bedHead ? dx : 0), y, z: z - (b.bedHead ? dz : 0), facing: b.facing };
  }
  breakBed(x, y, z) {
    const bed = this.bedFoot(x, y, z);
    if (!bed) return;
    const [dx, dz] = bedDirection(bed.facing);
    this.g.world.set(bed.x, bed.y, bed.z, 0);
    this.g.world.set(bed.x + dx, bed.y, bed.z + dz, 0);
  }
  useBed(target) {
    const g = this.g,
      bed = this.bedFoot(target.x, target.y, target.z);
    if (!bed) return;
    g.actionCooldown = 0.5;
    if (g.dimension === 'nether') {
      this.breakBed(target.x, target.y, target.z);
      this.explode(
        target.x + 0.5,
        target.y + 0.5,
        target.z + 0.5,
        5,
        'A bed exploded in the Nether.'
      );
      return;
    }
    const [dx, dz] = bedDirection(bed.facing);
    if (!BLOCKS[g.world.get(bed.x + dx, bed.y, bed.z + dz)]?.bedHead) return;
    g.bedSpawn = { ...bed, dimension: 'overworld' };
    g.save(true).catch(() => {});
    if (!this.isNight()) {
      g.ui.toast('Respawn point set.', 'You can sleep only at night.');
      return;
    }
    if (
      g.creatures.list.some(
        (m) =>
          g.creatures.hostile(m) &&
          Math.hypot(m.x - bed.x, m.z - bed.z) < 8 &&
          Math.abs(m.y - bed.y) < 5
      )
    ) {
      g.ui.toast('You cannot rest now.', 'There are monsters nearby.', true);
      return;
    }
    this.sleepTimer = 3;
    this.sleepHealth = g.player.health;
    g.clearInput();
    $('sleep-overlay').classList.remove('hidden');
  }
  wake(skip = false) {
    if (!this.sleepTimer) return;
    this.sleepTimer = 0;
    $('sleep-overlay').classList.add('hidden');
    if (skip) {
      const g = this.g;
      g.time = Math.floor(g.time / 1200) * 1200 + (g.time % 1200 < 300 ? 300 : 1500);
      g.ui.toast('Good morning.');
      g.save(true).catch(() => {});
    }
  }
  interact(target, item) {
    const g = this.g;
    if (!target) return false;
    if (!g.keys.has('ShiftLeft') && !g.keys.has('ShiftRight')) {
      if (BLOCKS[target.id]?.bed) {
        this.useBed(target);
        return true;
      }
      if (target.id === B.CHEST) {
        g.openInventory('chest', target);
        g.creatures.alertPiglins();
        return true;
      }
    }
    if (item?.igniter) {
      const frame =
        findPortalFrame(
          g.world,
          target.x + target.normal.x,
          target.y + target.normal.y,
          target.z + target.normal.z
        ) || findPortalFrame(g.world, target.x, target.y, target.z);
      if (frame && frame.cells.some((cell) => g.world.get(...cell) !== frame.id)) {
        for (const cell of frame.cells) g.world.set(...cell, frame.id);
        if (g.mode === 'survival') g.storage.damageTool(g.selected, 1);
        g.inventoryChanged();
        g.sound.play('place');
        g.ui.toast('Portal opened.', 'Stand inside to travel.');
      } else if (!frame)
        g.ui.toast(
          'Build an obsidian frame.',
          'A 4-block-wide, 5-block-high frame needs 10 obsidian without corners.'
        );
      g.actionCooldown = 0.5;
      return true;
    }
    if (item?.spawn) {
      if (g.mode === 'creative')
        g.creatures.spawn(
          item.spawn,
          target.x + target.normal.x + 0.5,
          target.z + target.normal.z + 0.5,
          target.y + target.normal.y
        );
      g.actionCooldown = 0.3;
      return true;
    }
    return false;
  }
  placeBed(x, y, z) {
    const g = this.g;
    const facing = ((Math.round(-g.player.yaw / (Math.PI / 2)) % 4) + 4) % 4;
    const [dx, dz] = bedDirection(facing);
    for (const [bx, bz] of [
      [x, z],
      [x + dx, z + dz],
    ]) {
      if (
        g.world.get(bx, y, bz) !== B.AIR ||
        !isSolid(g.world.get(bx, y - 1, bz)) ||
        (Math.abs(bx + 0.5 - g.player.x) < 0.79 &&
          Math.abs(bz + 0.5 - g.player.z) < 0.79 &&
          Math.abs(y - g.player.y) < 1)
      ) {
        g.ui.toast('A bed needs two empty blocks with solid ground below.');
        return false;
      }
    }
    g.world.set(x, y, z, B.BED + facing * 2);
    g.world.set(x + dx, y, z + dz, B.BED + facing * 2 + 1);
    return true;
  }
  chest(target) {
    const g = this.g,
      key = `${target.x},${target.y},${target.z}`;
    if (!g.containers.has(key)) {
      const slots = Array(27).fill(null);
      if (g.dimension === 'nether' && !g.world.edits.has(key)) {
        const items =
          g.world.get(target.x, target.y - 1, target.z) === B.GOLD_BLOCK
            ? [
                [131, 8],
                [33, 5],
                [94, 2],
                [150, 1],
                [130, 1],
              ]
            : [
                [92, 3],
                [131, 4],
                [46, 5],
                [33, 3],
                [135, 1],
              ];
        items.forEach(
          ([id, count], i) =>
            (slots[i * 3] = makeStack(
              id,
              count +
                (ITEMS[id].durability
                  ? 0
                  : Math.floor(hash(target.x + i, target.z, g.world.seedHash) * 3))
            ))
        );
      }
      g.containers.set(key, slots);
    }
    return g.containers.get(key);
  }
  explode(x, y, z, radius, reason) {
    const g = this.g;
    g.particles.burst(x, y, z, '#f3ae64', 35);
    g.sound.play('hurt');
    for (let dz = -radius; dz <= radius; dz++)
      for (let dx = -radius; dx <= radius; dx++)
        for (let dy = -radius; dy <= radius; dy++) {
          if (Math.hypot(dx, dy, dz) > radius - 0.3) continue;
          const bx = Math.floor(x + dx),
            by = Math.floor(y + dy),
            bz = Math.floor(z + dz),
            id = g.world.get(bx, by, bz);
          if (!id || [B.BEDROCK, B.OBSIDIAN, B.LAVA, B.WATER].includes(id)) continue;
          if (id === B.CHEST) this.chest({ x: bx, y: by, z: bz });
          if (BLOCKS[id].bed) this.breakBed(bx, by, bz);
          const key = `${bx},${by},${bz}`;
          for (const stack of g.containers.get(key) || g.furnaces.get(key)?.slots || [])
            if (stack) g.drops.spawn(stack, { x: bx + 0.5, y: by + 0.5, z: bz + 0.5 });
          g.containers.delete(key);
          g.furnaces.delete(key);
          g.world.set(bx, by, bz, 0);
        }
    for (const [key, id] of g.world.edits)
      if (isPortal(id)) {
        const [px, py, pz] = key.split(',').map(Number);
        if (Math.hypot(px - x, py - y, pz - z) < radius + 6) collapsePortals(g.world, px, py, pz);
      }
    const distance = Math.hypot(g.player.x - x, g.player.y - y, g.player.z - z);
    if (distance < radius * 2)
      g.hurt(Math.ceil((1 - distance / (radius * 2)) * radius * 5), reason, true);
    for (const m of [...g.creatures.list]) {
      const d = Math.hypot(m.x - x, m.y - y, m.z - z);
      if (d < radius * 2) g.creatures.hurt(m, Math.ceil((1 - d / (radius * 2)) * radius * 5));
    }
  }
  async changeDimension(destination, sourceFrame = null, respawn = false) {
    const g = this.g;
    if (this.transition) return;
    this.transition = true;
    const before = g.snapshot(),
      source = g.dimension;
    try {
      await g.save(true);
      this.states[source] = this.capture();
      g.paused = true;
      g.clearInput();
      g.ui.hide();
      $('loading').classList.remove('hidden');
      $('load-status').textContent =
        destination === 'nether' ? 'Entering the Nether...' : 'Returning to the Overworld...';
      const state = this.states[destination] || {
        edits: [],
        furnaces: [],
        drops: [],
        containers: [],
        mobSites: [],
      };
      g.dimension = destination;
      g.containers = new Map(state.containers || []);
      g.mobSites = new Set(state.mobSites || []);
      await g.setupWorld(before.seed, {
        ...before,
        ...state,
        player: { ...before.player, x: 0.5, y: 50, z: 0.5 },
      });
      if (!respawn && sourceFrame) {
        let link = g.portalLinks.find(
          (p) => p[source] && frameKey(p[source]) === frameKey(sourceFrame)
        );
        let frame = link?.[destination];
        if (!frame || !lightPortal(g.world, frame.x + frame.dx, frame.y + 1, frame.z + frame.dz)) {
          const scale = destination === 'nether' ? 1 / 8 : 8;
          const x = Math.max(-999990, Math.min(999990, Math.floor(sourceFrame.x * scale))),
            z = Math.max(-999990, Math.min(999990, Math.floor(sourceFrame.z * scale)));
          const y = Math.min(55, Math.max(20, g.world.surface(x, z)));
          for (let dz = -2; dz <= 3; dz++)
            for (let dx = -2; dx <= 5; dx++)
              for (let dy = 0; dy <= 5; dy++)
                g.world.set(x + dx, y + dy, z + dz, dy === 0 ? B.OBSIDIAN : B.AIR);
          frame = buildPortal(g.world, x, y, z);
          if (!link) {
            link = { [source]: sourceFrame };
            g.portalLinks.push(link);
          }
          link[destination] = frame;
        }
        Object.assign(g.player, portalExit(frame));
        g.player.velocity.set(0, 0, 0);
        g.player.fallTop = g.player.y;
      }
      g.player.flying = false;
      this.portalTime = 0;
      this.portalCooldown = 3;
      $('loading').classList.add('hidden');
      g.refreshHeld();
      g.ui.update();
      g.paused = false;
      if (document.pointerLockElement !== g.canvas) g.capture();
      await g.save(true);
    } catch (error) {
      console.error(error);
      g.dimension = source;
      g.containers = new Map(before.containers || []);
      g.mobSites = new Set(before.mobSites || []);
      await g.setupWorld(before.seed, before);
      $('loading').classList.add('hidden');
      g.pause();
      g.ui.toast('Travel failed. Your original world is retained.', error.message, true);
      throw error;
    } finally {
      this.transition = false;
    }
  }
  async respawn() {
    const g = this.g;
    g.deathCountdown = 0;
    this.wake();
    if (g.dimension !== 'overworld') await this.changeDimension('overworld', null, true);
    const bed = g.bedSpawn;
    let position = null;
    if (bed) {
      const id = g.world.get(bed.x, bed.y, bed.z),
        [dx, dz] = bedDirection(bed.facing);
      if (id === B.BED + bed.facing * 2 && g.world.get(bed.x + dx, bed.y, bed.z + dz) === id + 1) {
        for (const [ox, oz] of [
          [-1, 0],
          [1, 0],
          [0, 1],
          [0, -1],
          [-1, -1],
          [1, 1],
        ]) {
          const x = bed.x + ox + 0.5,
            z = bed.z + oz + 0.5;
          for (const y of [bed.y, bed.y + 1, bed.y - 1])
            if (
              isSolid(g.world.get(x, y - 1, z)) &&
              !collides(g.world, x, y, z) &&
              ![B.LAVA, B.WATER, B.CAMPFIRE].includes(g.world.get(x, y, z))
            ) {
              position = { x, y: y + 0.05, z };
              break;
            }
          if (position) break;
        }
      }
      if (!position)
        g.ui.toast('Your bed is missing or obstructed.', 'Returned to the world spawn.');
    }
    g.player = g.newPlayer(position || g.world.spawn());
    g.damageTimer = 0;
    g.damageCooldown = 2;
    g.burning = 0;
    g.ui.hide();
    g.paused = false;
    g.refreshHeld();
    g.ui.update();
    if (document.pointerLockElement !== g.canvas) g.capture();
    await g.save(true);
  }
  frame(dt) {
    const g = this.g;
    if (g.deathCountdown > 0 && !this.transition) {
      g.deathCountdown = Math.max(0, g.deathCountdown - dt);
      $('death-countdown').textContent = `Respawning in ${Math.ceil(g.deathCountdown)}...`;
      if (g.deathCountdown === 0)
        this.respawn().catch((e) => {
          console.error(e);
          g.paused = true;
          g.ui.show('death-modal');
          g.deathCountdown = 3;
          g.ui.toast('Could not respawn. Retrying...', e.message, true);
        });
    }
  }
  tick(dt) {
    const g = this.g;
    if (this.transition) return;
    if (this.sleepTimer) {
      if (g.player.health < this.sleepHealth) this.wake();
      else {
        this.sleepTimer -= dt;
        if (this.sleepTimer <= 0.01) {
          this.sleepTimer = 0.01;
          this.wake(true);
        }
      }
    }
    const lava = g.world.get(g.player.x, g.player.y + 0.2, g.player.z) === B.LAVA;
    if (lava) {
      g.hurt(4, 'Tried to swim in lava.');
      g.burning = 8;
    } else if (g.player.water) g.burning = 0;
    else if (g.burning > 0) {
      g.burning -= dt;
      g.hurt(1, 'Burned to death.');
    }
    $('fire-overlay').classList.toggle('hidden', g.burning <= 0 || g.mode === 'creative');
    this.portalCooldown = Math.max(0, this.portalCooldown - dt);
    const id = g.world.get(g.player.x, g.player.y + 0.1, g.player.z);
    if (isPortal(id) && this.portalCooldown === 0 && !g.ui.modal) {
      const frame = findPortalFrame(
        g.world,
        Math.floor(g.player.x),
        Math.floor(g.player.y + 0.1),
        Math.floor(g.player.z)
      );
      if (!frame) {
        collapsePortals(
          g.world,
          Math.floor(g.player.x),
          Math.floor(g.player.y + 0.1),
          Math.floor(g.player.z)
        );
        this.portalTime = 0;
      } else {
        this.portalTime += dt;
        if (this.portalTime >= (g.mode === 'creative' ? 0.2 : 4))
          this.changeDimension(g.dimension === 'nether' ? 'overworld' : 'nether', frame).catch(
            () => {}
          );
      }
    } else this.portalTime = Math.max(0, this.portalTime - dt * 3);
    $('portal-overlay').style.opacity = String(Math.min(0.7, this.portalTime / 5));
  }
}
