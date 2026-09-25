namespace VoxelWilds.Core
{
    public enum Dimension { Overworld, Nether, End }

    public enum Block : ushort
    {
        Air = 0, Grass = 1, Dirt = 2, Stone = 3, Sand = 4, Log = 5, Leaves = 6, Planks = 7,
        Cobble = 8, Glass = 9, Bricks = 10, Water = 11, CoalOre = 12, IronOre = 13,
        CrystalOre = 14, Snow = 15, Workbench = 16, Lantern = 17, Basalt = 18, Clay = 19,
        Wool = 20, Campfire = 21, Bedrock = 22, Furnace = 23, Torch = 24, Bed = 25,
        BedHead = 26, BedEast = 27, BedEastHead = 28, BedSouth = 29, BedSouthHead = 30,
        BedWest = 31, BedWestHead = 32, Obsidian = 33, PortalX = 34, PortalZ = 35,
        Netherrack = 36, Lava = 37, NetherBricks = 38, Blackstone = 39, GoldBlock = 40,
        Chest = 41, SoulSand = 42, Glowstone = 43, Gravel = 44, NetherGold = 45,
        NetherWart = 46, Spawner = 47, EndStone = 48, EndPortal = 49, Farmland = 50,
        Crop = 51, Door = 52, EndFrame = 53
    }

    public static class Blocks
    {
        public static bool IsFluid(Block id) => id == Block.Water || id == Block.Lava;
        public static bool IsSolid(Block id) => id != Block.Air && !IsFluid(id) && id != Block.Torch && id != Block.Lantern && id != Block.Campfire && id != Block.PortalX && id != Block.PortalZ && id != Block.NetherWart && id != Block.Crop && id != Block.EndPortal;
        public static bool IsTransparent(Block id) => !IsSolid(id) || id == Block.Glass || id == Block.Leaves || id == Block.Chest || id == Block.Spawner || IsBed(id) || id == Block.Door;
        public static bool IsBed(Block id) => id >= Block.Bed && id <= Block.BedWestHead;
        public static bool IsReplaceable(Block id) => id == Block.Air || IsFluid(id) || id == Block.PortalX || id == Block.PortalZ || id == Block.Crop || id == Block.NetherWart || id == Block.Torch || id == Block.Lantern || id == Block.Campfire;
        public static uint ColorRgb(Block id)
        {
            if (IsBed(id)) return 0xb93f43;
            switch (id)
            {
                case Block.Grass: return 0x7eaa43; case Block.Dirt: return 0x916341;
                case Block.Stone: return 0x88938c; case Block.Sand: return 0xdfcc92;
                case Block.Log: return 0x91643b; case Block.Leaves: return 0x50813c;
                case Block.Planks: return 0xc39860; case Block.Cobble: return 0x78857f;
                case Block.Glass: return 0xabd7d6; case Block.Bricks: return 0xa96f57;
                case Block.Water: return 0x3d8fd1; case Block.CoalOre: return 0x505d57;
                case Block.IronOre: return 0xb19983; case Block.CrystalOre: return 0x71cbd1;
                case Block.Snow: return 0xe9eee8; case Block.Workbench: return 0xb88852;
                case Block.Lantern: case Block.Torch: return 0xffc975;
                case Block.Basalt: return 0x424e50; case Block.Clay: return 0xb99e86;
                case Block.Wool: return 0xf1e5c9; case Block.Campfire: return 0xd89b49;
                case Block.Bedrock: return 0x363c41; case Block.Furnace: return 0x8c918d;
                case Block.Obsidian: return 0x33263f; case Block.PortalX: case Block.PortalZ: case Block.EndPortal: return 0x8633ba;
                case Block.Netherrack: return 0x8e3938; case Block.Lava: return 0xf98428;
                case Block.NetherBricks: return 0x45272e; case Block.Blackstone: return 0x423c46;
                case Block.GoldBlock: return 0xedc94f; case Block.Chest: return 0xaf773c;
                case Block.SoulSand: return 0x655145; case Block.Glowstone: return 0xf7d585;
                case Block.Gravel: return 0xa29991; case Block.NetherGold: return 0xbc7046;
                case Block.NetherWart: return 0xbb333d; case Block.Spawner: return 0x393c48;
                case Block.EndStone: return 0xdedfa6; case Block.Farmland: return 0x694a2d;
                case Block.Crop: return 0xa2b847; case Block.Door: return 0x9f7548;
                case Block.EndFrame: return 0x568e79; default: return 0xffffff;
            }
        }
        public static string Name(Block id)
        {
            if (IsBed(id)) return "Red bed";
            switch (id)
            {
                case Block.Workbench: return "Crafting table"; case Block.Cobble: return "Cobblestone";
                case Block.CoalOre: return "Coal ore"; case Block.IronOre: return "Iron ore";
                case Block.CrystalOre: return "Diamond ore"; case Block.NetherBricks: return "Nether bricks";
                case Block.NetherGold: return "Nether gold ore"; case Block.NetherWart: return "Nether wart";
                case Block.EndStone: return "End stone"; case Block.EndPortal: return "End portal";
                case Block.EndFrame: return "End portal frame"; case Block.PortalX: case Block.PortalZ: return "Nether portal";
                case Block.SoulSand: return "Soul sand"; case Block.GoldBlock: return "Block of gold";
                case Block.Crop: return "Wheat"; default: return id.ToString();
            }
        }
        public static float Hardness(Block id)
        {
            if (IsBed(id)) return 0.3f;
            switch (id)
            {
                case Block.Air: case Block.Water: return 0;
                case Block.Bedrock: case Block.EndFrame: case Block.EndPortal: case Block.PortalX: case Block.PortalZ: case Block.Lava: return float.PositiveInfinity;
                case Block.Obsidian: return 10; case Block.Spawner: return 5; case Block.Furnace: return 3.5f;
                case Block.CrystalOre: case Block.GoldBlock: return 3; case Block.IronOre: return 2.4f;
                case Block.Chest: return 2.5f; case Block.CoalOre: case Block.Basalt: case Block.NetherBricks: case Block.EndStone: return 2;
                case Block.Stone: return 1.8f; case Block.Cobble: case Block.Blackstone: return 1.5f;
                case Block.Bricks: return 1.4f; case Block.Log: return 1.2f;
                case Block.Workbench: case Block.NetherGold: return 1;
                case Block.Planks: case Block.Door: return 0.8f;
                case Block.Glass: case Block.Glowstone: return 0.3f; case Block.Leaves: return 0.2f;
                case Block.Crop: case Block.NetherWart: case Block.Torch: return 0.1f;
                default: return 0.5f;
            }
        }
        public static int Drop(Block id)
        {
            if (IsBed(id)) return (int)Block.Bed;
            switch (id)
            {
                case Block.Grass: case Block.Farmland: return (int)Block.Dirt;
                case Block.Stone: return (int)Block.Cobble; case Block.CoalOre: return 91;
                case Block.IronOre: return 95; case Block.CrystalOre: return 94;
                case Block.Clay: return 96; case Block.NetherGold: return 138;
                case Block.Spawner: case Block.Glass: case Block.Bedrock: case Block.EndFrame: case Block.EndPortal: case Block.PortalX: case Block.PortalZ: case Block.Water: case Block.Lava: return 0;
                default: return (int)id;
            }
        }
    }
}
