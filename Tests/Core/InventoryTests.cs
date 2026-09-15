using System;
using System.Linq;
using VoxelWilds.Core;

public static class InventoryTests
{
    static ItemStack[] Grid(params int[] ids) => ids.Select(id => id == 0 ? null : new ItemStack(id)).ToArray();
    static int Total(Inventory bag) => bag.Slots.Where(s => s != null).Sum(s => s.Count);
    static void Near(float a, float b, string message) => Spec.True(Math.Abs(a - b) < 0.01f, message + "; " + a + " != " + b);

    public static void Run()
    {
        Spec.Run("item stacks clone independently and initialize durability", () =>
        {
            var stack = new ItemStack(Items.IronPickaxe);
            Spec.Equal(250, stack.Durability);
            var copy = stack.Clone();
            copy.Durability -= 10;
            Spec.Equal(250, stack.Durability);
            Spec.Equal(240, copy.Durability);
            Spec.True(new ItemStack(0).Empty);
            Spec.True(new ItemStack(Items.Coal, 0).Empty);
            Spec.Equal(250, new ItemStack(Items.IronPickaxe, 1, 999).Durability);
        });
        Spec.Run("creative catalog has valid unique IDs and all new mobs", () =>
        {
            var ids = Items.CreateCreativeInventory();
            Spec.Equal(ids.Length, ids.Distinct().Count());
            Spec.True(ids.All(id => Items.Exists(id) && Items.MaxStack(id) > 0 && Items.Name(id) != "Unknown item"));
            Spec.True(ids.Contains(Items.EndermanEgg) && ids.Contains(Items.DragonEgg) && ids.Contains(Items.VillagerEgg));
            Spec.Equal("dragon", Items.SpawnMob(Items.DragonEgg));
            Spec.Equal<string>(null, Items.SpawnMob(Items.Bread));
            Spec.Equal(16, Items.MaxStack(Items.EmptyBucket));
            Spec.Equal(1, Items.MaxStack(Items.WaterBucket));
            Spec.Equal(1, Items.MaxStack((int)Block.Bed));
        });
        Spec.Run("inventory splits stacks and fills partial stacks first", () =>
        {
            var bag = new Inventory();
            Spec.Equal(0, bag.Add(Items.Coal, 70));
            Spec.Equal(64, bag.Slots[0].Count);
            Spec.Equal(6, bag.Slots[1].Count);
            bag.Slots[0] = null;
            bag.Add(Items.Coal, 60);
            Spec.Equal(64, bag.Slots[1].Count);
            Spec.Equal(2, bag.Slots[0].Count);
            Spec.Equal(66, bag.Count(Items.Coal));
        });
        Spec.Run("full inventory reports leftovers without deleting items", () =>
        {
            var bag = new Inventory();
            Spec.Equal(5, bag.Add((int)Block.Stone, 36 * 64 + 5));
            Spec.Equal(36 * 64, Total(bag));
            Spec.Equal(0, bag.CapacityFor((int)Block.Stone));
            Spec.True(!bag.CanAdd(Items.Coal, 1));
            Spec.Equal(3, bag.Add(9999, 3));
            Spec.Equal(1, bag.Add(Items.IronPickaxe, 1));
            Spec.Equal(36 * 64, Total(bag));
        });
        Spec.Run("tools never merge and retain their individual durability", () =>
        {
            var bag = new Inventory();
            bag.Add(Items.IronPickaxe, 2, 31);
            bag.Add(Items.IronPickaxe, 1, 90);
            Spec.Equal(1, bag.Slots[0].Count);
            Spec.Equal(31, bag.Slots[1].Durability);
            Spec.Equal(90, bag.Slots[2].Durability);
            Spec.Equal(1, bag.Add(Items.IronPickaxe, 1, 0));
            Spec.Equal(3, bag.Count(Items.IronPickaxe));
        });
        Spec.Run("removing materials is atomic and rejects negative counts", () =>
        {
            var bag = new Inventory();
            bag.Add(Items.Coal, 70);
            Spec.True(!bag.Remove(Items.Coal, 71));
            Spec.Equal(70, bag.Count(Items.Coal));
            Spec.True(!bag.Remove(Items.Coal, -1));
            Spec.True(bag.Remove(Items.Coal, 65));
            Spec.Equal<ItemStack>(null, bag.Slots[0]);
            Spec.Equal(5, bag.Count(Items.Coal));
        });
        Spec.Run("selected tool wears exactly once and is removed on break", () =>
        {
            var bag = new Inventory();
            bag.Add(Items.IronPickaxe, 1, 2);
            Spec.True(!bag.WearSelected(1));
            Spec.Equal(1, bag.Held.Durability);
            Spec.True(!bag.WearSelected(-5));
            Spec.True(bag.WearSelected(1));
            Spec.Equal<ItemStack>(null, bag.Held);
            Spec.True(!bag.WearSelected(1));
            bag.Selected = -1;
            Spec.Equal<ItemStack>(null, bag.Held);
        });
        Spec.Run("transfers split, merge to cap and swap without aliasing", () =>
        {
            var bag = new Inventory();
            bag.Add(Items.Coal, 9);
            bag.Transfer(0, 1, true);
            Spec.Equal(4, bag.Slots[0].Count);
            Spec.Equal(5, bag.Slots[1].Count);
            bag.Slots[2] = new ItemStack(Items.Coal, 62);
            bag.Transfer(1, 2);
            Spec.Equal(3, bag.Slots[1].Count);
            Spec.Equal(64, bag.Slots[2].Count);
            bag.Slots[3] = new ItemStack(Items.IronSword, 1, 12);
            bag.Transfer(2, 3, true);
            Spec.Equal(Items.IronSword, bag.Slots[3].Id);
            bag.Transfer(2, 3);
            Spec.Equal(12, bag.Slots[2].Durability);
            Spec.Equal(64, bag.Slots[3].Count);
            bag.Transfer(0, 0);
            Spec.Equal(4, bag.Slots[0].Count);
        });
        Spec.Run("random transfers conserve every item and enforce caps", () =>
        {
            var bag = new Inventory();
            bag.Add(Items.Coal, 256); bag.Add(Items.IronPickaxe, 7); bag.Add(Items.EmptyBucket, 40);
            var random = new Random(1729);
            for (int i = 0; i < 3000; i++)
            {
                bag.Transfer(random.Next(36), random.Next(36), random.Next(2) == 0);
                Spec.Equal(256, bag.Count(Items.Coal));
                Spec.Equal(7, bag.Count(Items.IronPickaxe));
                Spec.Equal(40, bag.Count(Items.EmptyBucket));
                Spec.True(bag.Slots.All(s => s == null || s.Count > 0 && s.Count <= Items.MaxStack(s.Id)));
            }
        });
        Spec.Run("quick move never swaps or reorders a full hotbar", () =>
        {
            var bag = new Inventory();
            for (int i = 0; i < 9; i++) bag.Slots[i] = new ItemStack(i + 1, 64);
            bag.Slots[9] = new ItemStack(Items.Coal, 17);
            var before = bag.Slots.Select(s => s?.Clone()).ToArray();
            Spec.True(!bag.QuickMove(9));
            for (int i = 0; i < 10; i++)
            {
                Spec.Equal(before[i].Id, bag.Slots[i].Id);
                Spec.Equal(before[i].Count, bag.Slots[i].Count);
            }
        });
        Spec.Run("quick move merges before filling empty destination slots", () =>
        {
            var bag = new Inventory();
            bag.Slots[0] = new ItemStack(Items.IronIngot, 8);
            bag.Slots[2] = new ItemStack(Items.Coal, 62);
            bag.Slots[9] = new ItemStack(Items.Coal, 17);
            Spec.True(bag.QuickMove(9));
            Spec.Equal(Items.IronIngot, bag.Slots[0].Id);
            Spec.Equal(8, bag.Slots[0].Count);
            Spec.Equal(64, bag.Slots[2].Count);
            Spec.Equal(15, bag.Slots[1].Count);
            Spec.Equal<ItemStack>(null, bag.Slots[9]);
            Spec.True(bag.QuickMove(1));
            Spec.Equal(15, bag.Slots[9].Count);
            Spec.Equal(79, bag.Count(Items.Coal));
        });
        Spec.Run("random quick moves conserve quantities and durability", () =>
        {
            var bag = new Inventory();
            bag.Add(Items.Coal, 600); bag.Add(Items.EmptyBucket, 80); bag.Add(Items.IronPickaxe, 5, 17);
            var random = new Random(175);
            for (int i = 0; i < 2000; i++)
            {
                bag.QuickMove(random.Next(36));
                Spec.Equal(600, bag.Count(Items.Coal));
                Spec.Equal(80, bag.Count(Items.EmptyBucket));
                Spec.Equal(5, bag.Count(Items.IronPickaxe));
                Spec.True(bag.Slots.Where(s => s?.Id == Items.IronPickaxe).All(s => s.Durability == 17));
                Spec.True(bag.Slots.All(s => s == null || s.Count > 0 && s.Count <= Items.MaxStack(s.Id)));
            }
            Spec.True(!bag.QuickMove(-1) && !bag.QuickMove(36));
        });
        Spec.Run("death drops include armor and offhand once", () =>
        {
            var bag = new Inventory();
            bag.Add(Items.Coal, 5);
            bag.Armor[0] = new ItemStack(Items.IronHelmet, 1, 33);
            bag.Offhand = new ItemStack(Items.Shield, 1, 30);
            var drops = bag.TakeAll();
            Spec.Equal(7, drops.Sum(s => s.Count));
            Spec.Equal(33, drops.Single(s => s.Id == Items.IronHelmet).Durability);
            Spec.Equal(0, bag.TakeAll().Count);
            Spec.True(bag.Slots.All(s => s == null) && bag.Armor.All(s => s == null) && bag.Offhand == null);
        });
        Spec.Run("craft preview does not consume and recipes translate in grid", () =>
        {
            var grid = Grid(0, 0, 0, 5);
            var result = Crafting.Preview(grid, 2);
            Spec.Equal(4, result.Count);
            Spec.Equal((int)Block.Planks, result.Id);
            Spec.Equal(1, grid[3].Count);
            Spec.Equal(4, Crafting.Craft(grid, 2).Count);
            Spec.True(grid.All(s => s == null));
            var sticks = Grid(0, 0, 0, 0, 0, 7, 0, 0, 7);
            Spec.Equal(Items.Stick, Crafting.Preview(sticks, 3).Id);
        });
        Spec.Run("asymmetric axe recipes mirror correctly", () =>
        {
            var left = Grid(92, 92, 0, 92, 90, 0, 0, 90, 0);
            var right = Grid(0, 92, 92, 0, 90, 92, 0, 90, 0);
            Spec.Equal(Items.IronAxe, Crafting.Preview(left, 3).Id);
            Spec.Equal(Items.IronAxe, Crafting.Preview(right, 3).Id);
            right[6] = new ItemStack(Items.Coal);
            Spec.Equal<ItemStack>(null, Crafting.Preview(right, 3));
        });
        Spec.Run("crafting rejects wrong shapes and insufficient grid sizes", () =>
        {
            var wrong = Grid(7, 7, 7, 0, 90, 0, 90, 0, 0);
            Spec.Equal<ItemStack>(null, Crafting.Craft(wrong, 3));
            Spec.Equal(5, wrong.Count(s => s != null));
            Spec.Equal<ItemStack>(null, Crafting.Preview(Grid(92, 92, 90, 90), 2));
            Spec.Equal<ItemStack>(null, Crafting.Preview(Grid(7, 7, 7, 7), 3));
            Spec.Equal<ItemStack>(null, Crafting.Preview(null, 2));
        });
        Spec.Run("shapeless recipes require separate occupied ingredients", () =>
        {
            var eyes = Grid(0, Items.BlazePowder, Items.EnderPearl, 0);
            eyes[1].Count = 10; eyes[2].Count = 2;
            Spec.Equal(Items.EyeEnder, Crafting.Craft(eyes, 2).Id);
            Spec.Equal(9, eyes[1].Count); Spec.Equal(1, eyes[2].Count);
            eyes[0] = new ItemStack(Items.BlazePowder);
            Spec.Equal<ItemStack>(null, Crafting.Preview(eyes, 2));
            Spec.Equal(Items.FlintSteel, Crafting.Craft(Grid(129, 92, 0, 0), 2).Id);
        });
        Spec.Run("crafting cannot consume inputs when destination is full", () =>
        {
            var bag = new Inventory();
            bag.Add(Items.Coal, 36 * 64);
            var grid = Grid(5, 0, 0, 0);
            Spec.True(!Crafting.TryCraftInto(grid, 2, bag));
            Spec.Equal(1, grid[0].Count);
            bag.Slots[0] = new ItemStack((int)Block.Planks, 62);
            Spec.True(!Crafting.TryCraftInto(grid, 2, bag));
            Spec.Equal(1, grid[0].Count);
            bag.Slots[0].Count = 60;
            Spec.True(Crafting.TryCraftInto(grid, 2, bag));
            Spec.Equal(64, bag.Slots[0].Count);
            Spec.Equal<ItemStack>(null, grid[0]);
        });
        Spec.Run("all recipes have valid ingredients and produce declared outputs", () =>
        {
            foreach (var recipe in Crafting.Recipes)
            {
                Spec.True(Items.Exists(recipe.Output), recipe.Name);
                Spec.True(recipe.Ingredients.Keys.All(Items.Exists), recipe.Name);
                var grid = new ItemStack[9];
                for (int y = 0; y < recipe.Height; y++)
                    for (int x = 0; x < recipe.Width; x++)
                    {
                        int id = recipe.Pattern[y * recipe.Width + x];
                        if (id != 0) grid[y * 3 + x] = new ItemStack(id);
                    }
                var output = Crafting.Craft(grid, 3);
                Spec.True(output != null, recipe.Name);
                Spec.Equal(recipe.Output, output.Id, recipe.Name);
                Spec.Equal(recipe.Count, output.Count, recipe.Name);
                Spec.True(grid.All(s => s == null), recipe.Name);
            }
        });
        Spec.Run("food, mining and armor metadata distinguish effective tools", () =>
        {
            Spec.Equal(8, Items.Food(Items.Steak));
            Spec.True(!Items.IsFood(Items.Coal));
            Spec.Equal(0, Items.ArmorSlot(Items.GoldHelmet));
            Spec.Equal(6, Items.Protection(Items.IronChestplate));
            Spec.True(Items.CanHarvest(Items.CrystalPickaxe, Block.Obsidian));
            Spec.True(!Items.CanHarvest(Items.IronPickaxe, Block.Obsidian));
            Spec.True(!Items.CanHarvest(Items.CrystalPickaxe, Block.Bedrock));
            Spec.Equal(1f, Items.MiningSpeed(Items.IronSword, Block.Stone));
            Spec.Equal(6f, Items.MiningSpeed(Items.IronAxe, Block.Log));
            Spec.Equal(Block.Water, Items.PlaceBlock(Items.WaterBucket));
            Spec.Equal(Block.Air, Items.PlaceBlock(Items.EnderPearl));
        });
        Spec.Run("coal burns for eight complete ten second smelts", () =>
        {
            var furnace = new Furnace { Input = new ItemStack(Items.RawIron, 10), Fuel = new ItemStack(Items.Coal) };
            Spec.Equal(8, furnace.Tick(80));
            Spec.Equal(8, furnace.Output.Count);
            Spec.Equal(Items.IronIngot, furnace.Output.Id);
            Spec.Equal(2, furnace.Input.Count);
            Near(0, furnace.BurnRemaining, "Coal exhausted");
            Spec.Equal<ItemStack>(null, furnace.Fuel);
            Spec.Equal(0, furnace.Tick(10));
        });
        Spec.Run("furnace supports multiple fuels within a single large tick", () =>
        {
            var furnace = new Furnace { Input = new ItemStack((int)Block.Sand, 4), Fuel = new ItemStack(Items.Stick, 8) };
            Spec.Equal(4, furnace.Tick(40));
            Spec.Equal((int)Block.Glass, furnace.Output.Id);
            Spec.Equal<ItemStack>(null, furnace.Fuel);
            Spec.Equal<ItemStack>(null, furnace.Input);
        });
        Spec.Run("lava fuel returns exactly one empty bucket", () =>
        {
            var furnace = new Furnace { Input = new ItemStack(Items.RawBeef, 64), Fuel = new ItemStack(Items.LavaBucket) };
            Spec.Equal(64, furnace.Tick(640));
            Spec.Equal(64, furnace.Output.Count);
            Spec.Equal(Items.Steak, furnace.Output.Id);
            Spec.Equal(Items.EmptyBucket, furnace.Fuel.Id);
            Spec.Equal(1, furnace.Fuel.Count);
            Near(360, furnace.BurnRemaining, "Lava burn remainder");
            furnace.Tick(400);
            Near(0, furnace.BurnRemaining, "Unused heat still burns");
            Spec.Equal(1, furnace.Fuel.Count);
        });
        Spec.Run("blocked furnace output consumes no fresh fuel or input", () =>
        {
            var furnace = new Furnace { Input = new ItemStack(Items.RawChicken, 3), Fuel = new ItemStack(Items.Coal, 2), Output = new ItemStack(Items.CookedChicken, 64) };
            Spec.Equal(0, furnace.Tick(60));
            Spec.Equal(2, furnace.Fuel.Count);
            Spec.Equal(3, furnace.Input.Count);
            furnace.Output = new ItemStack(Items.Steak);
            Spec.Equal(0, furnace.Tick(20));
            Spec.Equal(2, furnace.Fuel.Count);
            furnace.Output = null;
            Spec.Equal(1, furnace.Tick(10));
            Spec.Equal(Items.CookedChicken, furnace.Output.Id);
        });
        Spec.Run("changing furnace input resets partial progress", () =>
        {
            var furnace = new Furnace { Input = new ItemStack(Items.RawMutton), Fuel = new ItemStack(Items.Coal) };
            furnace.Tick(9);
            Near(9, furnace.CookProgress, "Partial smelt");
            furnace.Input = new ItemStack(Items.ClayBall);
            Spec.Equal(0, furnace.Tick(1));
            Near(1, furnace.CookProgress, "New ingredient progress");
            Spec.Equal(1, furnace.Tick(9));
            Spec.Equal(Items.Brick, furnace.Output.Id);
        });
        Spec.Run("furnace split ticks agree with a single elapsed interval", () =>
        {
            var one = new Furnace { Input = new ItemStack(Items.RawPorkchop, 40), Fuel = new ItemStack(Items.Coal, 5) };
            var many = new Furnace { Input = new ItemStack(Items.RawPorkchop, 40), Fuel = new ItemStack(Items.Coal, 5) };
            one.Tick(97.5f);
            for (int i = 0; i < 390; i++) many.Tick(0.25f);
            Spec.Equal(one.Output.Count, many.Output.Count);
            Spec.Equal(one.Input.Count, many.Input.Count);
            Spec.Equal(one.Fuel.Count, many.Fuel.Count);
            Near(one.BurnRemaining, many.BurnRemaining, "Split burn");
            Near(one.CookProgress, many.CookProgress, "Split progress");
        });
        Spec.Run("invalid furnace time cannot create fuel or items", () =>
        {
            var furnace = new Furnace { Input = new ItemStack(Items.RawIron), Fuel = new ItemStack(Items.Coal) };
            furnace.Tick(-10); furnace.Tick(float.NaN); furnace.Tick(float.PositiveInfinity);
            Spec.Equal(1, furnace.Input.Count);
            Spec.Equal(1, furnace.Fuel.Count);
            Spec.Equal<ItemStack>(null, furnace.Output);
            Spec.Equal(0, furnace.Tick(0));
        });
    }
}
