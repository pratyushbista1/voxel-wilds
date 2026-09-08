import { ITEMS } from './blocks.js';
import { matchRecipe, FUEL } from './recipes.js';

export const BAG_SIZE = 36;
export const maxStack = (id) => (ITEMS[id]?.durability ? 1 : 64);
export const cloneStack = (stack) => (stack ? { ...stack } : null);
export const stackable = (a, b) => a && b && a.id === b.id && !ITEMS[a.id]?.durability;

export function makeStack(id, count = 1, durability = ITEMS[id]?.durability) {
  if (!ITEMS[id] || !id || count <= 0) return null;
  const stack = { id, count: Math.min(maxStack(id), Math.floor(count)) };
  if (ITEMS[id].durability)
    stack.durability = Math.max(
      1,
      Math.min(ITEMS[id].durability, durability ?? ITEMS[id].durability)
    );
  return stack;
}

export class Inventory {
  constructor(saved = null) {
    this.slots = Array(BAG_SIZE).fill(null);
    this.armor = Array(4).fill(null);
    this.offhand = [null];
    this.gridSize = 2;
    this.grid = Array(4).fill(null);
    this.cursor = null;
    this.overflow = [];
    if (saved) this.restore(saved);
  }
  restore(data) {
    this.slots = Array.from({ length: BAG_SIZE }, (_, i) => cloneStack(data.slots?.[i]));
    this.armor = Array.from({ length: 4 }, (_, i) => cloneStack(data.armor?.[i]));
    this.offhand = [cloneStack(data.offhand?.[0])];
    this.gridSize = data.gridSize === 3 ? 3 : 2;
    this.grid = Array.from({ length: this.gridSize ** 2 }, (_, i) => cloneStack(data.grid?.[i]));
    this.cursor = cloneStack(data.cursor);
    this.overflow = (data.overflow || []).map(cloneStack);
  }
  static migrate(bag = {}, hotbar = []) {
    const result = new Inventory(),
      remaining = { ...bag };
    hotbar.slice(0, 9).forEach((id, i) => {
      if (!ITEMS[id] || !(remaining[id] > 0)) return;
      const amount = Math.min(maxStack(id), remaining[id]);
      result.slots[i] = makeStack(id, amount);
      remaining[id] -= amount;
    });
    for (const [key, count] of Object.entries(remaining)) {
      const id = Number(key);
      if (!ITEMS[id]) continue;
      for (let n = count; n > 0; n -= maxStack(id)) {
        const excess = result.insert(makeStack(id, n));
        if (excess) result.overflow.push(excess);
      }
    }
    return result;
  }
  serialize() {
    return {
      slots: this.slots.map(cloneStack),
      armor: this.armor.map(cloneStack),
      offhand: this.offhand.map(cloneStack),
      grid: this.grid.map(cloneStack),
      gridSize: this.gridSize,
      cursor: cloneStack(this.cursor),
      overflow: this.overflow.map(cloneStack),
    };
  }
  totals(includeGrid = false) {
    const bag = {};
    const stacks = includeGrid
      ? [...this.slots, ...this.grid, this.cursor, ...this.armor, ...this.offhand, ...this.overflow]
      : this.slots;
    for (const stack of stacks) if (stack) bag[stack.id] = (bag[stack.id] || 0) + stack.count;
    return bag;
  }
  region(name) {
    return name === 'bag'
      ? this.slots
      : name === 'grid'
        ? this.grid
        : name === 'armor'
          ? this.armor
          : name === 'offhand'
            ? this.offhand
            : name === 'furnace'
              ? this.furnace?.slots
              : null;
  }
  accepts(name, index, stack) {
    return (
      !stack ||
      ((name !== 'armor' || ITEMS[stack.id]?.armor === index) &&
        (name !== 'furnace' || (index !== 2 && (index !== 1 || !!FUEL[stack.id]))))
    );
  }
  freeFor(stack, indices = [...this.slots.keys()]) {
    return indices.reduce(
      (n, i) =>
        n +
        (!this.slots[i]
          ? maxStack(stack.id)
          : stackable(stack, this.slots[i])
            ? maxStack(stack.id) - this.slots[i].count
            : 0),
      0
    );
  }
  insert(stack, indices = [...this.slots.keys()]) {
    if (!stack) return null;
    const rest = cloneStack(stack);
    for (const i of indices)
      if (stackable(this.slots[i], rest)) {
        const moved = Math.min(rest.count, maxStack(rest.id) - this.slots[i].count);
        this.slots[i].count += moved;
        rest.count -= moved;
        if (!rest.count) return null;
      }
    for (const i of indices)
      if (!this.slots[i]) {
        const moved = Math.min(rest.count, maxStack(rest.id));
        this.slots[i] = makeStack(rest.id, moved, rest.durability);
        rest.count -= moved;
        if (!rest.count) return null;
      }
    return rest;
  }
  extract(id, count) {
    if ((this.totals()[id] || 0) < count) return false;
    for (let i = 0; i < this.slots.length && count > 0; i++)
      if (this.slots[i]?.id === id) {
        const n = Math.min(count, this.slots[i].count);
        this.slots[i].count -= n;
        count -= n;
        if (!this.slots[i].count) this.slots[i] = null;
      }
    return true;
  }
  take(name, index, count = Infinity) {
    const list = this.region(name),
      source = list?.[index];
    if (!source) return null;
    const taken = { ...source, count: Math.min(count, source.count) };
    source.count -= taken.count;
    if (!source.count) list[index] = null;
    return taken;
  }
  click(name, index, button = 0, shift = false) {
    const list = this.region(name);
    if (!list || index < 0 || index >= list.length) return false;
    if (shift) return this.quickMove(name, index);
    const target = list[index];
    if (!this.cursor) {
      this.cursor = this.take(
        name,
        index,
        button === 2 ? Math.ceil((target?.count || 0) / 2) : Infinity
      );
      return !!this.cursor;
    }
    if (!this.accepts(name, index, this.cursor)) return false;
    if (!target) {
      const n = button === 2 ? 1 : this.cursor.count;
      list[index] = { ...this.cursor, count: n };
      this.cursor.count -= n;
    } else if (stackable(target, this.cursor)) {
      const n = Math.min(button === 2 ? 1 : this.cursor.count, maxStack(target.id) - target.count);
      target.count += n;
      this.cursor.count -= n;
    } else {
      list[index] = this.cursor;
      this.cursor = target;
    }
    if (this.cursor?.count === 0) this.cursor = null;
    return true;
  }
  quickMove(name, index) {
    const list = this.region(name),
      stack = list?.[index];
    if (!stack) return false;
    const armorSlot = ITEMS[stack.id]?.armor;
    if (name === 'bag' && armorSlot !== undefined && !this.armor[armorSlot]) {
      this.armor[armorSlot] = stack;
      list[index] = null;
      return true;
    }
    const indices =
      name === 'bag'
        ? index < 9
          ? Array.from({ length: 27 }, (_, i) => i + 9)
          : Array.from({ length: 9 }, (_, i) => i)
        : [...this.slots.keys()];
    const rest = this.insert(stack, indices);
    list[index] = rest;
    return !rest || rest.count !== stack.count;
  }
  swapHotbar(name, index, hotbarIndex) {
    const list = this.region(name);
    if (
      !list ||
      hotbarIndex < 0 ||
      hotbarIndex > 8 ||
      !this.accepts(name, index, this.slots[hotbarIndex])
    )
      return false;
    [list[index], this.slots[hotbarIndex]] = [this.slots[hotbarIndex], list[index]];
    return true;
  }
  collect() {
    if (!this.cursor || maxStack(this.cursor.id) === 1) return;
    for (const list of [this.slots, this.grid])
      for (let i = 0; i < list.length; i++)
        if (stackable(this.cursor, list[i])) {
          const n = Math.min(maxStack(this.cursor.id) - this.cursor.count, list[i].count);
          this.cursor.count += n;
          list[i].count -= n;
          if (!list[i].count) list[i] = null;
        }
  }
  distribute(addresses, single = false) {
    if (!this.cursor) return;
    const unique = [...new Map(addresses.map((a) => [a.name + ':' + a.index, a])).values()];
    const valid = unique.filter((a) => {
      const list = this.region(a.name),
        target = list?.[a.index];
      return (
        list &&
        this.accepts(a.name, a.index, this.cursor) &&
        (!target || stackable(this.cursor, target)) &&
        (!target || target.count < maxStack(target.id))
      );
    });
    if (!valid.length) return;
    const portion = single ? 1 : Math.floor(this.cursor.count / valid.length);
    for (const a of valid) {
      if (!this.cursor) break;
      const list = this.region(a.name),
        target = list[a.index];
      const n = Math.min(
        portion,
        this.cursor.count,
        maxStack(this.cursor.id) - (target?.count || 0)
      );
      if (!n) continue;
      if (target) target.count += n;
      else list[a.index] = { ...this.cursor, count: n };
      this.cursor.count -= n;
      if (!this.cursor.count) this.cursor = null;
    }
  }
  openGrid(size) {
    this.returnGrid();
    this.gridSize = size;
    this.grid = Array(size * size).fill(null);
  }
  returnGrid() {
    for (const stack of this.grid) {
      const rest = this.insert(stack);
      if (rest) this.overflow.push(rest);
    }
    this.grid.fill(null);
  }
  close() {
    this.returnGrid();
    const rest = this.insert(this.cursor);
    this.cursor = null;
    if (rest) this.overflow.push(rest);
    const overflow = this.overflow;
    this.overflow = [];
    return overflow;
  }
  recipe() {
    return matchRecipe(this.grid, this.gridSize);
  }
  craftOutput(shift = false) {
    let crafted = 0;
    do {
      const match = this.recipe();
      if (!match) break;
      const result = makeStack(match.recipe.output, match.recipe.count);
      if (shift) {
        if (this.freeFor(result) < result.count) break;
        this.insert(result);
      } else {
        if (
          this.cursor &&
          (!stackable(this.cursor, result) ||
            this.cursor.count + result.count > maxStack(result.id))
        )
          break;
        if (this.cursor) this.cursor.count += result.count;
        else this.cursor = result;
      }
      for (const index of match.slots) this.take('grid', index, 1);
      crafted += result.count;
      if (!shift) break;
    } while (crafted < 4096);
    return crafted;
  }
  fillRecipe(recipe, all = false) {
    if (recipe.width > this.gridSize || recipe.height > this.gridSize) return false;
    const backup = this.serialize();
    this.returnGrid();
    const bag = this.totals();
    let times = Math.min(...recipe.ingredients.map(([id, n]) => Math.floor((bag[id] || 0) / n)));
    if (!times) {
      this.restore(backup);
      return false;
    }
    times = all ? Math.min(times, 64) : 1;
    for (let y = 0; y < recipe.height; y++)
      for (let x = 0; x < recipe.width; x++) {
        const id = recipe.pattern[y * recipe.width + x];
        if (!id) continue;
        this.extract(id, times);
        this.grid[y * this.gridSize + x] = makeStack(id, times);
      }
    return true;
  }
  damageTool(index, amount = 1) {
    const stack = this.slots[index];
    if (!stack || !ITEMS[stack.id]?.durability) return false;
    stack.durability -= amount;
    if (stack.durability <= 0) {
      this.slots[index] = null;
      return true;
    }
    return false;
  }
}
