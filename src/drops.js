import * as THREE from 'three';
import { isSolid } from './blocks.js';
import { cloneStack, maxStack, stackable } from './inventory.js';

export class Drops {
  constructor(scene, world, icons) {
    this.scene = scene;
    this.world = world;
    this.icons = icons;
    this.list = [];
    this.materials = new Map();
    this.geometry = new THREE.PlaneGeometry(0.38, 0.38);
    this.clock = 0;
  }
  spawn(stack, position, velocity = null, age = 0) {
    if (!stack) return;
    const merge = this.list.find(
      (d) =>
        stackable(d.stack, stack) &&
        d.stack.count + stack.count <= maxStack(stack.id) &&
        Math.hypot(d.x - position.x, d.y - position.y, d.z - position.z) < 1
    );
    if (merge) {
      merge.stack.count += stack.count;
      merge.age = Math.min(merge.age, age);
      return;
    }
    if (!this.materials.has(stack.id)) {
      const texture = new THREE.TextureLoader().load(this.icons[stack.id]);
      texture.magFilter = THREE.NearestFilter;
      texture.colorSpace = THREE.SRGBColorSpace;
      this.materials.set(
        stack.id,
        new THREE.MeshLambertMaterial({
          map: texture,
          transparent: true,
          alphaTest: 0.25,
          side: THREE.DoubleSide,
        })
      );
    }
    const mesh = new THREE.Mesh(this.geometry, this.materials.get(stack.id));
    const d = {
      stack: cloneStack(stack),
      x: position.x,
      y: position.y,
      z: position.z,
      vx: velocity?.x || 0,
      vy: velocity?.y ?? 2,
      vz: velocity?.z || 0,
      age,
      mesh,
      phase: Math.random() * 6,
    };
    mesh.position.set(d.x, d.y, d.z);
    this.scene.add(mesh);
    this.list.push(d);
  }
  update(dt, player, inventory, onPickup) {
    this.clock += dt;
    for (const d of [...this.list]) {
      d.age += dt;
      d.vy = Math.max(-12, d.vy - dt * 14);
      const nx = d.x + d.vx * dt,
        nz = d.z + d.vz * dt,
        ny = d.y + d.vy * dt;
      if (!isSolid(this.world.get(nx, d.y, d.z))) d.x = nx;
      else d.vx = 0;
      if (!isSolid(this.world.get(d.x, d.y, nz))) d.z = nz;
      else d.vz = 0;
      if (!isSolid(this.world.get(d.x, ny - 0.12, d.z))) d.y = ny;
      else {
        if (d.vy < 0) d.y = Math.floor(ny - 0.12) + 1.12;
        d.vy = 0;
      }
      d.vx *= Math.exp(-dt * 4);
      d.vz *= Math.exp(-dt * 4);
      d.mesh.position.set(d.x, d.y + Math.sin(this.clock * 2.3 + d.phase) * 0.04, d.z);
      d.mesh.rotation.y += dt * 1.3;
      if (
        d.age > 0.8 &&
        Math.hypot(d.x - player.x, d.y - (player.y + 0.65), d.z - player.z) < 1.65 &&
        player.health > 0
      ) {
        const before = d.stack.count,
          rest = inventory.insert(d.stack);
        if (before !== (rest?.count || 0)) onPickup(d.stack.id, before - (rest?.count || 0));
        if (!rest) {
          this.remove(d);
          continue;
        }
        d.stack = rest;
      }
      if (d.age > 300 || d.y < -5) this.remove(d);
    }
  }
  remove(d) {
    this.scene.remove(d.mesh);
    this.list.splice(this.list.indexOf(d), 1);
  }
  serialize() {
    return this.list.map((d) => ({
      stack: cloneStack(d.stack),
      x: d.x,
      y: d.y,
      z: d.z,
      age: d.age,
    }));
  }
  dispose() {
    for (const d of [...this.list]) this.remove(d);
    for (const material of this.materials.values()) {
      material.map.dispose();
      material.dispose();
    }
    this.geometry.dispose();
  }
}
