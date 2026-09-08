import * as THREE from 'three';
import { ITEMS, CREATIVE_ITEMS } from './blocks.js';
import { RECIPES, FUEL, SMELTING } from './recipes.js';
import { makeStack, maxStack, stackable } from './inventory.js';

const $ = (id) => document.getElementById(id);
export function stackHTML(stack, icons) {
  if (!stack) return '';
  const item = ITEMS[stack.id],
    ratio = stack.durability / item.durability;
  return `<img src="${icons[stack.id]}" alt="${item.name}" draggable="false">${stack.count > 1 ? `<span class="stack-count">${stack.count}</span>` : ''}${item.durability && ratio < 1 ? `<span class="durability"><i style="width:${ratio * 100}%;background:hsl(${ratio * 120} 85% 45%)"></i></span>` : ''}`;
}

export class InventoryView {
  constructor(game, icons) {
    this.game = game;
    this.icons = icons;
    this.book = false;
    this.hover = null;
    this.root = $('inventory-modal');
    this.drag = null;
    this.lastClick = null;
    this.root.addEventListener('contextmenu', (e) => e.preventDefault());
    this.root.addEventListener('click', (e) => {
      const recipe = e.target.closest('[data-recipe]');
      if (recipe)
        game.craft(
          RECIPES.find((r) => r.id === recipe.dataset.recipe),
          e.shiftKey
        );
      if (e.target.closest('#recipe-toggle')) {
        this.book = !this.book;
        this.render();
      }
      if (e.target.closest('#inventory-close')) game.ui.close();
    });
    $('recipe-search').addEventListener('input', () => this.renderBook());
    $('creative-search').addEventListener('input', () => this.renderCatalogue());
    $('creative-category').addEventListener('change', () => this.renderCatalogue());
    document.addEventListener('pointerdown', (e) => this.down(e));
    document.addEventListener('pointermove', (e) => this.move(e));
    document.addEventListener('pointerup', (e) => this.up(e));
  }
  address(el) {
    return el?.dataset.region ? { name: el.dataset.region, index: Number(el.dataset.index) } : null;
  }
  slot(name, index, stack, extra = '') {
    return `<button class="inv-slot ${extra}" data-region="${name}" data-index="${index}" aria-label="${stack ? ITEMS[stack.id].name + ', ' + stack.count : 'Empty slot'}">${stackHTML(stack, this.icons)}</button>`;
  }
  render() {
    const s = this.game.storage,
      kind = this.game.inventoryKind;
    const creative = this.game.mode === 'creative' && kind === 'personal';
    this.root.classList.toggle('has-book', this.book && !creative && kind !== 'furnace');
    this.root.classList.toggle('is-creative', creative);
    $('recipe-book').classList.toggle('hidden', !this.book || creative || kind === 'furnace');
    $('creative-catalogue').classList.toggle('hidden', !creative);
    $('recipe-toggle').classList.toggle('hidden', creative || kind === 'furnace');
    $('inventory-title').textContent =
      kind === 'table'
        ? 'Crafting'
        : kind === 'furnace'
          ? 'Furnace'
          : creative
            ? 'Creative Inventory'
            : 'Inventory';
    const area = $('station-area');
    area.className = 'station-area ' + kind;
    if (kind === 'furnace') {
      const f = s.furnace;
      area.innerHTML = `<div class="furnace-stack">${this.slot('furnace', 0, f.slots[0])}<div class="furnace-flame"><i style="height:${f.burnTotal ? (f.burn / f.burnTotal) * 100 : 0}%">♨</i><span>♨</span></div>${this.slot('furnace', 1, f.slots[1])}</div><div class="craft-arrow"><i style="width:${f.progress * 10}%">➜</i><span>➜</span></div>${this.slot('furnace', 2, f.slots[2], 'output-slot')}`;
    } else {
      const match = s.recipe(),
        result = match ? makeStack(match.recipe.output, match.recipe.count) : null;
      area.innerHTML = `${kind === 'personal' ? `<div class="armor-slots">${s.armor.map((stack, i) => this.slot('armor', i, stack, 'armor-' + i)).join('')}</div><div id="player-preview"></div>${this.slot('offhand', 0, s.offhand[0], 'offhand-slot')}` : ''}<div class="craft-area"><span class="craft-label">Crafting</span><div class="craft-row"><div class="craft-grid" style="grid-template-columns:repeat(${s.gridSize}, var(--slot))">${s.grid.map((stack, i) => this.slot('grid', i, stack)).join('')}</div><div class="craft-arrow">➜</div>${this.slot('output', 0, result, 'output-slot')}</div></div>`;
      if (kind === 'personal') {
        this.avatar ||= new Avatar();
        $('player-preview').append(this.avatar.renderer.domElement);
        this.avatar.update(s.armor);
      }
    }
    $('inventory-storage').innerHTML = s.slots
      .slice(9)
      .map((stack, i) => this.slot('bag', i + 9, stack))
      .join('');
    $('inventory-hotbar').innerHTML = s.slots
      .slice(0, 9)
      .map((stack, i) => this.slot('bag', i, stack))
      .join('');
    $('recipe-toggle').setAttribute('aria-expanded', String(this.book));
    this.renderBook();
    if (creative) this.renderCatalogue();
    this.renderCursor();
  }
  renderBook() {
    if (!this.book) return;
    const s = this.game.storage,
      bag = s.totals(true),
      query = $('recipe-search').value.toLowerCase();
    $('recipe-list').innerHTML = RECIPES.filter(
      (r) =>
        r.width <= s.gridSize &&
        r.height <= s.gridSize &&
        ITEMS[r.output].name.toLowerCase().includes(query)
    )
      .map((r) => {
        const can = r.ingredients.every(([id, n]) => (bag[id] || 0) >= n);
        const title = `${ITEMS[r.output].name}: ${r.ingredients.map(([id, n]) => `${n} ${ITEMS[id].name}`).join(', ')}`;
        return `<button class="recipe-tile ${can ? '' : 'unavailable'}" data-recipe="${r.id}" title="${title}">${stackHTML(makeStack(r.output, r.count), this.icons)}</button>`;
      })
      .join('');
    $('recipe-hint').textContent =
      s.gridSize === 2
        ? 'Use a crafting table for 3x3 recipes.'
        : 'Click a recipe to arrange its ingredients.';
  }
  updateFurnace() {
    const furnace = this.game.storage.furnace;
    if (!furnace || this.drag) return;
    for (let i = 0; i < 3; i++) {
      const slot = this.root.querySelector(`[data-region="furnace"][data-index="${i}"]`);
      if (!slot) return;
      const key = JSON.stringify(furnace.slots[i]);
      if (slot.dataset.stack !== key) {
        slot.dataset.stack = key;
        slot.innerHTML = stackHTML(furnace.slots[i], this.icons);
        slot.setAttribute(
          'aria-label',
          furnace.slots[i] ? ITEMS[furnace.slots[i].id].name : 'Empty slot'
        );
      }
    }
    this.root.querySelector('.furnace-flame i').style.height =
      `${furnace.burnTotal ? (furnace.burn / furnace.burnTotal) * 100 : 0}%`;
    this.root.querySelector('.craft-arrow i').style.width = `${furnace.progress * 10}%`;
  }
  renderCatalogue() {
    const search = $('creative-search').value.toLowerCase(),
      category = $('creative-category').value;
    $('creative-grid').innerHTML = CREATIVE_ITEMS.filter(
      (id) =>
        ITEMS[id].name.toLowerCase().includes(search) &&
        (!category || ITEMS[id].category === category)
    )
      .map(
        (id) =>
          `<button class="inv-slot" data-catalogue="${id}" title="${ITEMS[id].name}">${stackHTML(makeStack(id), this.icons)}</button>`
      )
      .join('');
  }
  renderCursor() {
    $('inventory-cursor').innerHTML = stackHTML(this.game.storage.cursor, this.icons);
    $('inventory-cursor').classList.toggle(
      'hidden',
      !this.game.storage.cursor || this.game.ui.modal !== 'inventory-modal'
    );
  }
  down(e) {
    if (this.game.ui.modal !== 'inventory-modal' || ![0, 2].includes(e.button)) return;
    const catalogue = e.target.closest('[data-catalogue]');
    if (catalogue) {
      document.activeElement?.blur();
      e.preventDefault();
      const id = Number(catalogue.dataset.catalogue);
      if (e.shiftKey) this.game.storage.insert(makeStack(id, 64));
      else this.game.storage.cursor = makeStack(id, e.button === 2 ? 1 : 64);
      this.game.inventoryChanged();
      return;
    }
    const address = this.address(e.target.closest('[data-region]'));
    if (address) {
      document.activeElement?.blur();
      e.preventDefault();
      this.drag = {
        address,
        addresses: [address],
        button: e.button,
        shift: e.shiftKey,
        hasCursor: !!this.game.storage.cursor,
        x: e.clientX,
        y: e.clientY,
      };
    } else if (!this.root.contains(e.target) && this.game.storage.cursor) {
      const stack = this.game.storage.cursor,
        count = e.button === 2 ? 1 : stack.count;
      if (this.game.mode !== 'creative') this.game.dropStack({ ...stack, count }, true);
      stack.count -= count;
      if (!stack.count) this.game.storage.cursor = null;
      this.game.inventoryChanged();
    }
  }
  move(e) {
    if (this.game.ui.modal !== 'inventory-modal') return;
    const el = e.target.closest('[data-region]');
    this.hover = this.address(el);
    $('inventory-cursor').style.transform = `translate(${e.clientX - 22}px, ${e.clientY - 22}px)`;
    const stack = this.game.storage.region(this.hover?.name)?.[this.hover?.index];
    const tip = $('inventory-tooltip');
    tip.classList.toggle('hidden', !stack || !!this.game.storage.cursor);
    if (stack) {
      tip.textContent =
        ITEMS[stack.id].name +
        (stack.durability
          ? `\nDurability: ${stack.durability} / ${ITEMS[stack.id].durability}`
          : '');
      tip.style.left = Math.min(innerWidth - 220, e.clientX + 16) + 'px';
      tip.style.top = Math.min(innerHeight - 60, e.clientY - 35) + 'px';
    }
    if (
      this.drag?.hasCursor &&
      this.hover &&
      this.hover.name !== 'output' &&
      Math.hypot(e.clientX - this.drag.x, e.clientY - this.drag.y) > 6
    ) {
      this.drag.addresses.push(this.hover);
      el.classList.add('drag-highlight');
    }
    this.avatar?.look(e.clientX, e.clientY);
  }
  up(e) {
    if (!this.drag || this.game.ui.modal !== 'inventory-modal') {
      this.drag = null;
      return;
    }
    const d = this.drag;
    this.drag = null;
    const s = this.game.storage,
      a = d.address;
    const unique = new Set(d.addresses.map((v) => v.name + v.index));
    if (unique.size > 1 && !d.shift) s.distribute(d.addresses, d.button === 2);
    else if (a.name === 'output') {
      const count = s.craftOutput(d.shift);
      if (count) {
        this.game.stats.crafted += count;
        this.game.sound.play('craft');
      }
    } else if (a.name === 'furnace' && a.index === 2) {
      const output = s.furnace.slots[2];
      if (d.shift) s.quickMove('furnace', 2);
      else if (
        output &&
        (!s.cursor || (stackable(output, s.cursor) && s.cursor.count + output.count <= 64))
      ) {
        if (s.cursor) s.cursor.count += output.count;
        else s.cursor = { ...output };
        s.furnace.slots[2] = null;
      }
    } else if (d.shift && s.furnace && a.name === 'bag') {
      const source = s.slots[a.index],
        to = SMELTING[source?.id] ? 0 : FUEL[source?.id] ? 1 : -1;
      const target = s.furnace.slots[to];
      if (to >= 0 && (!target || stackable(source, target))) {
        const n = Math.min(source.count, maxStack(source.id) - (target?.count || 0));
        if (target) target.count += n;
        else s.furnace.slots[to] = { ...source, count: n };
        s.take('bag', a.index, n);
      } else s.quickMove(a.name, a.index);
    } else if (
      !d.shift &&
      d.button === 0 &&
      this.lastClick?.key === a.name + a.index &&
      performance.now() - this.lastClick.time < 300
    ) {
      if (!s.cursor) s.cursor = s.take(a.name, a.index);
      s.collect();
      this.lastClick = null;
    } else {
      s.click(a.name, a.index, d.button, d.shift);
      this.lastClick = { key: a.name + a.index, time: performance.now() };
    }
    this.game.inventoryChanged();
  }
  key(e) {
    const a = this.hover,
      s = this.game.storage;
    if (!a || a.name === 'output') return;
    if (e.code.startsWith('Digit')) s.swapHotbar(a.name, a.index, Number(e.code.slice(5)) - 1);
    if (e.code === 'KeyQ')
      this.game.dropStack(s.take(a.name, a.index, e.ctrlKey ? Infinity : 1), true);
    if (e.code === 'KeyF' && s.accepts(a.name, a.index, s.offhand[0])) {
      const list = s.region(a.name);
      [list[a.index], s.offhand[0]] = [s.offhand[0], list[a.index]];
    }
    this.game.inventoryChanged();
  }
  hide() {
    this.drag = null;
    this.hover = null;
    $('inventory-cursor').classList.add('hidden');
    $('inventory-tooltip').classList.add('hidden');
  }
}

class Avatar {
  constructor() {
    this.renderer = new THREE.WebGLRenderer({ alpha: true, antialias: false });
    this.renderer.setSize(110, 166);
    this.renderer.setPixelRatio(2);
    this.scene = new THREE.Scene();
    this.scene.add(new THREE.HemisphereLight(0xffffff, 0x50576a, 3));
    this.camera = new THREE.PerspectiveCamera(34, 110 / 166, 0.1, 20);
    this.camera.position.set(0, 1.15, 4.7);
    this.camera.lookAt(0, 1, 0);
    this.person = new THREE.Group();
    this.scene.add(this.person);
    this.parts = [];
    const box = (w, h, d, x, y, color) => {
      const m = new THREE.Mesh(
        new THREE.BoxGeometry(w, h, d),
        new THREE.MeshLambertMaterial({ color })
      );
      m.position.set(x, y, 0);
      this.person.add(m);
      return m;
    };
    this.parts[0] = box(0.48, 0.48, 0.48, 0, 1.82, 0xcba67f);
    box(0.5, 0.13, 0.5, 0, 2.01, 0x58402a);
    for (const x of [-0.12, 0.12]) {
      const eye = box(0.06, 0.055, 0.015, x, 1.85, 0x283e36);
      eye.position.z = 0.25;
    }
    this.parts[1] = box(0.5, 0.7, 0.28, 0, 1.23, 0x618663);
    this.parts[2] = box(0.21, 0.62, 0.26, -0.13, 0.56, 0x525a75);
    this.parts[3] = box(0.21, 0.62, 0.26, 0.13, 0.56, 0x525a75);
    this.parts[4] = box(0.22, 0.18, 0.3, -0.13, 0.16, 0x494944);
    this.parts[5] = box(0.22, 0.18, 0.3, 0.13, 0.16, 0x494944);
    box(0.22, 0.6, 0.24, -0.37, 1.17, 0xcba67f).rotation.z = -0.06;
    box(0.22, 0.6, 0.24, 0.37, 1.17, 0xcba67f).rotation.z = 0.06;
  }
  update(armor) {
    const base = [0xcba67f, 0x618663, 0x525a75, 0x525a75, 0x494944, 0x494944];
    this.parts.forEach((part, i) =>
      part.material.color.set(armor[i < 2 ? i : i < 4 ? 2 : 3] ? 0xc5d2cb : base[i])
    );
    this.renderer.render(this.scene, this.camera);
  }
  look(x, y) {
    const rect = this.renderer.domElement.getBoundingClientRect();
    this.person.rotation.y = THREE.MathUtils.clamp((x - rect.x - rect.width / 2) / 450, -0.6, 0.6);
    this.person.rotation.x = THREE.MathUtils.clamp(
      (y - rect.y - rect.height / 2) / 800,
      -0.13,
      0.13
    );
    this.renderer.render(this.scene, this.camera);
  }
}
