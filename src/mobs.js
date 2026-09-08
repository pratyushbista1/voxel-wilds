import * as THREE from 'three';
import { Creatures } from './assets.js';
import { B, ITEMS, isSolid } from './blocks.js';
import { moveBody, collides, raycast } from './world.js';
import { makeStack } from './inventory.js';
import { netherStructures } from './structures.js';

export const MOB_TYPES = {
  sheep: {
    health: 8,
    speed: 0.8,
    height: 1.3,
    loot: [
      [120, 1, 2],
      [20, 1, 1],
    ],
  },
  cow: {
    health: 10,
    speed: 0.85,
    height: 1.4,
    loot: [
      [122, 1, 3],
      [132, 0, 2],
    ],
  },
  pig: { health: 10, speed: 0.9, height: 1.3, loot: [[124, 1, 3]] },
  chicken: {
    health: 4,
    speed: 0.9,
    height: 0.75,
    loot: [
      [126, 1, 1],
      [133, 0, 2],
    ],
  },
  fox: { health: 10, speed: 1.3, height: 0.85, loot: [] },
  zombie: {
    health: 20,
    speed: 1.7,
    height: 1.95,
    hostile: true,
    undead: true,
    damage: 3,
    loot: [[128, 0, 2]],
  },
  skeleton: {
    health: 20,
    speed: 1.6,
    height: 1.95,
    hostile: true,
    undead: true,
    ranged: true,
    damage: 3,
    loot: [[134, 0, 2]],
  },
  creeper: { health: 20, speed: 1.5, height: 1.8, hostile: true, damage: 0, loot: [] },
  spider: { health: 16, speed: 2, height: 0.85, hostile: true, damage: 2, loot: [[99, 0, 2]] },
  piglin: { health: 16, speed: 1.8, height: 1.95, hostile: true, damage: 5, loot: [[138, 0, 3]] },
  brute: {
    model: 'piglin',
    health: 50,
    speed: 1.7,
    height: 1.95,
    hostile: true,
    damage: 9,
    loot: [[131, 0, 1]],
  },
  blaze: {
    health: 20,
    speed: 1.1,
    height: 1.6,
    hostile: true,
    ranged: true,
    flying: true,
    damage: 5,
    loot: [[135, 0, 1]],
  },
  wither_skeleton: {
    model: 'skeleton',
    health: 20,
    speed: 1.9,
    height: 2.1,
    hostile: true,
    damage: 6,
    loot: [
      [134, 0, 2],
      [91, 0, 1],
    ],
  },
  sentinel: { health: 20, speed: 1.6, height: 2.2, hostile: true, damage: 3, loot: [[94, 1, 2]] },
};
export class Mobs extends Creatures {
  constructor(scene, world, assets, game) {
    super(scene, world, assets);
    this.game = game;
    this.projectiles = [];
    this.scanTimer = 0;
  }
  spawn(kind, x, z, y) {
    const def = MOB_TYPES[kind];
    if (!def || this.list.length >= 48) return null;
    y ??= this.world.surface(x, z);
    if (
      this.world.get(x, y, z) === B.LAVA ||
      this.world.get(x, y, z) === B.WATER ||
      collides(this.world, x, y + 0.02, z, 0.25, def.height)
    )
      return null;
    const m = super.spawn(def.model || kind, x, z, y + 0.02);
    if (!m) return null;
    Object.assign(m, {
      kind,
      health: def.health,
      velocity: new THREE.Vector3(),
      grounded: false,
      hurtTime: 0,
      fire: 0,
      fuse: 0,
      anger: 0,
      attackPose: 0,
    });
    m.materials = [];
    m.mesh.traverse((o) => {
      if (o.isMesh) {
        o.material = o.material.clone();
        if (kind === 'wither_skeleton') o.material.color.multiplyScalar(0.28);
        if (kind === 'brute' && o.material.name.startsWith('brown'))
          o.material.color.set('#29242a');
        m.materials.push(o.material);
      }
    });
    if (kind === 'wither_skeleton') m.mesh.scale.setScalar(1.07);
    return m;
  }
  populate(saved) {
    if (Array.isArray(saved)) {
      for (const data of saved) {
        const m = this.spawn(data.kind, data.x, data.z, data.y - 0.02);
        if (m) Object.assign(m, { health: data.health, yaw: data.yaw || 0 });
      }
      return;
    }
    if (this.world.dimension === 'nether') return;
    for (const [kind, x, z] of [
      ['sheep', -7, 0],
      ['sheep', -11, -4],
      ['sheep', -14, 3],
      ['cow', -7, 10],
      ['cow', -10, 13],
      ['pig', -2, -9],
      ['pig', -5, -12],
      ['chicken', -5, 7],
      ['chicken', -8, 8],
      ['fox', -20, 7],
    ])
      this.spawn(kind, x + 0.5, z + 0.5);
  }
  serialize() {
    return this.list.map(({ kind, x, y, z, health, yaw }) => ({ kind, x, y, z, health, yaw }));
  }
  hostile(m) {
    if (!MOB_TYPES[m.kind].hostile || this.game.settings.difficulty === 'peaceful') return false;
    if (
      m.kind === 'piglin' &&
      m.anger <= 0 &&
      this.game.storage.armor.some((s) => ITEMS[s?.id]?.gold)
    )
      return false;
    return m.kind !== 'spider' || this.game.isNight() || m.anger > 0;
  }
  alertPiglins() {
    for (const m of this.list) if (['piglin', 'brute'].includes(m.kind)) m.anger = 45;
  }
  hurt(m, damage, direction, drops = true) {
    if (!this.list.includes(m) || m.hurtTime > 0.15) return false;
    m.health -= damage;
    m.hurtTime = 0.4;
    m.anger = 15;
    if (direction && ['piglin', 'brute'].includes(m.kind)) this.alertPiglins();
    m.walking = true;
    m.timer = 2;
    if (direction) {
      m.velocity.x += direction.x * 5;
      m.velocity.z += direction.z * 5;
      m.velocity.y = 3;
      m.grounded = false;
      m.yaw = Math.atan2(direction.x, direction.z);
    }
    if (m.health > 0) return false;
    if (drops)
      for (const [id, min, max] of MOB_TYPES[m.kind].loot) {
        const count = min + Math.floor(Math.random() * (max - min + 1));
        if (count) this.game.drops.spawn(makeStack(id, count), { x: m.x, y: m.y + 0.4, z: m.z });
      }
    this.game.stats.kills = (this.game.stats.kills || 0) + 1;
    this.remove(m);
    return true;
  }
  shoot(m, player) {
    const pos = new THREE.Vector3(m.x, m.y + 1.2, m.z);
    const direction = new THREE.Vector3(player.x, player.y + 1, player.z).sub(pos).normalize();
    const mesh = new THREE.Mesh(
      m.kind === 'blaze'
        ? new THREE.BoxGeometry(0.22, 0.22, 0.22)
        : new THREE.BoxGeometry(0.06, 0.06, 0.6),
      new THREE.MeshBasicMaterial({ color: m.kind === 'blaze' ? '#ffb334' : '#cdb995' })
    );
    mesh.position.copy(pos);
    mesh.lookAt(pos.clone().add(direction));
    this.scene.add(mesh);
    this.projectiles.push({
      mesh,
      direction,
      speed: m.kind === 'blaze' ? 7 : 13,
      life: 5,
      damage:
        MOB_TYPES[m.kind].damage *
        (this.game.settings.difficulty === 'hard'
          ? 1.5
          : this.game.settings.difficulty === 'easy'
            ? 0.65
            : 1),
      fire: m.kind === 'blaze',
    });
  }
  update(dt, player, night, survival) {
    const g = this.game;
    this.clock += dt;
    this.spawnTimer += dt;
    this.scanTimer += dt;
    const nether = this.world.dimension === 'nether';
    if (this.scanTimer > 3 && nether) {
      this.scanTimer = 0;
      for (const s of netherStructures(player.x, player.z, 50)) {
        const key = `${s.kind}:${s.x},${s.z}`;
        if (Math.hypot(s.x - player.x, s.z - player.z) > 55 || g.mobSites.has(key)) continue;
        g.mobSites.add(key);
        if (s.kind === 'fortress') {
          this.spawn('wither_skeleton', s.x + 3, s.z, s.y + 1);
          this.spawn('blaze', s.x, s.z + 20, s.y + 2);
        } else {
          this.spawn('piglin', s.x + 6, s.z, s.y + 1);
          this.spawn('piglin', s.x - 6, s.z, s.y + 1);
          this.spawn('brute', s.x + 3, s.z + 3, s.y + 1);
        }
      }
    }
    if (this.spawnTimer > 10) {
      this.spawnTimer = 0;
      if (
        survival &&
        (night || nether) &&
        g.settings.difficulty !== 'peaceful' &&
        this.list.filter((m) => MOB_TYPES[m.kind].hostile).length < 12
      ) {
        for (let attempt = 0; attempt < 5; attempt++) {
          const a = Math.random() * Math.PI * 2,
            radius = 24 + Math.random() * 10;
          const x = Math.floor(player.x + Math.sin(a) * radius) + 0.5,
            z = Math.floor(player.z + Math.cos(a) * radius) + 0.5,
            y = this.world.surface(x, z);
          if (g.blockLight(x, y, z) >= 8) continue;
          const kinds = nether
            ? ['piglin', 'piglin', 'blaze']
            : ['zombie', 'zombie', 'skeleton', 'spider', 'creeper'];
          if (this.spawn(kinds[Math.floor(Math.random() * kinds.length)], x, z, y)) break;
        }
      }
      if (!nether && !night && this.list.filter((m) => !MOB_TYPES[m.kind].hostile).length < 10) {
        for (let attempt = 0; attempt < 5; attempt++) {
          const angle = Math.random() * Math.PI * 2,
            radius = 24 + Math.random() * 10;
          const x = Math.floor(player.x + Math.sin(angle) * radius) + 0.5;
          const z = Math.floor(player.z + Math.cos(angle) * radius) + 0.5;
          const y = this.world.surface(x, z);
          if (this.world.get(x, y - 1, z) !== B.GRASS) continue;
          const kinds = ['sheep', 'cow', 'pig', 'chicken'];
          if (this.spawn(kinds[Math.floor(Math.random() * kinds.length)], x, z, y)) break;
        }
      }
      if (nether && survival && g.settings.difficulty !== 'peaceful') {
        const spawners = new Map();
        for (const s of netherStructures(player.x, player.z, 40))
          if (s.kind === 'fortress')
            spawners.set(`${s.x},${s.y + 1},${s.z + 23}`, [s.x, s.y + 1, s.z + 23]);
        for (const [key, id] of this.world.edits)
          if (id === B.SPAWNER) spawners.set(key, key.split(',').map(Number));
        for (const [x, y, z] of spawners.values()) {
          if (
            this.world.get(x, y, z) === B.SPAWNER &&
            Math.hypot(x - player.x, y - player.y, z - player.z) < 16 &&
            this.list.filter((m) => m.kind === 'blaze').length < 4
          )
            this.spawn('blaze', x + 2.5, z + 0.5, y + 1);
        }
      }
    }
    for (const m of [...this.list]) {
      const def = MOB_TYPES[m.kind],
        distance = Math.hypot(m.x - player.x, m.z - player.z);
      if (distance > 100 || (def.hostile && g.settings.difficulty === 'peaceful')) {
        this.remove(m);
        continue;
      }
      m.mesh.visible = distance <= g.settings.entityDistance;
      if (distance > Math.min(80, g.settings.entityDistance)) continue;
      m.timer -= dt;
      m.cooldown -= dt;
      m.anger = Math.max(0, m.anger - dt);
      m.hurtTime = Math.max(0, m.hurtTime - dt);
      m.attackPose = Math.max(0, m.attackPose - dt);
      for (const mat of m.materials)
        if (mat.emissive) {
          mat.emissive.set(m.hurtTime > 0 ? '#b52421' : '#000000');
          mat.emissiveIntensity = 0.5;
        }
      if (def.undead && !nether && !night) {
        let covered = false;
        for (let y = Math.floor(m.y + def.height); y < 72; y++)
          if (isSolid(this.world.get(m.x, y, m.z))) {
            covered = true;
            break;
          }
        if (!covered) {
          m.fire += dt;
          if (m.fire >= 1) {
            m.fire = 0;
            this.hurt(m, 2);
            if (!this.list.includes(m)) continue;
          }
        }
      }
      const aggressive = survival && this.hostile(m) && distance < 28;
      const origin = new THREE.Vector3(m.x, m.y + Math.min(1.4, def.height * 0.8), m.z);
      const toward = new THREE.Vector3(player.x, player.y + 1, player.z).sub(origin);
      const wall = aggressive
        ? raycast(this.world, origin, toward.clone().normalize(), toward.length())
        : null;
      const visible = !wall || wall.distance >= toward.length() - 0.5;
      if (aggressive) {
        m.yaw = Math.atan2(player.x - m.x, player.z - m.z);
        m.walking = distance > (def.ranged && visible ? 7 : 1.3);
        if (m.kind === 'creeper') {
          m.fuse = distance < 3 && visible ? m.fuse + dt : Math.max(0, m.fuse - dt * 2);
          m.mesh.scale.setScalar(1 + Math.sin(m.fuse * 24) * 0.045);
          if (m.fuse >= 1.5) {
            g.explode(m.x, m.y + 0.5, m.z, 3, 'A creeper exploded.');
            if (this.list.includes(m)) this.remove(m);
            continue;
          }
        } else if (
          visible &&
          m.cooldown <= 0 &&
          distance < (def.ranged ? 16 : 1.9) &&
          Math.abs(player.y - m.y) < (def.ranged ? 9 : 2)
        ) {
          m.cooldown = def.ranged ? 2.3 : 1.2;
          m.attackPose = 0.4;
          if (def.ranged) this.shoot(m, player);
          else
            g.hurt(
              def.damage *
                (g.settings.difficulty === 'hard'
                  ? 1.5
                  : g.settings.difficulty === 'easy'
                    ? 0.65
                    : 1),
              `Killed by a ${m.kind.replaceAll('_', ' ')}.`,
              true
            );
        }
      } else if (m.timer <= 0) {
        m.timer = 2 + Math.random() * 4;
        m.walking = Math.random() > 0.3;
        m.yaw += (Math.random() - 0.5) * 2;
      }
      const speed = def.speed * (!def.hostile && m.anger > 0 ? 2.4 : 1);
      const blend = 1 - Math.exp(-dt * (m.hurtTime > 0 ? 2 : 10));
      m.velocity.x += ((m.walking ? Math.sin(m.yaw) * speed : 0) - m.velocity.x) * blend;
      m.velocity.z += ((m.walking ? Math.cos(m.yaw) * speed : 0) - m.velocity.z) * blend;
      if (def.flying)
        m.velocity.y +=
          ((aggressive
            ? Math.max(-1.5, Math.min(1.5, player.y + 1 - m.y))
            : Math.sin(this.clock + m.phase) * 0.25) -
            m.velocity.y) *
          blend;
      else {
        m.velocity.y = Math.max(-30, m.velocity.y - 22 * dt);
        if (
          m.grounded &&
          m.walking &&
          collides(
            this.world,
            m.x + Math.sin(m.yaw) * 0.5,
            m.y + 0.05,
            m.z + Math.cos(m.yaw) * 0.5,
            0.25,
            def.height
          )
        )
          m.velocity.y = 7.5;
      }
      const result = moveBody(
        this.world,
        m,
        m.velocity,
        dt,
        !!def.flying,
        m.kind === 'spider' ? 0.55 : 0.25,
        def.height
      );
      m.grounded = result.grounded;
      m.mesh.position.set(m.x, m.y, m.z);
      m.mesh.rotation.y +=
        Math.atan2(Math.sin(m.yaw - m.mesh.rotation.y), Math.cos(m.yaw - m.mesh.rotation.y)) *
        blend;
      m.walkBlend = THREE.MathUtils.damp(m.walkBlend, m.walking ? 1 : 0, 7, dt);
      m.walkPhase += dt * speed * 8;
      m.legs.forEach(
        (leg, i) =>
          (leg.rotation.x =
            Math.sin(m.walkPhase + (m.legs.length === 4 ? [0, 1, 1, 0][i] : i % 2) * Math.PI) *
            0.48 *
            m.walkBlend)
      );
      m.arms.forEach(
        (arm, i) =>
          (arm.rotation.x =
            m.kind === 'zombie'
              ? -1.25 + Math.sin(this.clock * 3) * 0.05
              : m.attackPose > 0
                ? -1.4
                : Math.sin(m.walkPhase + i * Math.PI) * 0.35 * m.walkBlend)
      );
      if (m.headRig) m.headRig.rotation.y = aggressive ? 0 : Math.sin(this.clock + m.phase) * 0.18;
      if (m.kind === 'blaze') m.mesh.rotation.y += dt * 2;
    }
    for (const p of [...this.projectiles]) {
      p.life -= dt;
      const hit = raycast(this.world, p.mesh.position, p.direction, p.speed * dt);
      p.mesh.position.addScaledVector(p.direction, p.speed * dt);
      const touches =
        Math.hypot(p.mesh.position.x - player.x, p.mesh.position.z - player.z) < 0.55 &&
        p.mesh.position.y > player.y &&
        p.mesh.position.y < player.y + 1.8;
      if (touches && !hit && survival) {
        g.hurt(p.damage, p.fire ? 'Hit by a blaze fireball.' : 'Shot by a skeleton.', true);
        if (p.fire && !g.blocking && survival) g.burning = 4;
      }
      if (hit || touches || p.life <= 0) this.removeProjectile(p);
    }
  }
  hit(origin, direction, reach = 3.1) {
    let nearest = null,
      best = reach;
    for (const m of this.list) {
      const def = MOB_TYPES[m.kind];
      const box = new THREE.Box3(
        new THREE.Vector3(m.x - 0.4, m.y, m.z - 0.4),
        new THREE.Vector3(m.x + 0.4, m.y + def.height, m.z + 0.4)
      );
      const point = new THREE.Ray(origin, direction).intersectBox(box, new THREE.Vector3());
      const distance = point?.distanceTo(origin);
      if (point && distance < best) {
        best = distance;
        nearest = m;
      }
    }
    return nearest;
  }
  removeProjectile(p) {
    this.scene.remove(p.mesh);
    p.mesh.geometry.dispose();
    p.mesh.material.dispose();
    this.projectiles.splice(this.projectiles.indexOf(p), 1);
  }
  remove(m) {
    if (!this.list.includes(m)) return;
    for (const mat of m.materials || []) mat.dispose();
    super.remove(m);
  }
  dispose() {
    super.dispose();
    for (const p of [...this.projectiles]) this.removeProjectile(p);
  }
}
