import * as THREE from 'three';
import { B, BLOCKS, ITEMS, isSolid, tileFor } from './blocks.js';
import { Inventory, makeStack, maxStack } from './inventory.js';
import { makeFurnace, tickFurnace } from './furnace.js';
import { Drops } from './drops.js';
import { updatePlayer, animateHand } from './player.js';
import { World, raycast, collides, moveBody, CHUNK, HEIGHT } from './world.js';
import { createAtlas, makeIcons, uvFor } from './textures.js';
import { ChunkRenderer } from './mesher.js';
import { Assets, Atmosphere, Particles } from './assets.js';
import { Mobs } from './mobs.js';
import { Survival } from './survival.js';
import { collapsePortals } from './portals.js';
import { VIDEO_DEFAULTS, applyVideo } from './settings.js';
import { Sound } from './audio.js';
import { UI, $ } from './ui.js';

const nextFrame = () => new Promise((resolve) => requestAnimationFrame(resolve));
const clamp = THREE.MathUtils.clamp;

class Game {
  constructor() {
    this.canvas = $('game-canvas');
    this.renderer = new THREE.WebGLRenderer({
      canvas: this.canvas,
      antialias: true,
      powerPreference: 'high-performance',
    });
    this.renderer.setPixelRatio(Math.min(devicePixelRatio, 1.75));
    this.renderer.setSize(innerWidth, innerHeight);
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFShadowMap;
    this.renderer.info.autoReset = false;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.12;
    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(75, innerWidth / innerHeight, 0.06, 300);
    this.camera.rotation.order = 'YXZ';
    this.assets = new Assets();
    this.sound = new Sound();
    this.atlas = createAtlas();
    this.icons = makeIcons(this.atlas.canvas);
    this.settings = {
      ...VIDEO_DEFAULTS,
      sensitivity: 45,
      distance: 5,
      fov: 75,
      volume: 40,
      shadows: true,
      showFps: false,
    };
    this.hotbar = [1, 2, 5, 7, 8, 9, 17, 21, 102];
    this.selected = 0;
    this.inventory = {};
    this.storage = new Inventory();
    this.furnaces = new Map();
    this.inventoryKind = 'personal';
    this.walkBlend = 0;
    this.sway = new THREE.Vector2();
    this.swingPhase = 1;
    this.eatTime = 0;
    this.jumpBuffer = 0;
    this.mode = 'creative';
    this.time = 400;
    this.playTime = 0;
    this.stats = { mined: 0, placed: 0, logs: 0, crafted: 0, flown: false };
    this.active = false;
    this.paused = true;
    this.keys = new Set();
    this.buttons = new Set();
    this.target = null;
    this.mining = { key: null, progress: 0 };
    this.actionCooldown = 0;
    this.bob = 0;
    this.elapsed = 0;
    this.fps = 60;
    this.frameCount = 0;
    this.frameTime = 0;
    this.lastHud = 0;
    this.lastSave = 0;
    this.lastSaveRevision = -1;
    this.saveError = false;
    this.saving = false;
    this.damageTimer = 0;
    this.damageCooldown = 0;
    this.systems = new Survival(this);
    this.ui = new UI(this, this.icons);
    this.player = this.newPlayer(new World('wildflower').spawn());
    const outlineGeo = new THREE.EdgesGeometry(new THREE.BoxGeometry(1.006, 1.006, 1.006));
    this.outline = new THREE.LineSegments(
      outlineGeo,
      new THREE.LineBasicMaterial({ color: 0xe9f1c6, transparent: true, opacity: 0.7 })
    );
    this.outline.visible = false;
    this.scene.add(this.outline);
    this.crackCanvas = document.createElement('canvas');
    this.crackCanvas.width = 32;
    this.crackCanvas.height = 32;
    this.crackTexture = new THREE.CanvasTexture(this.crackCanvas);
    this.crackTexture.magFilter = THREE.NearestFilter;
    this.crackMesh = new THREE.Mesh(
      new THREE.BoxGeometry(1.008, 1.008, 1.008),
      new THREE.MeshBasicMaterial({
        map: this.crackTexture,
        transparent: true,
        depthWrite: false,
        polygonOffset: true,
        polygonOffsetFactor: -1,
      })
    );
    this.crackMesh.visible = false;
    this.scene.add(this.crackMesh);
    this.viewScene = new THREE.Scene();
    this.viewCamera = new THREE.PerspectiveCamera(65, innerWidth / innerHeight, 0.01, 10);
    this.heldRoot = new THREE.Group();
    this.viewScene.add(this.heldRoot, new THREE.HemisphereLight(0xeef3dc, 0x685744, 2.5));
    const handLight = new THREE.DirectionalLight(0xffe2ad, 2);
    handLight.position.set(-2, 4, 3);
    this.viewScene.add(handLight);
    this.handMaterial = new THREE.MeshLambertMaterial({ color: 0xcda476 });
    this.sleeveMaterial = new THREE.MeshLambertMaterial({ color: 0x637e5b });
    this.heldBlockMaterial = new THREE.MeshLambertMaterial({ map: this.atlas.texture });
    this.blockLights = Array.from({ length: 5 }, () => {
      const light = new THREE.PointLight(0xffb556, 0, 12, 1.4);
      this.scene.add(light);
      return light;
    });
    this.bind();
    this.applySettings();
  }
  newPlayer(pos) {
    return {
      ...pos,
      yaw: -0.45,
      pitch: -0.12,
      health: 20,
      hunger: 20,
      saturation: 5,
      exhaustion: 0,
      eyeHeight: 1.62,
      fallTop: pos.y,
      velocity: new THREE.Vector3(),
      grounded: false,
      flying: false,
      underwater: false,
      water: false,
      breath: 12,
      regen: 0,
      stepTime: 0,
    };
  }
  async init() {
    $('load-status').textContent = 'Loading models...';
    await this.assets.load((progress) => {
      $('load-fill').style.width = `${10 + progress * 30}%`;
    });
    await this.setupWorld('wildflower', null, true);
    $('load-status').textContent = 'Loading world...';
    $('load-fill').style.width = '95%';
    await this.ui.fetchWorlds();
    this.refreshHeld();
    this.tick(performance.now());
    await nextFrame();
    $('loading').classList.add('hidden');
    $('menu').classList.remove('hidden');
  }
  async setupWorld(seed, saved = null, preview = false) {
    this.generating = true;
    $('toast-stack').replaceChildren();
    this.damageTimer = 0;
    this.chunks?.dispose();
    this.atmosphere?.dispose();
    this.creatures?.dispose();
    this.particles?.dispose();
    this.drops?.dispose();
    this.world = new World(seed, saved?.edits || [], this.dimension);
    if (!saved && this.dimension === 'overworld') {
      for (const [x, z, id] of [
        [-1, -1, B.CAMPFIRE],
        [-4, -2, B.WORKBENCH],
        [-6, 1, B.LOG],
      ]) {
        const y = this.world.surface(x, z);
        this.world.set(x, y, z, id);
        if (id === B.LOG) this.world.set(x, y + 1, z, B.LANTERN);
      }
    }
    this.atmosphere = new Atmosphere(this.scene, this.world);
    this.creatures = new Mobs(this.scene, this.world, this.assets, this);
    this.creatures.populate(saved?.mobs);
    this.particles = new Particles(this.scene);
    this.drops = new Drops(this.scene, this.world, this.icons);
    for (const d of saved?.drops || []) this.drops.spawn(d.stack, d, { x: 0, y: 0, z: 0 }, d.age);
    this.furnaces = new Map(saved?.furnaces || []);
    this.chunks = new ChunkRenderer(this.world, this.scene, this.atlas.texture, this.assets);
    this.player = this.newPlayer(saved?.player || this.world.spawn());
    if (saved) {
      this.player.yaw = saved.player.yaw;
      this.player.pitch = clamp(saved.player.pitch, -1.55, 1.55);
      this.player.health = clamp(saved.player.health, 0, 20);
      this.player.hunger = clamp(saved.player.hunger, 0, 20);
      this.player.saturation = clamp(saved.player.saturation ?? 5, 0, this.player.hunger);
      this.player.exhaustion = clamp(saved.player.exhaustion ?? 0, 0, 4);
      this.player.flying = saved.mode === 'creative' && !!saved.player.flying;
      if (collides(this.world, this.player.x, this.player.y, this.player.z)) {
        this.player.y = this.world.surface(this.player.x, this.player.z) + 0.05;
      }
    }
    const center = preview ? { x: 7, z: 7 } : this.player;
    this.chunks.update(center.x, center.z, 3);
    let pass = 0;
    while (this.chunks.queue.length) {
      this.chunks.update(center.x, center.z, 3);
      $('load-fill').style.width = `${40 + Math.min(50, ++pass * 2)}%`;
      $('load-status').textContent = 'Generating terrain...';
      await nextFrame();
    }
    this.chunks.lastCenter = '';
    this.mining = { key: null, progress: 0 };
    this.target = null;
    this.clearInput();
    this.generating = false;
    this.applySettings();
  }
  async create(name, seed, mode) {
    this.systems.reset();
    await this.sound.start();
    this.paused = true;
    this.active = false;
    this.ui.hide();
    $('menu').classList.add('hidden');
    $('loading').classList.remove('hidden');
    $('load-fill').style.width = '10%';
    this.id = `world-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 7)}`;
    this.name = name;
    this.mode = mode;
    this.time = 400;
    this.playTime = 0;
    this.lastSave = 0;
    this.stats = { mined: 0, placed: 0, logs: 0, crafted: 0, flown: false };
    this.storage = new Inventory();
    const initial =
      mode === 'creative' ? [1, 5, 7, 8, 9, 10, 17, 21, 102] : [100, 0, 0, 0, 0, 0, 0, 0, 93];
    initial.forEach(
      (id, i) =>
        (this.storage.slots[i] = makeStack(id, mode === 'creative' ? 64 : id === 93 ? 6 : 1))
    );
    this.syncInventory();
    this.selected = 0;
    await this.setupWorld(seed);
    this.active = true;
    this.refreshHeld();
    this.ui.refreshItems();
    this.ui.update();
    $('loading').classList.add('hidden');
    $('hud').classList.remove('hidden');
    await this.save();
    this.capture();
    this.ui.toast(
      'World ready.',
      mode === 'creative'
        ? 'E: inventory. Double-tap Space: fly.'
        : 'You have a wooden pickaxe and 6 berries.'
    );
  }
  async load(id) {
    await this.sound.start();
    const response = await fetch(`/api/worlds/${encodeURIComponent(id)}`);
    if (!response.ok)
      throw Error('Could not load this world. Your save files are still in the saves folder.');
    const saved = await response.json();
    this.systems.reset(saved);
    this.paused = true;
    this.active = false;
    this.ui.hide();
    $('menu').classList.add('hidden');
    $('loading').classList.remove('hidden');
    $('load-fill').style.width = '15%';
    this.id = saved.id;
    this.name = saved.name;
    this.mode = saved.mode;
    this.storage = saved.inventoryData
      ? new Inventory(saved.inventoryData)
      : Inventory.migrate(
          saved.mode === 'creative'
            ? Object.fromEntries(saved.hotbar.filter(Boolean).map((id) => [id, maxStack(id)]))
            : saved.inventory,
          saved.hotbar
        );
    this.syncInventory();
    this.selected = clamp(saved.selected || 0, 0, 8);
    this.time = saved.time;
    this.playTime = saved.playTime || 0;
    this.stats = { mined: 0, placed: 0, logs: 0, crafted: 0, flown: false, ...saved.stats };
    this.lastSave = this.playTime;
    if (saved.settings) {
      for (const [key, value] of Object.entries(saved.settings))
        if (key in this.settings && typeof value === typeof this.settings[key])
          this.settings[key] = value;
      this.settings.distance = clamp(this.settings.distance, 3, 12);
      this.settings.fov = clamp(this.settings.fov, 60, 110);
      this.settings.sensitivity = clamp(this.settings.sensitivity, 10, 100);
      this.settings.volume = clamp(this.settings.volume, 0, 100);
      this.syncSettingsUI();
      this.applySettings();
    }
    await this.setupWorld(saved.seed, saved);
    for (const stack of this.storage.close()) this.dropStack(stack);
    this.active = true;
    this.lastSaveRevision = this.world.revision;
    this.refreshHeld();
    this.ui.refreshItems();
    this.ui.update();
    $('loading').classList.add('hidden');
    $('hud').classList.remove('hidden');
    if (this.player.health <= 0) this.showDeath('Returning to your respawn point.');
    else this.capture();
    this.ui.toast(`Welcome back to ${this.name}.`);
  }
  syncSettingsUI() {
    for (const [id, key, suffix] of [
      ['sensitivity', 'sensitivity', '%'],
      ['view-distance', 'distance', ' chunks'],
      ['fov', 'fov', '°'],
      ['volume', 'volume', '%'],
      ['render-scale', 'renderScale', '%'],
      ['brightness', 'brightness', '%'],
      ['fog-distance', 'fogDistance', '%'],
      ['entity-distance', 'entityDistance', ' blocks'],
    ]) {
      $(id).value = this.settings[key];
      $(id + '-value').textContent = this.settings[key] + suffix;
    }
    $('shadows').checked = this.settings.shadows;
    $('show-fps').checked = this.settings.showFps;
    for (const [id, key] of [
      ['clouds', 'clouds'],
      ['view-bobbing', 'viewBobbing'],
      ['vignette-setting', 'vignette'],
    ])
      $(id).checked = this.settings[key];
    for (const [id, key] of [
      ['max-fps', 'maxFps'],
      ['shadow-size', 'shadowSize'],
      ['particles', 'particles'],
      ['difficulty', 'difficulty'],
    ])
      $(id).value = String(this.settings[key]);
  }
  snapshot() {
    const p = this.player;
    return {
      version: 3,
      ...this.systems.snapshot(),
      id: this.id,
      name: this.name,
      seed: this.world.seed,
      mode: this.mode,
      edits: this.world.serialize(),
      player: {
        x: p.x,
        y: p.y,
        z: p.z,
        yaw: p.yaw,
        pitch: p.pitch,
        health: p.health,
        hunger: p.hunger,
        saturation: p.saturation,
        exhaustion: p.exhaustion,
        flying: p.flying,
      },
      inventory: { ...this.inventory },
      hotbar: [...this.hotbar],
      inventoryData: this.storage.serialize(),
      furnaces: [...this.furnaces],
      drops: this.drops.serialize(),
      selected: this.selected,
      time: this.time,
      playTime: this.playTime,
      stats: { ...this.stats },
      settings: { ...this.settings },
    };
  }
  async save(silent = false) {
    if (!this.active || !this.id) return;
    if (this.saving) {
      await this.saving;
      return this.save(silent);
    }
    const revision = this.world.revision,
      snapshot = this.snapshot();
    this.saving = (async () => {
      try {
        const response = await fetch(`/api/worlds/${this.id}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(snapshot),
        });
        if (!response.ok) {
          const error = await response.json();
          throw Error(error.error || 'Save failed.');
        }
        this.saveError = false;
        this.lastSave = this.playTime;
        this.lastSaveRevision = revision;
        $('save-indicator').lastElementChild.textContent = 'World saved';
        $('save-indicator').style.opacity = '1';
        clearTimeout(this.savedTimer);
        this.savedTimer = setTimeout(() => {
          $('save-indicator').style.opacity = '0';
        }, 2400);
      } catch (error) {
        this.saveError = true;
        $('save-indicator').lastElementChild.textContent = 'Save failed. Keep this session open.';
        $('save-indicator').style.opacity = '1';
        if (!silent)
          this.ui.toast('Could not save your world. Keep this session open.', error.message, true);
        throw error;
      }
    })();
    try {
      await this.saving;
    } finally {
      this.saving = null;
    }
  }
  async quit() {
    const button = $('save-quit-button');
    button.disabled = true;
    try {
      await this.save();
      this.paused = true;
      this.active = false;
      this.clearInput();
      this.ui.hide();
      if (document.pointerLockElement) document.exitPointerLock();
      $('hud').classList.add('hidden');
      $('menu').classList.remove('hidden');
      await this.ui.fetchWorlds();
    } finally {
      button.disabled = false;
    }
  }
  capture() {
    if (!this.active) return;
    this.paused = true;
    this.clearInput();
    this.sound.start().catch(() => {});
    const request = this.canvas.requestPointerLock();
    if (request?.catch)
      request.catch(() => {
        if (!document.pointerLockElement) {
          this.paused = true;
          this.ui.show('pause-modal');
          $('pause-save-status').textContent = 'Click Resume to enable mouse look.';
        }
      });
  }
  pause() {
    if (!this.active) return;
    this.paused = true;
    if (this.ui.modal !== 'death-modal') this.ui.show('pause-modal');
    this.clearInput();
    this.save(true).catch(() => {});
  }
  clearInput() {
    this.keys.clear();
    this.buttons.clear();
    this.jumpBuffer = 0;
    this.eatTime = 0;
    this.mining.progress = 0;
    this.mining.key = null;
    this.crackMesh && (this.crackMesh.visible = false);
    $('mining-progress').style.display = 'none';
  }
  bind() {
    window.addEventListener('resize', () => {
      this.renderer.setSize(innerWidth, innerHeight);
      this.camera.aspect = innerWidth / innerHeight;
      this.camera.updateProjectionMatrix();
      this.viewCamera.aspect = this.camera.aspect;
      this.viewCamera.updateProjectionMatrix();
    });
    document.addEventListener('pointerlockchange', () => {
      const locked = document.pointerLockElement === this.canvas;
      if (locked) {
        this.paused = false;
        this.ui.hide();
      } else if (this.active && !this.ui.modal) this.pause();
      this.clearInput();
    });
    this.canvas.addEventListener('click', () => {
      if (this.active && this.paused && !this.ui.modal) this.capture();
    });
    document.addEventListener('mousemove', (event) => {
      if (!this.active || this.paused || document.pointerLockElement !== this.canvas) return;
      const sensitivity = this.settings.sensitivity * 0.000055;
      this.player.yaw -= event.movementX * sensitivity;
      this.player.pitch = clamp(this.player.pitch - event.movementY * sensitivity, -1.55, 1.55);
      this.sway.x = clamp(this.sway.x + event.movementX * 0.0008, -0.055, 0.055);
      this.sway.y = clamp(this.sway.y + event.movementY * 0.0008, -0.04, 0.04);
    });
    document.addEventListener('keydown', (event) => {
      if (['INPUT', 'TEXTAREA'].includes(event.target.tagName)) {
        if (event.code === 'Escape') event.target.blur();
        else return;
      }
      if (!this.active) {
        if (event.code === 'Escape' && this.ui.modal) this.ui.close();
        return;
      }
      if (this.player.health <= 0 || this.systems.transition) return;
      if (this.systems.sleepTimer) {
        if (event.code === 'Escape') this.systems.wake();
        return;
      }
      if (
        [
          'Space',
          'KeyW',
          'KeyA',
          'KeyS',
          'KeyD',
          'ShiftLeft',
          'ShiftRight',
          'KeyE',
          'KeyF',
          'KeyG',
          'KeyQ',
          'KeyH',
          'Escape',
        ].includes(event.code) ||
        event.code.startsWith('Digit')
      )
        event.preventDefault();
      if (event.repeat) return;
      if (event.code === 'Escape') {
        if (this.ui.modal && this.ui.modal !== 'pause-modal') this.ui.close();
        else if (this.ui.modal === 'pause-modal') {
          this.ui.hide();
          this.capture();
        } else this.pause();
        return;
      }
      if (event.code === 'KeyE') {
        if (this.ui.modal === 'inventory-modal') this.ui.close();
        else if (!this.paused) this.openInventory();
        return;
      }
      if (event.code === 'KeyH') {
        if (this.ui.modal === 'guide-modal') this.ui.close();
        else if (!this.paused) this.ui.show('guide-modal');
        return;
      }
      if (this.ui.modal === 'inventory-modal') {
        this.ui.inventoryView.key(event);
        return;
      }
      if (event.code.startsWith('Digit')) {
        const n = Number(event.code.slice(5)) - 1;
        if (n >= 0 && n < 9) {
          this.selected = n;
          this.ui.renderHotbar();
          this.refreshHeld();
        }
        return;
      }
      if (this.paused) return;
      if (this.ui.modal) return;
      if (event.code === 'KeyF') {
        [this.storage.slots[this.selected], this.storage.offhand[0]] = [
          this.storage.offhand[0],
          this.storage.slots[this.selected],
        ];
        this.inventoryChanged();
      }
      if (event.code === 'KeyQ') {
        this.dropStack(this.storage.take('bag', this.selected, event.ctrlKey ? Infinity : 1), true);
        this.inventoryChanged();
      }
      if (event.code === 'Space') {
        this.jumpBuffer = 0.15;
        if (this.mode === 'creative' && this.elapsed - (this.lastJumpPress ?? -1) < 0.28)
          this.toggleFlight();
        this.lastJumpPress = this.elapsed;
      }
      if (event.code === 'KeyG' && this.mode === 'creative') this.toggleFlight();
      this.keys.add(event.code);
    });
    document.addEventListener('keyup', (event) => this.keys.delete(event.code));
    document.addEventListener('mousedown', (event) => {
      if (
        !this.active ||
        this.paused ||
        this.ui.modal ||
        event.target !== this.canvas ||
        document.pointerLockElement !== this.canvas
      )
        return;
      event.preventDefault();
      this.buttons.add(event.button);
      if (event.button === 2) this.place();
      else if (event.button === 1) this.pickBlock();
    });
    document.addEventListener('mouseup', (event) => {
      this.buttons.delete(event.button);
      if (event.button === 2) this.eatTime = 0;
      if (event.button === 0) {
        this.mining.progress = 0;
        this.mining.key = null;
      }
    });
    this.canvas.addEventListener('contextmenu', (event) => event.preventDefault());
    this.canvas.addEventListener(
      'wheel',
      (event) => {
        if (!this.active || this.paused || this.ui.modal) return;
        event.preventDefault();
        this.selected = (this.selected + (event.deltaY > 0 ? 1 : -1) + 9) % 9;
        this.eatTime = 0;
        this.ui.renderHotbar();
        this.refreshHeld();
      },
      { passive: false }
    );
    window.addEventListener('blur', () => {
      if (this.active && !this.paused) this.pause();
      this.clearInput();
    });
    document.addEventListener('visibilitychange', () => {
      if (document.hidden && this.active) this.pause();
    });
    window.desktop?.onCloseRequested(async () => {
      this.closing = true;
      this.pause();
      try {
        await this.save();
        window.desktop.finishClose(true);
      } catch {
        this.closing = false;
        window.desktop.finishClose(false);
      }
    });
    window.addEventListener('beforeunload', (event) => {
      if (!window.desktop && this.active) {
        const text = JSON.stringify(this.snapshot());
        if (text.length < 60000)
          fetch(`/api/worlds/${this.id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: text,
            keepalive: true,
          }).catch(() => {});
        event.preventDefault();
        event.returnValue = '';
      }
    });
  }
  toggleFlight() {
    this.player.flying = !this.player.flying;
    this.player.velocity.y = 0;
    this.stats.flown = true;
    this.ui.toast(
      this.player.flying ? 'Flight enabled.' : 'Flight disabled.',
      this.player.flying ? 'Space to rise. Shift to descend.' : null
    );
    this.ui.update();
  }
  applySettings() {
    applyVideo(this);
    this.renderer.shadowMap.enabled = this.settings.shadows;
    this.camera.fov = this.settings.fov;
    this.camera.updateProjectionMatrix();
    this.sound.setVolume(this.settings.volume / 100);
    $('fps-counter').classList.toggle('hidden', !this.settings.showFps);
    if (this.chunks) this.chunks.lastCenter = '';
  }
  addItem(id, count = 1, notify = true) {
    if (!ITEMS[id] || !id) return;
    for (let n = count; n > 0; n -= maxStack(id))
      this.dropStack(this.storage.insert(makeStack(id, n)));
    if (notify) this.ui.pickup(id, count);
    this.inventoryChanged();
  }
  syncInventory() {
    this.inventory = this.storage.totals();
    this.hotbar = this.storage.slots.slice(0, 9).map((stack) => stack?.id || 0);
  }
  inventoryChanged() {
    this.syncInventory();
    this.ui.refreshItems();
    this.refreshHeld();
    this.ui.update();
  }
  openInventory(kind = 'personal', target = null) {
    this.inventoryKind = kind;
    this.storage.openGrid(kind === 'table' ? 3 : 2);
    this.storage.furnace = null;
    this.storage.chest = null;
    this.stationKey = null;
    if (kind === 'furnace') {
      this.stationKey = `${target.x},${target.y},${target.z}`;
      if (!this.furnaces.has(this.stationKey)) this.furnaces.set(this.stationKey, makeFurnace());
      this.storage.furnace = this.furnaces.get(this.stationKey);
    }
    if (kind === 'chest') this.storage.chest = this.systems.chest(target);
    this.ui.show('inventory-modal');
  }
  closeInventory() {
    for (const stack of this.storage.close()) this.dropStack(stack);
    this.storage.furnace = null;
    this.storage.chest = null;
    this.inventoryChanged();
    this.save(true).catch(() => {});
  }
  craft(recipe, all = false) {
    if (!this.storage.fillRecipe(recipe, all))
      this.ui.toast('Not enough materials for this recipe.');
    this.inventoryChanged();
  }
  dropStack(stack, thrown = false) {
    if (!stack || !this.drops) return;
    const p = this.player,
      dir = new THREE.Vector3(-Math.sin(p.yaw), 0.2, -Math.cos(p.yaw));
    this.drops.spawn(
      stack,
      { x: p.x + dir.x * 0.45, y: p.y + 1.2, z: p.z + dir.z * 0.45 },
      thrown ? dir.multiplyScalar(4) : null
    );
  }
  wearTool(amount = 1) {
    if (this.mode !== 'survival' || !ITEMS[this.hotbar[this.selected]]?.tool) return;
    if (this.storage.damageTool(this.selected, amount)) {
      this.sound.play('mine');
      this.ui.toast('Your tool broke.');
    }
    this.inventoryChanged();
  }
  startSwing() {
    if (this.swingPhase >= 1) this.swingPhase = 0;
  }
  pickBlock() {
    if (this.mode !== 'creative' || !this.target) return;
    const id = this.target.id;
    if (id === B.BEDROCK) return;
    const existing = this.hotbar.indexOf(id);
    if (existing >= 0) this.selected = existing;
    else this.storage.slots[this.selected] = makeStack(id, 64);
    this.inventoryChanged();
  }
  place() {
    if (this.actionCooldown > 0 || this.systems.sleepTimer || this.systems.transition) return;
    if (
      this.target &&
      this.world.get(this.target.x, this.target.y, this.target.z) !== this.target.id
    )
      this.target = null;
    if (this.systems.interact(this.target, ITEMS[this.hotbar[this.selected]])) return;
    if (this.target && !this.keys.has('ShiftLeft') && !this.keys.has('ShiftRight')) {
      if (this.target.id === B.WORKBENCH) {
        this.openInventory('table', this.target);
        return;
      }
      if (this.target.id === B.FURNACE) {
        this.openInventory('furnace', this.target);
        return;
      }
    }
    const id = this.hotbar[this.selected],
      item = ITEMS[id];
    if (!item) return;
    if (item.food || item.shield) return;
    if (!BLOCKS[id] || !this.target) return;
    if (this.mode === 'survival' && !(this.inventory[id] > 0)) {
      this.ui.toast('Gather some more ' + item.name.toLowerCase() + '.');
      return;
    }
    const t = this.target,
      x = t.x + t.normal.x,
      y = t.y + t.normal.y,
      z = t.z + t.normal.z,
      p = this.player;
    if (y <= 0 || y >= HEIGHT) {
      this.ui.toast('Building height limit reached.');
      return;
    }
    const existing = this.world.get(x, y, z);
    if (id === B.NETHER_WART && this.world.get(x, y - 1, z) !== B.SOUL_SAND) {
      this.ui.toast('Nether wart needs soul sand.');
      return;
    }
    if (existing !== B.AIR && existing !== B.WATER) return;
    if (
      isSolid(id) &&
      x + 1 > p.x - 0.29 &&
      x < p.x + 0.29 &&
      y + 1 > p.y + 0.01 &&
      y < p.y + 1.78 &&
      z + 1 > p.z - 0.29 &&
      z < p.z + 0.29
    )
      return;
    if (item.bed ? this.systems.placeBed(x, y, z) : this.world.set(x, y, z, id)) {
      if (id === B.CHEST) this.containers.set(`${x},${y},${z}`, Array(27).fill(null));
      if (this.mode === 'survival') this.storage.take('bag', this.selected, 1);
      this.stats.placed++;
      this.actionCooldown = 0.16;
      this.startSwing();
      this.sound.play('place');
      this.inventoryChanged();
      this.hideVegetation(x, z);
    }
  }
  hideVegetation(x, z) {
    if (!this.atmosphere.flowerPositions) return;
    const matrix = new THREE.Matrix4().makeScale(0, 0, 0),
      grass = this.atmosphere.flowers.children[0];
    this.atmosphere.flowerPositions.forEach((p, i) => {
      if (Math.floor(p.x) === x && Math.floor(p.z) === z) {
        grass.setMatrixAt(i * 2, matrix);
        grass.setMatrixAt(i * 2 + 1, matrix);
        grass.instanceMatrix.needsUpdate = true;
      }
    });
    this.atmosphere.petalPositions.forEach((list, color) =>
      list.forEach((p, i) => {
        if (Math.floor(p.x) === x && Math.floor(p.z) === z) {
          const mesh = this.atmosphere.flowers.children[color + 1];
          mesh.setMatrixAt(i, matrix);
          mesh.instanceMatrix.needsUpdate = true;
        }
      })
    );
  }
  mine(dt) {
    if (!this.buttons.has(0) || this.actionCooldown > 0) {
      if (!this.buttons.has(0)) this.mining.progress = 0;
      this.crackMesh.visible = false;
      $('mining-progress').style.display = 'none';
      return;
    }
    const origin = this.camera.position,
      dir = this.camera.getWorldDirection(new THREE.Vector3()),
      creature = this.creatures.hit(origin, dir, Math.min(3.1, this.target?.distance ?? 3.1));
    if (creature) {
      this.hitCreature(creature);
      return;
    }
    if (!this.target) {
      this.startSwing();
      return;
    }
    const t = this.target,
      block = BLOCKS[t.id],
      key = `${t.x},${t.y},${t.z}`,
      held = ITEMS[this.hotbar[this.selected]],
      hasTool = this.mode === 'creative' || (this.inventory[this.hotbar[this.selected]] || 0) > 0;
    if (block.hardness === Infinity) {
      if (this.mining.key !== key) this.ui.toast(`${block.name} cannot be mined.`);
      this.mining.key = key;
      return;
    }
    if (this.mining.key !== key) {
      this.mining.key = key;
      this.mining.progress = 0;
    }
    const tier = hasTool ? held?.tier || 0 : 0;
    const speed = tier ? held?.speed || 1 : 1,
      hardness =
        this.mode === 'creative'
          ? 0.16
          : block.hardness / (t.id === B.LOG ? Math.max(1, speed * 0.55) : speed);
    this.mining.progress += dt / Math.max(0.1, hardness);
    this.startSwing();
    $('mining-progress').style.display = 'block';
    $('mining-progress').firstElementChild.style.width =
      `${Math.min(100, this.mining.progress * 100)}%`;
    this.crackMesh.position.set(t.x + 0.5, t.y + 0.5, t.z + 0.5);
    this.crackMesh.visible = this.mode !== 'creative';
    this.drawCracks(this.mining.progress);
    if (this.mining.progress >= 1) {
      if (block.tier && tier < block.tier && this.mode !== 'creative') {
        this.ui.toast(
          'This ore needs a stronger pickaxe.',
          `Required: ${['', 'wooden', 'stone', 'iron', 'crystal'][block.tier]} pickaxe or better.`,
          true
        );
        this.actionCooldown = 0.5;
        this.mining.progress = 0;
        return;
      }
      if (t.id === B.CHEST) this.systems.chest(t);
      this.world.set(t.x, t.y, t.z, B.AIR);
      if (t.id === B.OBSIDIAN) collapsePortals(this.world, t.x, t.y, t.z);
      if (block.bed) {
        const facing = block.facing,
          dx = [0, 1, 0, -1][facing],
          dz = [-1, 0, 1, 0][facing];
        this.world.set(
          t.x + dx * (block.bedHead ? -1 : 1),
          t.y,
          t.z + dz * (block.bedHead ? -1 : 1),
          B.AIR
        );
      }
      if (this.containers.has(key)) {
        for (const stack of this.containers.get(key))
          if (stack) this.drops.spawn(stack, { x: t.x + 0.5, y: t.y + 0.5, z: t.z + 0.5 });
        this.containers.delete(key);
      }
      if ([B.CHEST, B.GOLD_BLOCK, B.NETHER_GOLD].includes(t.id)) this.creatures.alertPiglins();
      if (this.furnaces.has(key)) {
        for (const stack of this.furnaces.get(key).slots)
          if (stack) this.drops.spawn(stack, { x: t.x + 0.5, y: t.y + 0.5, z: t.z + 0.5 });
        this.furnaces.delete(key);
      }
      this.stats.mined++;
      if (t.id === B.LOG) this.stats.logs++;
      if (this.mode === 'survival') {
        if (block.drop)
          this.drops.spawn(
            makeStack(
              t.id === B.GRAVEL && Math.random() < 0.25 ? 129 : block.drop,
              block.dropCount || 1
            ),
            {
              x: t.x + 0.5,
              y: t.y + 0.4,
              z: t.z + 0.5,
            }
          );
        if (t.id === B.LEAVES && Math.random() < 0.35)
          this.drops.spawn(makeStack(93), { x: t.x + 0.5, y: t.y + 0.4, z: t.z + 0.5 });
        this.wearTool(held?.damage ? 2 : 1);
        this.player.exhaustion += 0.005;
      }
      this.hideVegetation(t.x, t.z);
      this.particles.burst(t.x + 0.5, t.y + 0.5, t.z + 0.5, block.color);
      this.sound.play('mine');
      this.actionCooldown = this.mode === 'creative' ? 0.11 : 0.08;
      this.mining.progress = 0;
      this.mining.key = null;
      this.crackMesh.visible = false;
      this.startSwing();
    }
  }
  drawCracks(progress) {
    const stage = Math.floor(progress * 7);
    if (stage === this.crackStage) return;
    this.crackStage = stage;
    const ctx = this.crackCanvas.getContext('2d');
    ctx.clearRect(0, 0, 32, 32);
    ctx.strokeStyle = 'rgba(25,31,21,.72)';
    ctx.lineWidth = 1.6;
    for (let i = 0; i <= stage; i++) {
      let x = 16,
        y = 16;
      ctx.beginPath();
      ctx.moveTo(x, y);
      for (let n = 0; n < 4; n++) {
        const a = i * 2.399 + n * 0.37;
        x += Math.cos(a) * 4.5;
        y += Math.sin(a) * 4.5;
        ctx.lineTo(Math.round(x), Math.round(y));
      }
      ctx.stroke();
    }
    this.crackTexture.needsUpdate = true;
  }
  hitCreature(m) {
    this.actionCooldown = 0.55;
    this.startSwing();
    const id = this.hotbar[this.selected],
      held = this.mode === 'creative' || this.inventory[id] > 0 ? ITEMS[id] : null,
      damage =
        (held?.damage || 1) * (!this.player.grounded && this.player.velocity.y < 0 ? 1.5 : 1);
    this.creatures.hurt(m, damage, this.camera.getWorldDirection(new THREE.Vector3()));
    this.wearTool(held?.damage ? 1 : 2);
    this.player.exhaustion += 0.1;
    this.particles.burst(m.x, m.y + 0.8, m.z, m.kind === 'sentinel' ? '#a8d7c3' : '#dec6a2', 6);
    this.sound.play('mine');
  }
  hurt(amount, reason, physical = false) {
    if (this.mode === 'creative' || this.damageCooldown > 0 || this.paused) return;
    if (physical) {
      const shield =
        this.blocking &&
        (ITEMS[this.storage.offhand[0]?.id]?.shield ? this.storage.offhand : this.storage.slots);
      if (shield) {
        const index = shield === this.storage.offhand ? 0 : this.selected;
        shield[index].durability -= Math.ceil(amount);
        if (shield[index].durability <= 0) shield[index] = null;
        this.damageCooldown = 0.6;
        this.sound.play('place');
        this.inventoryChanged();
        return;
      }
      const armor = this.storage.armor.reduce((n, s) => n + (ITEMS[s?.id]?.protection || 0), 0);
      const wear = Math.max(1, Math.floor(amount / 4));
      amount = Math.max(1, Math.round(amount * (1 - Math.min(20, armor) * 0.04)));
      this.storage.armor = this.storage.armor.map((s) =>
        s ? (s.durability - wear > 0 ? { ...s, durability: s.durability - wear } : null) : null
      );
      this.inventoryChanged();
    }
    this.player.health = Math.max(0, this.player.health - Math.max(1, Math.round(amount)));
    this.player.exhaustion += 0.1;
    this.damageTimer = 0.45;
    this.damageCooldown = 0.6;
    this.sound.play('hurt');
    this.ui.update();
    if (this.player.health <= 0) {
      this.ui.hide();
      for (const s of [
        ...this.storage.slots,
        ...this.storage.armor,
        ...this.storage.offhand,
        ...this.storage.grid,
        this.storage.cursor,
        ...this.storage.overflow,
      ])
        this.dropStack(s);
      this.storage = new Inventory();
      this.inventoryChanged();
      this.showDeath(reason);
    }
  }
  showDeath(reason) {
    this.paused = true;
    this.ui.show('death-modal');
    this.deathCountdown = 3;
    this.systems.wake();
    $('death-countdown').textContent = 'Respawning in 3...';
    $('death-reason').textContent = reason;
    this.clearInput();
  }
  respawn() {
    return this.systems.respawn();
  }
  isNight() {
    return this.systems.isNight();
  }
  blockLight(x, y, z) {
    return this.systems.blockLight(x, y, z);
  }
  explode(x, y, z, radius, reason) {
    return this.systems.explode(x, y, z, radius, reason);
  }
  refreshHeld() {
    if (!this.heldRoot) return;
    this.heldRoot.traverse((o) => {
      if (o.userData.ownedGeometry) o.geometry.dispose();
      if (o.userData.ownedTexture) o.material.map?.dispose();
      if (o.userData.ownedMaterial) o.material.dispose();
    });
    this.heldRoot.clear();
    const id = this.hotbar[this.selected],
      has = this.mode === 'creative' || (this.inventory[id] || 0) > 0;
    let held;
    if (has && ITEMS[id]?.tool) {
      held = this.assets.make(id === 103 ? 'sword' : 'pickaxe');
      if (held) {
        held.scale.setScalar(0.52);
        held.position.y = 0.1;
        held.rotation.set(0.12, -0.65, -0.23);
        if (id === 100 || id === 101 || id === 104)
          held.traverse((o) => {
            if (o.isMesh && o.material.name.startsWith('iron')) {
              o.material = o.material.clone();
              o.material.color.set(id === 100 ? 0xa9814b : id === 104 ? 0x60d8cf : 0x8c9990);
              o.userData.ownedMaterial = true;
            }
          });
      }
    } else if (has && BLOCKS[id]?.model) {
      held = this.assets.make(BLOCKS[id].model);
      if (held) {
        held.scale.setScalar(0.39);
        held.rotation.y = -0.7;
        held.position.y = 0.22;
      }
    } else if (has && BLOCKS[id]) {
      const geo = new THREE.BoxGeometry(0.24, 0.24, 0.24),
        uv = geo.getAttribute('uv');
      for (let f = 0; f < 6; f++) {
        const tile = uvFor(tileFor(id, f));
        for (let v = f * 4; v < f * 4 + 4; v++)
          uv.setXY(
            v,
            tile[0] + uv.getX(v) * (tile[2] - tile[0]),
            tile[1] + uv.getY(v) * (tile[3] - tile[1])
          );
      }
      held = new THREE.Mesh(geo, this.heldBlockMaterial);
      held.userData.ownedGeometry = true;
      held.position.y = 0.3;
      held.rotation.set(0.12, 0.45, -0.13);
    } else if (has && id === 114) {
      held = new THREE.Group();
      const plate = new THREE.Mesh(
        new THREE.BoxGeometry(0.32, 0.46, 0.065),
        new THREE.MeshLambertMaterial({ color: 0x8c978e })
      );
      const wood = new THREE.Mesh(
        new THREE.BoxGeometry(0.26, 0.38, 0.075),
        new THREE.MeshLambertMaterial({ color: 0xb59564 })
      );
      for (const mesh of [plate, wood]) {
        mesh.userData.ownedGeometry = true;
        mesh.userData.ownedMaterial = true;
        mesh.position.y = 0.26;
        held.add(mesh);
      }
      held.rotation.y = -0.4;
    } else if (has && id === 93) {
      held = new THREE.Group();
      for (let i = 0; i < 4; i++) {
        const berry = new THREE.Mesh(
          new THREE.BoxGeometry(0.08, 0.08, 0.08),
          new THREE.MeshLambertMaterial({ color: 0xa85465 })
        );
        berry.position.set((i % 2) * 0.08 - 0.04, 0.36 + Math.floor(i / 2) * 0.06, 0);
        berry.userData.ownedGeometry = true;
        berry.userData.ownedMaterial = true;
        held.add(berry);
      }
    }
    if (!held && has && this.icons[id]) {
      const texture = new THREE.TextureLoader().load(this.icons[id]);
      texture.magFilter = THREE.NearestFilter;
      texture.colorSpace = THREE.SRGBColorSpace;
      held = new THREE.Mesh(
        new THREE.PlaneGeometry(0.32, 0.32),
        new THREE.MeshLambertMaterial({
          map: texture,
          transparent: true,
          alphaTest: 0.3,
          side: THREE.DoubleSide,
        })
      );
      held.position.y = 0.32;
      Object.assign(held.userData, {
        ownedGeometry: true,
        ownedMaterial: true,
        ownedTexture: true,
      });
    }
    const arm = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.43, 0.18), this.sleeveMaterial);
    arm.position.set(0.02, -0.1, 0.12);
    arm.rotation.set(-0.25, 0, 0.12);
    arm.userData.ownedGeometry = true;
    this.heldRoot.add(arm);
    const hand = new THREE.Mesh(new THREE.BoxGeometry(0.17, 0.19, 0.19), this.handMaterial);
    hand.position.set(0, 0.18, 0.05);
    hand.userData.ownedGeometry = true;
    this.heldRoot.add(hand);
    if (held) this.heldRoot.add(held);
  }
  updatePlayer(dt) {
    updatePlayer(this, dt);
  }
  updateLights() {
    const positions = [];
    for (const [key, id] of this.world.edits)
      if (id === B.CAMPFIRE || id === B.LANTERN || id === B.TORCH) {
        const [x, y, z] = key.split(',').map(Number),
          d = (x - this.camera.position.x) ** 2 + (z - this.camera.position.z) ** 2;
        if (d < 32 * 32) positions.push({ x, y, z, id, d });
      }
    positions.sort((a, b) => a.d - b.d);
    for (let i = 0; i < this.blockLights.length; i++) {
      const light = this.blockLights[i],
        p = positions[i];
      light.visible = !!p;
      if (p) {
        light.position.set(p.x + 0.5, p.y + 0.9, p.z + 0.5);
        light.intensity =
          (p.id === B.CAMPFIRE ? 8 : 5) * (1 + Math.sin(this.elapsed * 11 + i) * 0.04);
      }
    }
    for (const group of this.chunks.meshes.values())
      for (const child of group.children) {
        const flame = child.getObjectByName('flame');
        if (flame)
          flame.scale.set(
            1 + Math.sin(this.elapsed * 7) * 0.04,
            1 + Math.sin(this.elapsed * 10) * 0.07,
            1
          );
      }
  }
  tick(now) {
    requestAnimationFrame((t) => this.tick(t));
    if (
      this.settings.maxFps &&
      this.previousFrame &&
      now - this.previousFrame < 1000 / this.settings.maxFps - 0.5
    )
      return;
    const realDt = Math.max(0, (now - (this.previousFrame || now)) / 1000);
    const dt = Math.min(realDt, 0.25);
    this.previousFrame = now;
    this.elapsed += dt;
    this.frameTime += realDt;
    this.frameCount++;
    if (this.frameTime >= 0.5) {
      this.fps = this.frameCount / this.frameTime;
      this.frameTime = 0;
      this.frameCount = 0;
    }
    if (!this.world || !this.chunks || this.generating) return;
    this.systems.frame(Math.min(realDt, 1));
    if (this.active) {
      if (!this.paused) {
        for (
          let remaining = dt;
          remaining > 0.00001 && !this.paused && !this.systems.transition;
          remaining -= 0.05
        ) {
          const step = Math.min(0.05, remaining);
          this.time += step;
          this.playTime += step;
          this.updatePlayer(step);
          this.creatures.update(step, this.player, this.isNight(), this.mode === 'survival');
          this.particles.update(step);
          this.drops.update(step, this.player, this.storage, (id, count) => {
            this.ui.pickup(id, count);
            this.inventoryChanged();
          });
          for (const furnace of this.furnaces.values()) tickFurnace(furnace, step);
          this.systems.tick(step);
        }
      }
      this.chunks.update(this.player.x, this.player.z, this.settings.distance);
      this.atmosphere.update(this.time, this.player, dt, this.settings.distance);
      this.scene.fog.near *= this.settings.fogDistance / 100;
      this.scene.fog.far *= this.settings.fogDistance / 100;
      this.outline.visible = !!this.target && !this.paused;
      if (this.paused) {
        this.crackMesh.visible = false;
        $('mining-progress').style.display = 'none';
      }
      const underwater = this.player.underwater && !this.paused;
      $('underwater').style.opacity = underwater ? '1' : '0';
      if (underwater) {
        this.scene.fog.color.set(0x397886);
        this.scene.fog.near = 1;
        this.scene.fog.far = 19;
      }
    } else {
      const orbit = this.elapsed * 0.012;
      this.camera.position.set(
        37 + Math.sin(orbit) * 5,
        42 + Math.sin(orbit * 0.5) * 1.5,
        48 + Math.cos(orbit) * 4
      );
      this.camera.lookAt(-5, 26, -16);
      this.chunks.update(7, 7, this.settings.distance);
      this.atmosphere.update(400, { x: 9, y: 29, z: 5 }, dt, this.settings.distance, true);
      this.creatures.update(dt, { x: 0, y: 29, z: 0 }, false, false);
      this.outline.visible = false;
      this.crackMesh.visible = false;
      $('underwater').style.opacity = '0';
    }
    this.damageTimer = Math.max(0, this.damageTimer - dt);
    $('damage-flash').style.opacity = String(this.damageTimer / 0.45);
    this.updateLights();
    this.assets.portalMaterial.uniforms.time.value = this.elapsed;
    this.renderer.info.reset();
    this.renderer.autoClear = true;
    this.renderer.render(this.scene, this.camera);
    if (this.active && !this.paused && !this.ui.modal) {
      animateHand(this, dt);
      this.renderer.autoClear = false;
      this.renderer.clearDepth();
      this.renderer.render(this.viewScene, this.viewCamera);
      this.renderer.autoClear = true;
    }
    if (this.elapsed - this.lastHud > 0.18) {
      this.lastHud = this.elapsed;
      if (this.active) this.ui.update();
    }
  }
  inspect() {
    return {
      ready: !!this.world,
      active: this.active,
      paused: this.paused,
      mode: this.mode,
      dimension: this.dimension,
      bedSpawn: this.bedSpawn,
      name: this.name,
      seed: this.world?.seed,
      player: {
        x: this.player.x,
        y: this.player.y,
        z: this.player.z,
        yaw: this.player.yaw,
        pitch: this.player.pitch,
        health: this.player.health,
        hunger: this.player.hunger,
        flying: this.player.flying,
        grounded: this.player.grounded,
        underwater: this.player.underwater,
      },
      time: this.time,
      playTime: this.playTime,
      selected: this.selected,
      hotbar: [...this.hotbar],
      inventory: { ...this.inventory },
      inventoryData: this.storage.serialize(),
      stats: { ...this.stats },
      target: this.target,
      edits: this.world?.serialize(),
      chunks: this.chunks?.meshes.size,
      models: [...this.assets.loaded],
      creatures: this.creatures?.list.map((m) => ({ kind: m.kind, x: m.x, y: m.y, z: m.z })),
      fps: this.fps,
      saveError: this.saveError,
    };
  }
}

async function boot() {
  try {
    const game = new Game();
    window.__wilds = Object.freeze({ inspect: () => game.inspect() });
    if (new URLSearchParams(location.search).has('test'))
      window.__wildsTest = {
        game,
        pose: (x, y, z, yaw = 0, pitch = 0) => {
          game.player.x = x;
          game.player.y = y;
          game.player.z = z;
          game.player.yaw = yaw;
          game.player.pitch = pitch;
          game.player.velocity.set(0, 0, 0);
          game.player.fallTop = y;
          game.player.grounded = false;
        },
        setBlock: (x, y, z, id) => game.world.set(x, y, z, id),
        give: (id, count) => game.addItem(id, count, false),
        time: (value) => {
          game.time = value;
        },
        save: () => game.save(),
        block: (x, y, z) => game.world.get(x, y, z),
      };
    await game.init();
  } catch (error) {
    console.error(error);
    $('loading').classList.add('hidden');
    $('fatal-error').classList.remove('hidden');
    $('fatal-message').textContent =
      `${error.message}. Start the desktop app or use Play Voxel Wilds.bat with hardware acceleration enabled in your browser.`;
  }
}
boot();
