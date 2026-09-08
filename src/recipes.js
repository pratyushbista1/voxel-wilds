function shaped(id, width, pattern, output, count = 1) {
  const totals = new Map();
  for (const item of pattern) if (item) totals.set(item, (totals.get(item) || 0) + 1);
  return {
    id,
    width,
    height: pattern.length / width,
    pattern,
    output,
    count,
    ingredients: [...totals],
  };
}

export const RECIPES = [
  shaped('planks', 1, [5], 7, 4),
  shaped('sticks', 1, [7, 7], 90, 4),
  shaped('workbench', 2, [7, 7, 7, 7], 16),
  shaped('woodpick', 3, [7, 7, 7, 0, 90, 0, 0, 90, 0], 100),
  shaped('stonepick', 3, [8, 8, 8, 0, 90, 0, 0, 90, 0], 101),
  shaped('ironpick', 3, [92, 92, 92, 0, 90, 0, 0, 90, 0], 102),
  shaped('crystalpick', 3, [94, 94, 94, 0, 90, 0, 0, 90, 0], 104),
  shaped('sword', 1, [92, 92, 90], 103),
  shaped('furnace', 3, [8, 8, 8, 8, 0, 8, 8, 8, 8], 23),
  shaped('torch', 1, [91, 90], 24, 4),
  shaped('nuggets', 1, [92], 98, 9),
  shaped('ingot', 3, [98, 98, 98, 98, 98, 98, 98, 98, 98], 92),
  shaped('lantern', 3, [98, 98, 98, 98, 24, 98, 98, 98, 98], 17),
  shaped('campfire', 3, [0, 90, 0, 90, 91, 90, 5, 5, 5], 21),
  shaped('bricks', 2, [97, 97, 97, 97], 10),
  shaped('clay', 2, [96, 96, 96, 96], 19),
  shaped('wool', 2, [99, 99, 99, 99], 20),
  shaped('helmet', 3, [92, 92, 92, 92, 0, 92], 110),
  shaped('chestplate', 3, [92, 0, 92, 92, 92, 92, 92, 92, 92], 111),
  shaped('leggings', 3, [92, 92, 92, 92, 0, 92, 92, 0, 92], 112),
  shaped('boots', 3, [92, 0, 92, 92, 0, 92], 113),
  shaped('shield', 3, [7, 92, 7, 7, 7, 7, 0, 7, 0], 114),
];

export function matchRecipe(grid, size) {
  let left = size,
    right = -1,
    top = size,
    bottom = -1;
  for (let i = 0; i < grid.length; i++)
    if (grid[i]) {
      left = Math.min(left, i % size);
      right = Math.max(right, i % size);
      top = Math.min(top, Math.floor(i / size));
      bottom = Math.max(bottom, Math.floor(i / size));
    }
  if (right < 0) return null;
  const width = right - left + 1,
    height = bottom - top + 1;
  for (const recipe of RECIPES) {
    if (recipe.width !== width || recipe.height !== height) continue;
    for (const mirrored of [false, true]) {
      const slots = [];
      let matches = true;
      for (let y = 0; y < height; y++)
        for (let x = 0; x < width; x++) {
          const index = (top + y) * size + left + x;
          const expected = recipe.pattern[y * width + (mirrored ? width - x - 1 : x)];
          if ((grid[index]?.id || 0) !== expected) matches = false;
          if (expected) slots.push(index);
        }
      if (matches) return { recipe, slots };
    }
  }
  return null;
}

export const SMELTING = { 4: 9, 95: 92, 96: 97, 8: 3, 5: 91 };
export const FUEL = { 91: 80, 5: 15, 7: 15, 90: 5, 16: 15, 100: 10 };
