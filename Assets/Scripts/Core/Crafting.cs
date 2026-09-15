using System;
using System.Collections.Generic;

namespace VoxelWilds.Core
{
    public sealed class Recipe
    {
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public int[] Pattern { get; }
        public int Output { get; }
        public int Count { get; }
        public bool Shapeless { get; }
        public IReadOnlyDictionary<int, int> Ingredients { get; }
        public Recipe(string name, int width, int[] pattern, int output, int count = 1, bool shapeless = false)
        {
            Name = name;
            Width = width;
            Height = pattern.Length / width;
            Pattern = (int[])pattern.Clone();
            Output = output;
            Count = count;
            Shapeless = shapeless;
            var ingredients = new Dictionary<int, int>();
            foreach (int id in Pattern)
                if (id != 0) ingredients[id] = ingredients.TryGetValue(id, out int value) ? value + 1 : 1;
            Ingredients = ingredients;
        }
    }

    public static class Crafting
    {
        static Recipe Shape(string name, int width, int output, int count, params int[] pattern) => new Recipe(name, width, pattern, output, count);
        static Recipe Mix(string name, int output, int count, params int[] ingredients) => new Recipe(name, ingredients.Length, ingredients, output, count, true);
        public static IReadOnlyList<Recipe> Recipes { get; } = Array.AsReadOnly(new[]
        {
            Shape("Oak planks", 1, 7, 4, 5),
            Shape("Sticks", 1, Items.Stick, 4, 7, 7),
            Shape("Crafting table", 2, 16, 1, 7, 7, 7, 7),
            Shape("Wooden pickaxe", 3, Items.WoodenPickaxe, 1, 7, 7, 7, 0, 90, 0, 0, 90, 0),
            Shape("Stone pickaxe", 3, Items.StonePickaxe, 1, 8, 8, 8, 0, 90, 0, 0, 90, 0),
            Shape("Iron pickaxe", 3, Items.IronPickaxe, 1, 92, 92, 92, 0, 90, 0, 0, 90, 0),
            Shape("Crystal pickaxe", 3, Items.CrystalPickaxe, 1, 94, 94, 94, 0, 90, 0, 0, 90, 0),
            Shape("Iron sword", 1, Items.IronSword, 1, 92, 92, 90),
            Shape("Crystal sword", 1, Items.CrystalSword, 1, 94, 94, 90),
            Shape("Iron axe", 2, Items.IronAxe, 1, 92, 92, 92, 90, 0, 90),
            Shape("Iron shovel", 1, Items.IronShovel, 1, 92, 90, 90),
            Shape("Iron hoe", 2, Items.IronHoe, 1, 92, 92, 0, 90, 0, 90),
            Shape("Furnace", 3, 23, 1, 8, 8, 8, 8, 0, 8, 8, 8, 8),
            Shape("Torches", 1, 24, 4, 91, 90),
            Shape("Iron nuggets", 1, Items.IronNugget, 9, 92),
            Shape("Iron ingot", 3, Items.IronIngot, 1, 98, 98, 98, 98, 98, 98, 98, 98, 98),
            Shape("Lantern", 3, 17, 1, 98, 98, 98, 98, 24, 98, 98, 98, 98),
            Shape("Campfire", 3, 21, 1, 0, 90, 0, 90, 91, 90, 5, 5, 5),
            Shape("Bricks", 2, 10, 1, 97, 97, 97, 97),
            Shape("Clay", 2, 19, 1, 96, 96, 96, 96),
            Shape("Wool", 2, 20, 1, 99, 99, 99, 99),
            Shape("Iron helmet", 3, Items.IronHelmet, 1, 92, 92, 92, 92, 0, 92),
            Shape("Iron chestplate", 3, Items.IronChestplate, 1, 92, 0, 92, 92, 92, 92, 92, 92, 92),
            Shape("Iron leggings", 3, Items.IronLeggings, 1, 92, 92, 92, 92, 0, 92, 92, 0, 92),
            Shape("Iron boots", 3, Items.IronBoots, 1, 92, 0, 92, 92, 0, 92),
            Shape("Shield", 3, Items.Shield, 1, 7, 92, 7, 7, 7, 7, 0, 7, 0),
            Shape("Red bed", 3, 25, 1, 20, 20, 20, 7, 7, 7),
            Shape("Chest", 3, 41, 1, 7, 7, 7, 7, 0, 7, 7, 7, 7),
            Mix("Flint and steel", Items.FlintSteel, 1, 92, 129),
            Shape("Gold block", 3, 40, 1, 131, 131, 131, 131, 131, 131, 131, 131, 131),
            Shape("Gold ingots", 1, Items.GoldIngot, 9, 40),
            Shape("Gold nuggets", 1, Items.GoldNugget, 9, 131),
            Shape("Gold ingot", 3, Items.GoldIngot, 1, 138, 138, 138, 138, 138, 138, 138, 138, 138),
            Shape("Golden helmet", 3, Items.GoldHelmet, 1, 131, 131, 131, 131, 0, 131),
            Shape("Nether bricks", 2, 38, 1, 139, 139, 139, 139),
            Shape("Bucket", 3, Items.EmptyBucket, 1, 92, 0, 92, 0, 92, 0),
            Shape("Bow", 3, Items.Bow, 1, 0, 90, 99, 90, 0, 99, 0, 90, 99),
            Shape("Arrows", 1, Items.Arrow, 4, 129, 90, 133),
            Mix("Blaze powder", Items.BlazePowder, 2, 135),
            Mix("Eye of ender", Items.EyeEnder, 1, Items.EnderPearl, Items.BlazePowder),
            Shape("Bread", 3, Items.Bread, 1, Items.Wheat, Items.Wheat, Items.Wheat),
            Shape("Oak doors", 2, (int)Block.Door, 3, 7, 7, 7, 7, 7, 7)
        });

        public static Recipe Find(ItemStack[] grid, int width) => Match(grid, width, out _);
        public static ItemStack Preview(ItemStack[] grid, int width)
        {
            var recipe = Match(grid, width, out _);
            return recipe == null ? null : new ItemStack(recipe.Output, recipe.Count);
        }
        public static ItemStack Craft(ItemStack[] grid, int width)
        {
            var recipe = Match(grid, width, out var used);
            if (recipe == null) return null;
            foreach (int index in used)
            {
                grid[index].Count--;
                if (grid[index].Empty) grid[index] = null;
            }
            return new ItemStack(recipe.Output, recipe.Count);
        }
        public static bool TryCraftInto(ItemStack[] grid, int width, Inventory inventory)
        {
            var result = Preview(grid, width);
            if (result == null || inventory == null || !inventory.CanAdd(result.Id, result.Count)) return false;
            result = Craft(grid, width);
            inventory.Add(result.Id, result.Count, result.Durability);
            return true;
        }
        static Recipe Match(ItemStack[] grid, int size, out List<int> used)
        {
            used = new List<int>();
            if (grid == null || size < 2 || size > 3 || grid.Length != size * size) return null;
            int left = size, right = -1, top = size, bottom = -1;
            var totals = new Dictionary<int, int>();
            for (int i = 0; i < grid.Length; i++)
            {
                var stack = grid[i];
                if (stack == null || stack.Empty) continue;
                left = Math.Min(left, i % size); right = Math.Max(right, i % size);
                top = Math.Min(top, i / size); bottom = Math.Max(bottom, i / size);
                totals[stack.Id] = totals.TryGetValue(stack.Id, out int n) ? n + 1 : 1;
                used.Add(i);
            }
            if (right < 0) return null;
            int width = right - left + 1, height = bottom - top + 1;
            foreach (var recipe in Recipes)
            {
                if (recipe.Shapeless)
                {
                    if (recipe.Ingredients.Count != totals.Count) continue;
                    bool matches = true;
                    foreach (var pair in recipe.Ingredients)
                        if (!totals.TryGetValue(pair.Key, out int amount) || amount != pair.Value) { matches = false; break; }
                    if (matches) return recipe;
                    continue;
                }
                if (recipe.Width != width || recipe.Height != height) continue;
                for (int flip = 0; flip < 2; flip++)
                {
                    bool matches = true;
                    for (int y = 0; y < height && matches; y++)
                        for (int x = 0; x < width; x++)
                        {
                            var stack = grid[(top + y) * size + left + x];
                            int actual = stack == null || stack.Empty ? 0 : stack.Id;
                            int expected = recipe.Pattern[y * width + (flip == 1 ? width - 1 - x : x)];
                            if (actual != expected) { matches = false; break; }
                        }
                    if (matches) return recipe;
                }
            }
            used.Clear();
            return null;
        }
    }
}
