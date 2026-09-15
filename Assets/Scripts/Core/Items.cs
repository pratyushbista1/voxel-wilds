using System;
using System.Collections.Generic;

namespace VoxelWilds.Core
{
    [Serializable]
    public sealed class ItemStack
    {
        public int Id;
        public int Count;
        public int Durability;

        public ItemStack() { }
        public ItemStack(int id, int count = 1, int durability = -1)
        {
            Id = id;
            Count = Math.Max(0, count);
            int maximum = Items.Durability(id);
            Durability = maximum > 0 ? (durability < 0 ? maximum : Math.Min(maximum, Math.Max(0, durability))) : 0;
        }
        public bool Empty => Id == 0 || Count <= 0;
        public ItemStack Clone() => new ItemStack(Id, Count, Durability);
    }

    public static class Items
    {
        public const int Stick = 90, Coal = 91, IronIngot = 92, Berries = 93, Crystal = 94, RawIron = 95,
            ClayBall = 96, Brick = 97, IronNugget = 98, String = 99, WoodenPickaxe = 100,
            StonePickaxe = 101, IronPickaxe = 102, IronSword = 103, CrystalPickaxe = 104,
            IronHelmet = 110, IronChestplate = 111, IronLeggings = 112, IronBoots = 113, Shield = 114,
            RawMutton = 120, CookedMutton = 121, RawBeef = 122, Steak = 123, RawPorkchop = 124,
            CookedPorkchop = 125, RawChicken = 126, CookedChicken = 127, RottenFlesh = 128,
            Flint = 129, FlintSteel = 130, GoldIngot = 131, Leather = 132, Feather = 133, Bone = 134,
            BlazeRod = 135, Quartz = 137, GoldNugget = 138, NetherBrick = 139, GoldHelmet = 150,
            ZombieEgg = 160, CowEgg = 161, PigEgg = 162, SheepEgg = 163, ChickenEgg = 164,
            BlazeEgg = 165, PiglinEgg = 166, SkeletonEgg = 167, CreeperEgg = 168, SpiderEgg = 169,
            BruteEgg = 170, WitherSkeletonEgg = 171, EndermanEgg = 172, DragonEgg = 173, VillagerEgg = 174,
            EmptyBucket = 180, WaterBucket = 181, LavaBucket = 182, Bow = 183, Arrow = 184,
            EnderPearl = 185, EyeEnder = 186, BlazePowder = 187, Wheat = 188, Bread = 189,
            Seeds = 190, CrystalSword = 191, IronAxe = 192, IronShovel = 193, IronHoe = 194, Apple = 195;

        static readonly string[] MobNames = { "zombie", "cow", "pig", "sheep", "chicken", "blaze", "piglin", "skeleton", "creeper", "spider", "brute", "wither_skeleton", "enderman", "dragon", "villager" };
        static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            [Stick] = "Stick", [Coal] = "Coal", [IronIngot] = "Iron ingot", [Berries] = "Wild berries",
            [Crystal] = "Crystal", [RawIron] = "Raw iron", [ClayBall] = "Clay ball", [Brick] = "Brick",
            [IronNugget] = "Iron nugget", [String] = "String", [WoodenPickaxe] = "Wooden pickaxe",
            [StonePickaxe] = "Stone pickaxe", [IronPickaxe] = "Iron pickaxe", [IronSword] = "Iron sword",
            [CrystalPickaxe] = "Crystal pickaxe", [IronHelmet] = "Iron helmet", [IronChestplate] = "Iron chestplate",
            [IronLeggings] = "Iron leggings", [IronBoots] = "Iron boots", [Shield] = "Shield",
            [RawMutton] = "Raw mutton", [CookedMutton] = "Cooked mutton", [RawBeef] = "Raw beef", [Steak] = "Steak",
            [RawPorkchop] = "Raw porkchop", [CookedPorkchop] = "Cooked porkchop", [RawChicken] = "Raw chicken",
            [CookedChicken] = "Cooked chicken", [RottenFlesh] = "Rotten flesh", [Flint] = "Flint",
            [FlintSteel] = "Flint and steel", [GoldIngot] = "Gold ingot", [Leather] = "Leather", [Feather] = "Feather",
            [Bone] = "Bone", [BlazeRod] = "Blaze rod", [Quartz] = "Quartz", [GoldNugget] = "Gold nugget",
            [NetherBrick] = "Nether brick", [GoldHelmet] = "Golden helmet", [EmptyBucket] = "Bucket",
            [WaterBucket] = "Water bucket", [LavaBucket] = "Lava bucket", [Bow] = "Bow", [Arrow] = "Arrow",
            [EnderPearl] = "Ender pearl", [EyeEnder] = "Eye of ender", [BlazePowder] = "Blaze powder",
            [Wheat] = "Wheat", [Bread] = "Bread", [Seeds] = "Wheat seeds", [CrystalSword] = "Crystal sword",
            [IronAxe] = "Iron axe", [IronShovel] = "Iron shovel", [IronHoe] = "Iron hoe", [Apple] = "Apple"
        };

        public static bool Exists(int id) => id > 0 && id <= (int)Block.EndFrame || Names.ContainsKey(id) || SpawnMob(id) != null;
        public static string Name(int id)
        {
            if (id >= 0 && id <= (int)Block.EndFrame) return Blocks.Name((Block)id);
            if (Names.TryGetValue(id, out var name)) return name;
            string mob = SpawnMob(id);
            return mob != null ? char.ToUpperInvariant(mob[0]) + mob.Substring(1).Replace('_', ' ') + " spawn egg" : "Unknown item";
        }
        public static string SpawnMob(int id) => id >= ZombieEgg && id <= VillagerEgg ? MobNames[id - ZombieEgg] : null;
        public static int MaxStack(int id)
        {
            if (!Exists(id)) return 0;
            if (Durability(id) > 0 || id == (int)Block.Bed || id >= 26 && id <= 32 || id == WaterBucket || id == LavaBucket) return 1;
            if (id == EmptyBucket || id == EnderPearl) return 16;
            return 64;
        }
        public static int Durability(int id)
        {
            switch (id)
            {
                case WoodenPickaxe: return 59; case StonePickaxe: return 131;
                case IronPickaxe: case IronSword: case IronAxe: case IronShovel: case IronHoe: return 250;
                case CrystalPickaxe: case CrystalSword: return 1561;
                case IronHelmet: return 165; case IronChestplate: return 240;
                case IronLeggings: return 225; case IronBoots: return 195;
                case GoldHelmet: return 77; case Shield: return 336; case Bow: return 384;
                case FlintSteel: return 64; default: return 0;
            }
        }
        public static bool IsTool(int id) => MiningTier(id) > 0 || id == IronSword || id == CrystalSword || id == IronAxe || id == IronShovel || id == IronHoe || id == FlintSteel || id == Bow;
        public static int ArmorSlot(int id)
        {
            if (id == GoldHelmet) return 0;
            return id >= IronHelmet && id <= IronBoots ? id - IronHelmet : -1;
        }
        public static int Protection(int id)
        {
            switch (id) { case IronHelmet: case IronBoots: case GoldHelmet: return 2; case IronChestplate: return 6; case IronLeggings: return 5; default: return 0; }
        }
        public static bool IsFood(int id) => Food(id) > 0;
        public static int Food(int id)
        {
            switch (id)
            {
                case Berries: case RawMutton: case RawChicken: return 2;
                case RawBeef: case RawPorkchop: return 3;
                case RottenFlesh: case Apple: return 4; case Bread: return 5;
                case CookedMutton: case CookedChicken: return 6;
                case Steak: case CookedPorkchop: return 8; default: return 0;
            }
        }
        public static float Saturation(int id)
        {
            switch (id)
            {
                case RottenFlesh: return 0.8f; case Berries: return 1.2f;
                case RawChicken: case RawMutton: return 1.2f;
                case RawBeef: case RawPorkchop: return 1.8f;
                case Apple: return 2.4f; case Bread: return 6;
                case CookedChicken: return 7.2f; case CookedMutton: return 9.6f;
                case Steak: case CookedPorkchop: return 12.8f; default: return 0;
            }
        }
        public static Block PlaceBlock(int id)
        {
            if (id == Seeds) return Block.Crop;
            if (id == WaterBucket) return Block.Water;
            if (id == LavaBucket) return Block.Lava;
            return id > 0 && id <= (int)Block.EndFrame ? (Block)id : Block.Air;
        }
        public static int MiningTier(int id)
        {
            switch (id) { case WoodenPickaxe: return 1; case StonePickaxe: return 2; case IronPickaxe: return 3; case CrystalPickaxe: return 4; default: return 0; }
        }
        static bool RequiresPick(Block block)
        {
            switch (block)
            {
                case Block.Stone: case Block.Cobble: case Block.CoalOre: case Block.IronOre: case Block.CrystalOre:
                case Block.Furnace: case Block.Basalt: case Block.Bricks: case Block.Obsidian: case Block.Netherrack:
                case Block.NetherBricks: case Block.Blackstone: case Block.GoldBlock: case Block.NetherGold:
                case Block.EndStone: case Block.Spawner: return true;
                default: return false;
            }
        }
        public static bool CanHarvest(int tool, Block block)
        {
            if (float.IsInfinity(Blocks.Hardness(block))) return false;
            if (!RequiresPick(block)) return true;
            int needed = block == Block.Obsidian ? 4 : block == Block.CrystalOre || block == Block.GoldBlock ? 3 : block == Block.IronOre ? 2 : 1;
            return MiningTier(tool) >= needed;
        }
        public static float MiningSpeed(int tool, Block block)
        {
            if (RequiresPick(block))
            {
                switch (tool) { case WoodenPickaxe: return 2; case StonePickaxe: return 4; case IronPickaxe: return 6; case CrystalPickaxe: return 8; }
            }
            if (tool == IronAxe && (block == Block.Log || block == Block.Planks || block == Block.Workbench || block == Block.Chest || block == Block.Door)) return 6;
            if (tool == IronShovel && (block == Block.Grass || block == Block.Dirt || block == Block.Sand || block == Block.Gravel || block == Block.Snow || block == Block.Clay || block == Block.SoulSand || block == Block.Farmland)) return 6;
            if ((tool == IronSword || tool == CrystalSword) && (block == Block.Leaves || block == Block.Wool)) return 1.5f;
            return 1;
        }
        public static int AttackDamage(int id)
        {
            switch (id)
            {
                case IronSword: return 6; case CrystalSword: return 7; case IronAxe: return 9;
                case WoodenPickaxe: return 2; case StonePickaxe: return 3;
                case IronPickaxe: case IronShovel: return 4; case CrystalPickaxe: return 5;
                default: return 1;
            }
        }
        public static int[] CreateCreativeInventory()
        {
            var ids = new List<int>();
            for (int id = 1; id <= (int)Block.EndFrame; id++)
                if (!(id >= 26 && id <= 32) && id != 34 && id != 35 && id != 49 && id != 50 && id != 51) ids.Add(id);
            ids.AddRange(Names.Keys);
            for (int id = ZombieEgg; id <= VillagerEgg; id++) ids.Add(id);
            ids.Sort();
            return ids.ToArray();
        }
    }
}
