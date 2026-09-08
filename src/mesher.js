import * as THREE from 'three';
import { CHUNK, HEIGHT, index } from './world.js';
import { B, BLOCKS, isSolid, tileFor } from './blocks.js';
import { uvFor } from './textures.js';

const FACES = [
  {
    n: [1, 0, 0],
    v: [
      [1, 0, 1],
      [1, 0, 0],
      [1, 1, 0],
      [1, 1, 1],
    ],
    shade: 0.82,
  },
  {
    n: [-1, 0, 0],
    v: [
      [0, 0, 0],
      [0, 0, 1],
      [0, 1, 1],
      [0, 1, 0],
    ],
    shade: 0.72,
  },
  {
    n: [0, 1, 0],
    v: [
      [0, 1, 1],
      [1, 1, 1],
      [1, 1, 0],
      [0, 1, 0],
    ],
    shade: 1,
  },
  {
    n: [0, -1, 0],
    v: [
      [0, 0, 0],
      [1, 0, 0],
      [1, 0, 1],
      [0, 0, 1],
    ],
    shade: 0.51,
  },
  {
    n: [0, 0, 1],
    v: [
      [0, 0, 1],
      [1, 0, 1],
      [1, 1, 1],
      [0, 1, 1],
    ],
    shade: 0.9,
  },
  {
    n: [0, 0, -1],
    v: [
      [1, 0, 0],
      [0, 0, 0],
      [0, 1, 0],
      [1, 1, 0],
    ],
    shade: 0.76,
  },
];

export class ChunkRenderer {
  constructor(world, scene, texture, assets) {
    this.world = world;
    this.scene = scene;
    this.assets = assets;
    this.meshes = new Map();
    this.queue = [];
    this.lastCenter = '';
    this.radius = 5;
    this.material = new THREE.MeshLambertMaterial({
      map: texture,
      vertexColors: true,
      alphaTest: 0.1,
    });
    this.glassMaterial = new THREE.MeshLambertMaterial({
      map: texture,
      vertexColors: true,
      transparent: true,
      opacity: 0.62,
      depthWrite: false,
    });
    this.waterMaterial = new THREE.MeshPhongMaterial({
      map: texture,
      color: 0x74b6bf,
      vertexColors: true,
      transparent: true,
      opacity: 0.67,
      shininess: 90,
      specular: 0xb7dbd2,
      depthWrite: false,
    });
    this.glowMaterial = new THREE.MeshBasicMaterial({ map: texture, vertexColors: true });
  }
  update(x, z, radius = this.radius, all = false) {
    const cx = Math.floor(x / CHUNK),
      cz = Math.floor(z / CHUNK),
      center = `${cx},${cz},${radius}`;
    this.radius = radius;
    if (center !== this.lastCenter) {
      this.lastCenter = center;
      this.queue = [];
      for (let dz = -radius; dz <= radius; dz++)
        for (let dx = -radius; dx <= radius; dx++)
          if (dx * dx + dz * dz <= (radius + 0.4) ** 2) {
            const key = this.world.key(cx + dx, cz + dz);
            if (!this.meshes.has(key))
              this.queue.push({ cx: cx + dx, cz: cz + dz, d: dx * dx + dz * dz });
          }
      this.queue.sort((a, b) => a.d - b.d);
      for (const [key, group] of this.meshes)
        if (
          Math.abs(group.userData.cx - cx) > radius + 1 ||
          Math.abs(group.userData.cz - cz) > radius + 1
        ) {
          this.remove(key);
        }
      this.world.evict(cx, cz, radius + 4);
    }
    for (const key of this.world.dirty)
      if (this.meshes.has(key)) {
        const [dx, dz] = key.split(',').map(Number);
        this.build(dx, dz);
      }
    this.world.dirty.clear();
    const start = performance.now();
    let count = 0;
    while (this.queue.length && (all || (count < 2 && performance.now() - start < 9))) {
      const c = this.queue.shift();
      this.build(c.cx, c.cz);
      count++;
    }
  }
  build(cx, cz) {
    const world = this.world,
      chunk = world.getChunk(cx, cz),
      ox = cx * CHUNK,
      oz = cz * CHUNK,
      key = chunk.key;
    this.remove(key);
    const buckets = [0, 1, 2, 3].map(() => ({
      positions: [],
      normals: [],
      colors: [],
      uvs: [],
      indices: [],
    }));
    const group = new THREE.Group();
    group.position.set(ox, 0, oz);
    group.userData = { cx, cz };
    const get = (x, y, z) =>
      x >= 0 && x < CHUNK && z >= 0 && z < CHUNK && y >= 0 && y < HEIGHT
        ? chunk.data[index(x, y, z)]
        : world.get(ox + x, y, oz + z);
    for (let y = 0; y < HEIGHT; y++)
      for (let z = 0; z < CHUNK; z++)
        for (let x = 0; x < CHUNK; x++) {
          const id = chunk.data[index(x, y, z)];
          if (!id) continue;
          const block = BLOCKS[id];
          if (block.model) {
            const model = this.assets.make(block.model);
            if (model) {
              model.position.set(x + 0.5, y, z + 0.5);
              if (block.bed) model.rotation.y = (-block.facing * Math.PI) / 2;
              group.add(model);
            }
            continue;
          }
          const bucket =
            buckets[
              id === B.WATER ? 2 : id === B.GLASS ? 1 : id === B.LAVA || id === B.GLOWSTONE ? 3 : 0
            ];
          for (let f = 0; f < 6; f++) {
            const face = FACES[f],
              n = face.n,
              neighbor = get(x + n[0], y + n[1], z + n[2]);
            if (
              neighbor === id ||
              (neighbor &&
                (!BLOCKS[neighbor].transparent ||
                  (id === B.WATER && neighbor !== B.AIR && neighbor !== B.GLASS)))
            )
              continue;
            const base = bucket.positions.length / 3,
              uv = uvFor(tileFor(id, f));
            for (let v = 0; v < 4; v++) {
              const vertex = face.v[v];
              let vy = vertex[1];
              if (id === B.WATER && get(x, y + 1, z) !== B.WATER && vy === 1) vy = 0.84;
              bucket.positions.push(x + vertex[0], y + vy, z + vertex[2]);
              bucket.normals.push(...n);
              const axes = [0, 1, 2].filter((a) => n[a] === 0),
                p = [x + n[0], y + n[1], z + n[2]],
                a = [...p],
                b = [...p],
                c = [...p];
              a[axes[0]] += vertex[axes[0]] ? 1 : -1;
              b[axes[1]] += vertex[axes[1]] ? 1 : -1;
              c[axes[0]] = a[axes[0]];
              c[axes[1]] = b[axes[1]];
              const sa = isSolid(get(...a)) ? 1 : 0,
                sb = isSolid(get(...b)) ? 1 : 0,
                sc = isSolid(get(...c)) ? 1 : 0;
              const ao = (sa && sb ? 3 : sa + sb + sc) * 0.105;
              const light = face.shade * (1 - ao);
              bucket.colors.push(light, light, light);
              bucket.uvs.push(v === 0 || v === 3 ? uv[0] : uv[2], v < 2 ? uv[1] : uv[3]);
            }
            bucket.indices.push(base, base + 1, base + 2, base, base + 2, base + 3);
          }
        }
    buckets.forEach((b, i) => {
      if (!b.positions.length) return;
      const geo = new THREE.BufferGeometry();
      geo.setAttribute('position', new THREE.Float32BufferAttribute(b.positions, 3));
      geo.setAttribute('normal', new THREE.Float32BufferAttribute(b.normals, 3));
      geo.setAttribute('color', new THREE.Float32BufferAttribute(b.colors, 3));
      geo.setAttribute('uv', new THREE.Float32BufferAttribute(b.uvs, 2));
      geo.setIndex(b.indices);
      geo.computeBoundingSphere();
      const mesh = new THREE.Mesh(
        geo,
        [this.material, this.glassMaterial, this.waterMaterial, this.glowMaterial][i]
      );
      mesh.receiveShadow = true;
      mesh.castShadow = i === 0;
      mesh.renderOrder = i;
      mesh.userData.chunkGeometry = true;
      group.add(mesh);
    });
    this.scene.add(group);
    this.meshes.set(key, group);
  }
  remove(key) {
    const group = this.meshes.get(key);
    if (!group) return;
    group.traverse((o) => {
      if (o.userData.chunkGeometry) o.geometry.dispose();
    });
    this.scene.remove(group);
    this.meshes.delete(key);
  }
  dispose() {
    for (const key of this.meshes.keys()) this.remove(key);
    this.material.dispose();
    this.glassMaterial.dispose();
    this.waterMaterial.dispose();
    this.glowMaterial.dispose();
  }
}
