import { B } from './blocks.js';

export function netherStructures(x, z, radius = 90) {
  const result = [];
  for (let rz = Math.floor((z - radius) / 160); rz <= Math.floor((z + radius) / 160); rz++) {
    for (let rx = Math.floor((x - radius) / 160); rx <= Math.floor((x + radius) / 160); rx++) {
      result.push({ kind: 'fortress', x: rx * 160 + 48, y: 34, z: rz * 160 + 40 });
      result.push({ kind: 'bastion', x: rx * 160 + 108, y: 32, z: rz * 160 + 108 });
    }
  }
  return result.filter((s) => Math.abs(s.x - x) < radius + 28 && Math.abs(s.z - z) < radius + 28);
}

export function decorateNether(cx, cz, put) {
  for (const s of netherStructures(cx * 16 + 8, cz * 16 + 8, 40)) {
    const box = (x1, y1, z1, x2, y2, z2, id) => {
      for (let z = Math.max(z1 + s.z, cz * 16); z <= Math.min(z2 + s.z, cz * 16 + 15); z++)
        for (let x = Math.max(x1 + s.x, cx * 16); x <= Math.min(x2 + s.x, cx * 16 + 15); x++)
          for (let y = y1 + s.y; y <= y2 + s.y; y++) put(x, y, z, id);
    };
    if (s.kind === 'fortress') {
      box(-23, 0, -3, 23, 0, 3, B.NETHER_BRICKS);
      box(-3, 0, -23, 3, 0, 23, B.NETHER_BRICKS);
      box(-23, 1, -2, 23, 4, 2, B.AIR);
      box(-2, 1, -23, 2, 4, 23, B.AIR);
      for (const v of [-3, 3]) {
        box(-23, 1, v, 23, 1, v, B.NETHER_BRICKS);
        box(v, 1, -23, v, 1, 23, B.NETHER_BRICKS);
      }
      for (const x of [-16, 0, 16])
        for (const z of [-2, 2]) box(x, -18, z, x + 1, -1, z + 1, B.NETHER_BRICKS);
      box(-7, 0, -7, 7, 7, 7, B.NETHER_BRICKS);
      box(-6, 1, -6, 6, 6, 6, B.AIR);
      box(-2, 1, -7, 2, 3, 7, B.AIR);
      box(-7, 1, -2, 7, 3, 2, B.AIR);
      box(-5, 1, -5, -5, 1, -5, B.CHEST);
      box(4, 0, -5, 5, 0, -3, B.SOUL_SAND);
      box(4, 1, -5, 5, 1, -3, B.NETHER_WART);
      box(-4, 0, 19, 4, 0, 26, B.NETHER_BRICKS);
      box(0, 1, 23, 0, 1, 23, B.SPAWNER);
      for (let i = 0; i < 14; i++) box(-2, -i, -24 - i, 2, -i, -24 - i, B.NETHER_BRICKS);
    } else {
      box(-12, -14, -12, 12, 0, 12, B.BLACKSTONE);
      box(-11, 1, -11, 11, 9, 11, B.AIR);
      for (const v of [-12, 12]) {
        box(v, 1, -12, v, 10, 12, B.BLACKSTONE);
        box(-12, 1, v, 12, 10, v, B.BLACKSTONE);
      }
      box(-2, 1, -12, 2, 4, -10, B.AIR);
      box(-10, 5, -10, -7, 5, 10, B.BLACKSTONE);
      box(7, 5, -10, 10, 5, 10, B.BLACKSTONE);
      box(-10, 5, 7, 10, 5, 10, B.BLACKSTONE);
      for (const x of [-11, 10])
        for (const z of [-11, 10]) box(x, 1, z, x + 1, 13, z + 1, B.BLACKSTONE);
      box(-3, 0, -3, 3, 0, 3, B.LAVA);
      box(-1, 0, -1, 1, 1, 1, B.GOLD_BLOCK);
      box(0, 2, 0, 0, 2, 0, B.CHEST);
      box(-9, 6, 8, -9, 6, 8, B.CHEST);
      for (let i = 0; i < 10; i++) box(-2, -i, -13 - i, 2, -i, -13 - i, B.BLACKSTONE);
      for (let i = 0; i < 5; i++) box(-10, i + 1, -8 + i, -8, i + 1, -8 + i, B.BLACKSTONE);
      box(12, 7, -5, 12, 10, 0, B.AIR);
      box(-12, 8, 3, -12, 10, 8, B.AIR);
    }
  }
}
