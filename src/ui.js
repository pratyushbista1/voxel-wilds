import { ITEMS, BLOCKS } from './blocks.js';
import { InventoryView, stackHTML } from './inventory-view.js';

export const $ = (id) => document.getElementById(id);
const escape = (text) =>
  String(text).replace(
    /[&<>"']/g,
    (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]
  );

export class UI {
  constructor(game, icons) {
    this.game = game;
    this.icons = icons;
    this.modal = null;
    this.returnTo = null;
    this.mode = 'creative';
    this.worlds = [];
    this.lastVitals = '';
    this.inventoryView = new InventoryView(game, icons);
    this.bind();
  }
  bind() {
    const g = this.game;
    const on = (id, fn) =>
      $(id)?.addEventListener('click', () => {
        g.sound.play('click');
        fn();
      });
    on('new-world-button', () => this.show('create-modal'));
    on('worlds-create', () => this.show('create-modal'));
    on('worlds-button', () => {
      this.renderWorlds();
      this.show('worlds-modal');
    });
    on('continue-button', () => this.run(() => g.load(this.worlds[0].id)));
    on('menu-settings-button', () => this.show('settings-modal'));
    on('how-to-button', () => this.show('guide-modal'));
    on('create-play-button', () =>
      this.run(() =>
        g.create(
          $('world-name').value.trim() || 'New World',
          $('world-seed').value.trim() || 'wildflower',
          this.mode
        )
      )
    );
    on('random-seed', () => {
      $('world-seed').value = String(Math.floor(Math.random() * 2147483647));
    });
    document.querySelectorAll('.mode-card').forEach((button) =>
      button.addEventListener('click', () => {
        this.mode = button.dataset.mode;
        document
          .querySelectorAll('.mode-card')
          .forEach((b) => b.classList.toggle('selected', b === button));
      })
    );
    document
      .querySelectorAll('[data-close]')
      .forEach((button) => button.addEventListener('click', () => this.close()));
    on('pause-button', () => g.pause());
    on('resume-button', () => {
      this.hide();
      g.capture();
    });
    on('pause-settings', () => this.show('settings-modal', 'pause-modal'));
    on('pause-guide', () => this.show('guide-modal', 'pause-modal'));
    on('save-quit-button', () => this.run(() => g.quit()));
    on('respawn-button', () => {
      g.respawn();
      this.hide();
      g.capture();
    });
    on('death-respawn', () => {
      g.respawn(true);
      this.hide();
      g.capture();
    });
    on('fullscreen-button', () => {
      const op = document.fullscreenElement
        ? document.exitFullscreen()
        : document.documentElement.requestFullscreen();
      op.catch(() => this.toast('Use F11 to switch fullscreen.'));
    });
    on('exit-game-button', () => window.desktop?.quit());
    $('exit-game-button')?.classList.toggle('hidden', !window.desktop);
    on('reload-button', () => location.reload());
    for (const [id, key, suffix] of [
      ['sensitivity', 'sensitivity', '%'],
      ['view-distance', 'distance', ' chunks'],
      ['fov', 'fov', '°'],
      ['volume', 'volume', '%'],
    ]) {
      $(id).value = g.settings[key];
      $(id + '-value').textContent = g.settings[key] + suffix;
      $(id).addEventListener('input', () => {
        g.settings[key] = Number($(id).value);
        $(id + '-value').textContent = $(id).value + suffix;
        g.applySettings();
      });
    }
    for (const [id, key] of [
      ['shadows', 'shadows'],
      ['show-fps', 'showFps'],
    ]) {
      $(id).checked = g.settings[key];
      $(id).addEventListener('change', () => {
        g.settings[key] = $(id).checked;
        g.applySettings();
      });
    }
    $('world-list').addEventListener('click', (e) => {
      const el = e.target.closest('[data-world]');
      if (el) this.run(() => g.load(el.dataset.world));
    });
    $('hotbar').addEventListener('click', (e) => {
      const slot = e.target.closest('[data-slot]');
      if (!slot || this.modal) return;
      g.selected = Number(slot.dataset.slot);
      this.renderHotbar();
      g.refreshHeld();
    });
  }
  async run(fn) {
    try {
      await fn();
    } catch (error) {
      console.error(error);
      this.toast(error.message || 'Operation failed.', null, true);
      $('loading').classList.add('hidden');
      if (this.game.active) this.game.pause();
      else {
        $('menu').classList.remove('hidden');
        this.hide();
      }
    }
  }
  async fetchWorlds() {
    try {
      const response = await fetch('/api/worlds');
      if (!response.ok) throw Error('Cannot read saved worlds.');
      this.worlds = await response.json();
      $('world-count').textContent = this.worlds.length;
      $('continue-button').classList.toggle('hidden', !this.worlds.length);
      $('continue-world-name').textContent = this.worlds[0]?.name || '';
      $('new-world-button').classList.toggle('primary', !this.worlds.length);
      $('new-world-button').classList.toggle('secondary', !!this.worlds.length);
    } catch (error) {
      this.toast(error.message, null, true);
    }
  }
  show(id, returnTo = null) {
    this.hide();
    this.modal = id;
    this.returnTo = returnTo;
    $(id).classList.remove('hidden');
    $('modal-shade').classList.remove('hidden');
    document.body.classList.toggle('inventory-open', id === 'inventory-modal');
    if (this.game.active) {
      this.game.paused = id !== 'inventory-modal';
      this.game.clearInput();
      if (document.pointerLockElement) document.exitPointerLock();
    }
    if (id === 'inventory-modal') {
      this.inventoryView.render();
      this.game.syncInventory();
    }
    if (id === 'pause-modal') {
      $('pause-world-name').textContent = this.game.name;
      $('pause-save-status').textContent = this.game.saveError
        ? 'Save failed. Keep the game open and try again.'
        : 'Your progress is saved automatically.';
    }
  }
  hide() {
    const previous = this.modal;
    if (previous) $(previous).classList.add('hidden');
    this.modal = null;
    this.returnTo = null;
    $('modal-shade').classList.add('hidden');
    document.body.classList.remove('inventory-open');
    this.inventoryView.hide();
    if (previous === 'inventory-modal') this.game.closeInventory();
  }
  close() {
    if (this.modal === 'death-modal') return;
    if (this.returnTo) {
      const id = this.returnTo;
      this.show(id);
      return;
    }
    this.hide();
    if (this.game.active) this.game.capture();
  }
  renderWorlds() {
    $('world-list').innerHTML = this.worlds.length
      ? this.worlds
          .map(
            (w) =>
              `<button class="world-row" data-world="${escape(w.id)}"><span class="world-icon"><img src="/favicon.svg" alt=""></span><span><strong>${escape(w.name)}</strong><small>${w.mode === 'creative' ? 'Creative' : 'Survival'} · ${Math.max(1, Math.round((w.playTime || 0) / 60))} minutes</small><small>Seed: ${escape(w.seed)}</small></span><span class="world-arrow">↗</span></button>`
          )
          .join('')
      : '<div class="empty-worlds">No saved worlds.</div>';
  }
  renderHotbar() {
    $('hotbar').innerHTML = this.game.storage.slots
      .slice(0, 9)
      .map(
        (stack, i) =>
          `<button class="hotbar-slot${i === this.game.selected ? ' active' : ''}" data-slot="${i}" title="${stack ? ITEMS[stack.id].name : 'Empty slot'}"><span class="slot-key">${i + 1}</span>${stackHTML(stack, this.icons)}</button>`
      )
      .join('');
    $('selected-label').textContent = ITEMS[this.game.hotbar[this.game.selected]]?.name || '';
    $('selected-label').style.opacity = '1';
    clearTimeout(this.labelTimer);
    this.labelTimer = setTimeout(() => ($('selected-label').style.opacity = '0'), 2000);
    const offhand = this.game.storage.offhand[0];
    $('offhand-hud').innerHTML = stackHTML(offhand, this.icons);
    $('offhand-hud').classList.toggle('hidden', !offhand);
  }
  refreshItems() {
    this.renderHotbar();
    if (this.modal === 'inventory-modal') this.inventoryView.render();
  }
  toast(message, detail = null, warning = false) {
    const div = document.createElement('div');
    div.className = 'toast' + (warning ? ' warning' : '');
    div.textContent = message;
    if (detail) {
      const small = document.createElement('small');
      small.textContent = detail;
      div.append(small);
    }
    $('toast-stack').append(div);
    while ($('toast-stack').children.length > 4) $('toast-stack').firstElementChild.remove();
    setTimeout(() => div.remove(), 4000);
  }
  pickup(id, count = 1) {
    this.toast(`+${count} ${ITEMS[id]?.name || 'Item'}`);
  }
  update() {
    const g = this.game,
      p = g.player;
    if (!p || !g.world) return;
    $('biome-name').textContent = g.world.biome(p.x, p.z);
    $('coordinates').textContent =
      `X ${Math.floor(p.x)} · Y ${Math.floor(p.y)} · Z ${Math.floor(p.z)}`;
    $('direction').textContent = ['N', 'W', 'S', 'E'][
      ((Math.round(p.yaw / (Math.PI / 2)) % 4) + 4) % 4
    ];
    const minutes = Math.floor(((g.time % 1200) / 1200) * 1440);
    $('day-label').textContent = `DAY ${Math.floor(g.time / 1200) + 1}`;
    $('time-label').textContent =
      `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
    $('time-icon').textContent = g.atmosphere.lightLevel > 0.4 ? '☀' : '☾';
    $('mode-label').textContent =
      g.mode === 'creative'
        ? `CREATIVE · ${p.flying ? 'FLYING' : 'DOUBLE-TAP SPACE TO FLY'}`
        : `SURVIVAL${p.underwater ? ' · HOLD SPACE TO SWIM' : ''}`;
    $('vitals').classList.toggle('hidden', g.mode === 'creative');
    const armor = g.storage.armor.reduce((n, s) => n + (ITEMS[s?.id]?.protection || 0), 0);
    const vitals = `${p.health},${p.hunger},${armor},${Math.ceil(p.breath)},${p.underwater}`;
    if (vitals !== this.lastVitals) {
      this.lastVitals = vitals;
      const meter = (value, cls, symbol) =>
        Array.from(
          { length: 10 },
          (_, i) =>
            `<span class="${cls}" data-fill="${THREEClamp(value - i * 2, 0, 2)}"><span>${symbol}</span><i style="width:${THREEClamp((value - i * 2) / 2, 0, 1) * 100}%">${symbol}</i></span>`
        ).join('');
      $('health').innerHTML = meter(p.health, 'heart', '♥');
      $('health').title = `Health: ${p.health} / 20`;
      $('hunger').innerHTML = meter(p.hunger, 'food', '◆');
      $('hunger').title = `Food: ${Math.floor(p.hunger)} / 20`;
      $('armor-meter').innerHTML = meter(armor, 'armor-point', '⬟');
      $('armor-meter').classList.toggle('hidden', !armor);
      $('breath-meter').textContent = p.underwater
        ? '○'.repeat(Math.max(0, Math.ceil(p.breath / 1.2)))
        : '';
    }
    $('target-info').classList.toggle('hidden', !g.target);
    if (g.target) {
      const block = BLOCKS[g.target.id];
      $('target-name').textContent = block.name;
      $('target-tool').textContent =
        g.target.id === 16 || g.target.id === 23
          ? 'Right click to open'
          : block.tier &&
              !(ITEMS[g.hotbar[g.selected]]?.tier >= block.tier) &&
              g.mode !== 'creative'
            ? 'Requires a stronger pickaxe'
            : 'Hold left click to mine';
    }
    let objective = 'Explore and build',
      detail = 'E: inventory. H: controls.';
    if (g.mode === 'survival') {
      if (!g.stats.logs) {
        objective = 'Gather wood';
        detail = 'Hold left click on a tree trunk.';
      } else if (!g.stats.crafted) {
        objective = 'Craft oak planks';
        detail = 'Place a log in your 2x2 crafting grid.';
      } else if (!g.inventory[101] && !g.inventory[102]) {
        objective = 'Craft a stone pickaxe';
        detail = 'Use a crafting table, 3 cobblestone and 2 sticks.';
      }
    }
    $('objective-text').textContent = objective;
    $('objective-detail').textContent = detail;
    $('fps-counter').textContent =
      `${Math.round(g.fps)} FPS · ${g.renderer.info.render.calls} draws\n${g.chunks.meshes.size} chunks · ${Math.round(g.renderer.info.render.triangles / 1000)}k triangles`;
    if (
      this.modal === 'inventory-modal' &&
      g.inventoryKind === 'furnace' &&
      !this.inventoryView.drag
    )
      this.inventoryView.updateFurnace();
  }
}
const THREEClamp = (n, min, max) => Math.min(max, Math.max(min, n));
