import { B } from './blocks.js';

export const isPortal = (id) => id === B.PORTAL_X || id === B.PORTAL_Z;
export function findPortalFrame(world, x, y, z) {
  for (const [dx, dz, id] of [
    [1, 0, B.PORTAL_X],
    [0, 1, B.PORTAL_Z],
  ]) {
    for (let offset = 0; offset <= 3; offset++)
      for (let down = 0; down <= 4; down++) {
        const ox = x - dx * offset,
          oy = y - down,
          oz = z - dz * offset;
        if (oy < 1 || oy + 4 >= 72) continue;
        let valid = true;
        const cells = [];
        for (let u = 0; u < 4 && valid; u++)
          for (let v = 0; v < 5; v++) {
            const block = world.get(ox + dx * u, oy + v, oz + dz * u);
            const corner = (u === 0 || u === 3) && (v === 0 || v === 4);
            const edge = u === 0 || u === 3 || v === 0 || v === 4;
            if (edge && !corner && block !== B.OBSIDIAN) valid = false;
            if (!edge && block !== B.AIR && !isPortal(block)) valid = false;
            if (!edge) cells.push([ox + dx * u, oy + v, oz + dz * u]);
          }
        if (valid) return { x: ox, y: oy, z: oz, dx, dz, id, cells };
      }
  }
  return null;
}
export function lightPortal(world, x, y, z) {
  const frame = findPortalFrame(world, x, y, z);
  if (!frame) return null;
  for (const cell of frame.cells) world.set(...cell, frame.id);
  return frame;
}
export function collapsePortals(world, x, y, z) {
  const queue = [[x, y, z]],
    seen = new Set();
  while (queue.length && seen.size < 128) {
    const [bx, by, bz] = queue.pop(),
      key = `${bx},${by},${bz}`;
    if (seen.has(key)) continue;
    seen.add(key);
    const portal = isPortal(world.get(bx, by, bz));
    if (portal && findPortalFrame(world, bx, by, bz)) continue;
    if (portal) world.set(bx, by, bz, B.AIR);
    if (portal || seen.size === 1)
      for (const [dx, dy, dz] of [
        [1, 0, 0],
        [-1, 0, 0],
        [0, 1, 0],
        [0, -1, 0],
        [0, 0, 1],
        [0, 0, -1],
      ])
        if (isPortal(world.get(bx + dx, by + dy, bz + dz))) queue.push([bx + dx, by + dy, bz + dz]);
  }
}
export function buildPortal(world, x, y, z, dx = 1, dz = 0) {
  for (let u = 0; u < 4; u++)
    for (let v = 0; v < 5; v++)
      world.set(
        x + dx * u,
        y + v,
        z + dz * u,
        u === 0 || u === 3 || v === 0 || v === 4 ? B.OBSIDIAN : dx ? B.PORTAL_X : B.PORTAL_Z
      );
  return { x, y, z, dx, dz, id: dx ? B.PORTAL_X : B.PORTAL_Z };
}
export const portalExit = (p) => ({
  x: p.x + p.dx * 1.5 + 0.5 + p.dz * 1.2,
  y: p.y + 1.05,
  z: p.z + p.dz * 1.5 + 0.5 + p.dx * 1.2,
});
export const bedDirection = (facing) =>
  [
    [0, -1],
    [1, 0],
    [0, 1],
    [-1, 0],
  ][facing];
