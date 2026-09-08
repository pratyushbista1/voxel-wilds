import * as THREE from 'three';
import { B, ITEMS } from './blocks.js';
import { moveBody, raycast, collides, HEIGHT } from './world.js';

export function updatePlayer(g, dt) {
  const p = g.player,
    k = g.keys,
    input = !g.ui.modal;
  g.damageCooldown = Math.max(0, g.damageCooldown - dt);
  g.actionCooldown = Math.max(0, g.actionCooldown - dt);
  g.jumpBuffer = Math.max(0, g.jumpBuffer - dt);
  const sneak = input && (k.has('ShiftLeft') || k.has('ShiftRight'));
  p.eyeHeight = THREE.MathUtils.damp(p.eyeHeight, sneak && !p.flying ? 1.32 : 1.62, 14, dt);
  p.water = g.world.get(p.x, p.y + 0.6, p.z) === B.WATER;
  p.underwater = g.world.get(p.x, p.y + p.eyeHeight, p.z) === B.WATER;
  const ix = input ? Number(k.has('KeyD')) - Number(k.has('KeyA')) : 0;
  const iz = input ? Number(k.has('KeyW')) - Number(k.has('KeyS')) : 0;
  const moving = !!(ix || iz),
    length = Math.hypot(ix, iz) || 1;
  const sprint =
    input &&
    !sneak &&
    iz > 0 &&
    (k.has('ControlLeft') || k.has('ControlRight')) &&
    (p.hunger > 6 || g.mode === 'creative');
  const held = ITEMS[g.hotbar[g.selected]];
  g.blocking =
    input &&
    g.buttons.has(2) &&
    (held?.shield || ITEMS[g.storage.offhand[0]?.id]?.shield) &&
    !held?.food;
  const speed = p.flying
    ? sprint
      ? 17
      : 10
    : p.water
      ? 3.1
      : g.blocking
        ? 1.3
        : sneak
          ? 1.3
          : sprint
            ? 6.6
            : 4.3;
  const vx = ((Math.cos(p.yaw) * ix - Math.sin(p.yaw) * iz) / length) * speed;
  const vz = ((-Math.sin(p.yaw) * ix - Math.cos(p.yaw) * iz) / length) * speed;
  const acceleration = p.grounded || p.flying ? 18 : 9;
  p.velocity.x = THREE.MathUtils.damp(p.velocity.x, vx, acceleration, dt);
  p.velocity.z = THREE.MathUtils.damp(p.velocity.z, vz, acceleration, dt);
  if (p.flying) {
    p.velocity.y = THREE.MathUtils.damp(
      p.velocity.y,
      (Number(input && k.has('Space')) - Number(sneak)) * 8,
      14,
      dt
    );
    p.fallTop = p.y;
  } else if (p.water) {
    p.velocity.y = Math.max(-3, p.velocity.y - dt * 5);
    if (input && k.has('Space')) p.velocity.y = 4.7;
    p.fallTop = p.y;
  } else {
    p.velocity.y = Math.max(-45, p.velocity.y - 25 * dt);
    if (g.jumpBuffer > 0 && p.grounded && input) {
      p.velocity.y = 8.3;
      p.grounded = false;
      g.jumpBuffer = 0;
      p.exhaustion += sprint ? 0.2 : 0.05;
    }
  }
  if (sneak && p.grounded && !p.flying && !p.water) {
    for (const axis of ['x', 'z']) {
      const nx = p.x + (axis === 'x' ? p.velocity.x * dt : 0);
      const nz = p.z + (axis === 'z' ? p.velocity.z * dt : 0);
      if (!collides(g.world, nx, p.y - 0.12, nz)) p.velocity[axis] = 0;
    }
  }
  const oldX = p.x,
    oldZ = p.z;
  p.fallTop = Math.max(p.fallTop ?? p.y, p.y);
  const movement = moveBody(g.world, p, p.velocity, dt, p.flying);
  p.grounded = movement.grounded;
  if (p.grounded) {
    const fall = Math.floor(p.fallTop - p.y - 3);
    if (fall > 0 && !p.water && !p.flying) g.hurt(fall, 'Fell from a high place.');
    p.fallTop = p.y;
  }
  p.y = THREE.MathUtils.clamp(p.y, 0.001, HEIGHT + 70);
  const travelled = Math.hypot(p.x - oldX, p.z - oldZ);
  if (g.mode === 'survival') {
    p.exhaustion += travelled * (sprint ? 0.1 : p.water ? 0.01 : 0);
    while (p.exhaustion >= 4) {
      p.exhaustion -= 4;
      if (p.saturation > 0) p.saturation = Math.max(0, p.saturation - 1);
      else p.hunger = Math.max(0, Math.floor(p.hunger) - 1);
    }
    if ((p.hunger >= 18 && p.health < 20) || p.hunger <= 0) p.regen += dt;
    else p.regen = 0;
    if (p.regen >= 4) {
      p.regen = 0;
      if (p.hunger >= 18) {
        p.health = Math.min(20, p.health + 1);
        p.exhaustion += 6;
      } else if (p.health > 1) g.hurt(1, 'Starved.');
      g.ui.update();
    }
    p.breath = p.underwater ? p.breath - dt : 12;
    if (p.breath < 0) {
      p.breath = 1;
      g.hurt(2, 'Drowned.');
    }
    if (g.world.get(p.x, p.y, p.z) === B.CAMPFIRE) g.hurt(1, 'Stood in a campfire.');
    if (input && g.buttons.has(2) && held?.food && p.hunger < 20) {
      g.eatTime += dt;
      if (g.eatTime >= 1.6) {
        g.storage.take('bag', g.selected, 1);
        p.hunger = Math.min(20, p.hunger + held.food);
        p.saturation = Math.min(p.hunger, p.saturation + held.food * held.saturation * 2);
        g.eatTime = 0;
        g.sound.play('eat');
        g.inventoryChanged();
      }
    } else g.eatTime = 0;
  }
  const walkTarget =
    moving && (p.grounded || p.water) ? Math.min(1.5, travelled / Math.max(dt, 0.001) / 4.3) : 0;
  g.walkBlend = THREE.MathUtils.damp(g.walkBlend, walkTarget, 9, dt);
  g.bob += dt * (3 + speed * 1.1);
  if (travelled > 0.005 && (p.grounded || p.water)) {
    p.stepTime += travelled;
    if (p.stepTime > 1.85) {
      p.stepTime = 0;
      g.sound.play(p.water ? 'water' : 'step');
    }
  }
  const bob = Math.sin(g.bob * 2) * 0.024 * g.walkBlend;
  g.camera.position.set(p.x, p.y + p.eyeHeight + bob, p.z);
  g.camera.rotation.set(p.pitch, p.yaw, Math.sin(g.bob) * 0.002 * g.walkBlend, 'YXZ');
  const fov = g.settings.fov + (sprint && moving ? 4 : 0);
  if (Math.abs(g.camera.fov - fov) > 0.01) {
    g.camera.fov = THREE.MathUtils.damp(g.camera.fov, fov, 7, dt);
    g.camera.updateProjectionMatrix();
  }
  g.camera.updateMatrixWorld();
  g.target = input
    ? raycast(
        g.world,
        g.camera.position,
        g.camera.getWorldDirection(new THREE.Vector3()),
        g.mode === 'creative' ? 5 : 4.5
      )
    : null;
  g.outline.visible = !!g.target;
  if (g.target) g.outline.position.set(g.target.x + 0.5, g.target.y + 0.5, g.target.z + 0.5);
  if (input) {
    g.mine(dt);
    if (g.buttons.has(2) && !held?.food && !g.blocking && g.actionCooldown <= 0) g.place();
  }
  if (g.playTime - g.lastSave > 20 && !g.saving) {
    g.lastSave = g.playTime;
    g.save(true).catch(() => g.ui.toast('Autosave failed. Keep this session open.', null, true));
  }
  g.sound.ambient(g.playTime, g.atmosphere.lightLevel > 0.4);
}

export function animateHand(g, dt) {
  g.swingPhase = Math.min(1, g.swingPhase + dt / 0.32);
  const swing = Math.sin(g.swingPhase * Math.PI);
  const arc = Math.sin(Math.sqrt(g.swingPhase) * Math.PI);
  const eat = g.eatTime > 0 ? Math.min(1, g.eatTime * 4) : 0;
  const block = g.blocking ? 1 : 0;
  g.sway.multiplyScalar(Math.exp(-dt * 10));
  g.heldRoot.position.set(
    0.49 - arc * 0.19 + Math.sin(g.bob) * 0.018 * g.walkBlend - g.sway.x - eat * 0.2 - block * 0.28,
    -0.58 -
      swing * 0.11 +
      Math.cos(g.bob * 2) * 0.016 * g.walkBlend +
      g.sway.y +
      eat * (0.18 + Math.sin(g.eatTime * 30) * 0.025) +
      block * 0.13,
    -0.9 + arc * 0.15 + eat * 0.1
  );
  g.heldRoot.rotation.set(
    -swing * 0.82 + eat * 0.5,
    -arc * 0.23 + g.sway.x * 3,
    0.08 + swing * 0.32 - eat * 0.45 - block * 0.45
  );
}
