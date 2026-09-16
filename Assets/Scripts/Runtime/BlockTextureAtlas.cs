using UnityEngine;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public static class BlockTextureAtlas
    {
        public const int TileSize = 16;
        public const int Padding = 8;
        public const int CellSize = TileSize + Padding * 2;
        public const int Columns = 8;
        public const int Rows = 8;
        public const int MipCount = 4;
        private const int GrassSide = 54, LogEnd = 55, WorkbenchTop = 56, FurnaceFront = 57;
        private const int FurnaceTop = 58, ChestTop = 59, ChestFront = 60, BedHeadTop = 61;
        private const int BedSide = 62, EndFrameTop = 63;
        private const int GrassTuft = 26, Wildflower = 27;

        public static Texture2D Create()
        {
            int width = Columns * CellSize, height = Rows * CellSize;
            var pixels = new Color32[width * height];
            for (int index = 0; index < Columns * Rows; index++)
            {
                var tile = Paint(index);
                int left = index % Columns * CellSize, bottom = index / Columns * CellSize;
                for (int y = 0; y < CellSize; y++)
                    for (int x = 0; x < CellSize; x++)
                    {
                        int tx = Mathf.Clamp(x - Padding, 0, TileSize - 1);
                        int ty = TileSize - 1 - Mathf.Clamp(y - Padding, 0, TileSize - 1);
                        pixels[left + x + (bottom + y) * width] = tile[tx + ty * TileSize];
                    }
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, MipCount, false)
            {
                name = "Wilds block atlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        public static Vector2 Uv(Block id, int face, float u, float v)
        {
            return TileUv(TileFor(id, face), u, v);
        }

        public static Vector2 GrassUv(float u, float v) => TileUv(GrassTuft, u, v);
        public static Vector2 FlowerUv(float u, float v) => TileUv(Wildflower, u, v);

        private static Vector2 TileUv(int tile, float u, float v)
        {
            return new Vector2((tile % Columns * CellSize + Padding + Mathf.Clamp01(u) * TileSize) / (Columns * (float)CellSize),
                (tile / Columns * CellSize + Padding + Mathf.Clamp01(v) * TileSize) / (Rows * (float)CellSize));
        }

        public static int TileFor(Block id, int face)
        {
            if (Blocks.IsBed(id))
            {
                if (face == 3) return (int)Block.Planks;
                if (face != 2) return BedSide;
                return ((int)id & 1) == 0 ? BedHeadTop : (int)Block.Bed;
            }
            switch (id)
            {
                case Block.Grass: return face == 2 ? (int)id : face == 3 ? (int)Block.Dirt : GrassSide;
                case Block.Log: return face == 2 || face == 3 ? LogEnd : (int)id;
                case Block.Workbench: return face == 2 ? WorkbenchTop : face == 3 ? (int)Block.Planks : (int)id;
                case Block.Furnace: return face == 5 ? FurnaceFront : face == 2 || face == 3 ? FurnaceTop : (int)id;
                case Block.Chest: return face == 5 ? ChestFront : face == 2 ? ChestTop : (int)id;
                case Block.Farmland: return face == 2 ? (int)id : (int)Block.Dirt;
                case Block.EndFrame: return face == 2 ? EndFrameTop : face == 3 ? (int)Block.EndStone : (int)id;
                default: return Mathf.Clamp((int)id, 0, 53);
            }
        }

        private static Color32[] Paint(int index)
        {
            var p = new Tile(index);
            Block id = (Block)Mathf.Min(index, 53);
            p.Fill(Blocks.ColorRgb(id));
            switch (index)
            {
                case (int)Block.Air: p.Fill(0, 0); break;
                case (int)Block.Grass: Grass(p); break;
                case (int)Block.Dirt: Dirt(p); break;
                case (int)Block.Stone: Stone(p); break;
                case (int)Block.Sand:
                    p.Noise(0xddce99, 10, 2); p.Flecks(24, 1, 1, 0xc5b77f, 0xeee0b1);
                    p.Rect(2, 4, 4, 1, 0xe9dca7); p.Rect(10, 11, 4, 1, 0xd0bf88); break;
                case (int)Block.Log: Bark(p); break;
                case (int)Block.Leaves: Leaves(p); break;
                case (int)Block.Planks: Planks(p); break;
                case (int)Block.Cobble: Cobble(p, 0x87918a, 0x59645f, 0xa0aaa0); break;
                case (int)Block.Glass: Glass(p); break;
                case (int)Block.Bricks: Bricks(p, 0xac7058, 0xd0b29a, 0xc78b70); break;
                case (int)Block.Water: Water(p); break;
                case (int)Block.CoalOre: Ore(p, 0x333b37, 0x59625a, 0x222c2b); break;
                case (int)Block.IronOre: Ore(p, 0xc5a287, 0xedd0aa, 0x9b7964); break;
                case (int)Block.CrystalOre: Ore(p, 0x60bdbb, 0xb5f1db, 0x397b8f); break;
                case (int)Block.Snow:
                    p.Noise(0xe7eee8, 5, 2); p.Flecks(17, 2, 1, 0xd4e2e1, 0xf3f5e9); break;
                case (int)Block.Workbench: Workbench(p, false); break;
                case (int)Block.Lantern: Lantern(p); break;
                case (int)Block.Basalt:
                    p.Noise(0x4e5b5b, 10, 2);
                    for (int x = 0; x < 16; x += 4) { p.Rect(x, 0, 1, 16, 0x364241); p.Rect(x + 1, 2 + x % 3, 1, 10, 0x65716b); }
                    p.Flecks(10, 2, 1, 0x46514d, 0x798279); break;
                case (int)Block.Clay:
                    p.Noise(0xafa397, 9, 3); p.Flecks(14, 3, 1, 0xc1b5a8, 0x9e938b); break;
                case (int)Block.Wool:
                    p.Noise(0xeee4cf, 5, 2);
                    for (int y = 0; y < 16; y += 3) for (int x = y % 2; x < 16; x += 3) { p.Pixel(x, y, 0xd6cbb5); p.Pixel(x + 1, y + 1, 0xf6eedc); }
                    break;
                case (int)Block.Campfire: Fire(p); break;
                case (int)Block.Bedrock:
                    p.Noise(0x454d4d, 25, 2); p.Flecks(28, 2, 2, 0x262e31, 0x747c75);
                    p.Flecks(12, 3, 1, 0x1e262a, 0x939b91); break;
                case (int)Block.Furnace: Cobble(p, 0x8b9490, 0x606963, 0xa3aaa1); p.Frame(0, 0, 16, 16, 0x68736e); break;
                case (int)Block.Torch: Torch(p); break;
                case (int)Block.Bed:
                case (int)Block.BedEastHead: case (int)Block.BedSouth: case (int)Block.BedSouthHead:
                case (int)Block.BedWest: case (int)Block.BedWestHead: Bed(p, false); break;
                case GrassTuft: Tuft(p, false); break;
                case Wildflower: Tuft(p, true); break;
                case (int)Block.Obsidian:
                    Cobble(p, 0x33263f, 0x1d1d2d, 0x594368); p.Flecks(12, 2, 1, 0x443351, 0x755286); break;
                case (int)Block.PortalX: case (int)Block.PortalZ: Portal(p, false); break;
                case (int)Block.Netherrack: Netherrack(p); break;
                case (int)Block.Lava: Lava(p); break;
                case (int)Block.NetherBricks: Bricks(p, 0x57333b, 0x2f242e, 0x76464b); break;
                case (int)Block.Blackstone: Cobble(p, 0x48434c, 0x292a33, 0x68606c); break;
                case (int)Block.GoldBlock:
                    p.Noise(0xeac655, 7, 3); p.Frame(0, 0, 16, 16, 0x9d7828);
                    p.Rect(1, 1, 14, 1, 0xffeda0); p.Rect(1, 1, 1, 14, 0xffe48a);
                    p.Rect(2, 14, 13, 1, 0xbe9535); p.Rect(14, 2, 1, 12, 0xc29a3a);
                    p.Rect(3, 3, 5, 1, 0xf8db78); p.Rect(3, 4, 1, 3, 0xf8db78); break;
                case (int)Block.Chest: Chest(p, 0); break;
                case (int)Block.SoulSand:
                    p.Noise(0x725b49, 14, 2); p.Flecks(18, 2, 2, 0x594334, 0x8c6f54);
                    for (int n = 0; n < 4; n++) { int x = 1 + n % 2 * 8, y = 1 + n / 2 * 8; p.Rect(x, y, 1, 2, 0x42382f); p.Rect(x + 3, y, 1, 2, 0x42382f); p.Rect(x + 1, y + 3, 2, 2, 0x584135); }
                    break;
                case (int)Block.Glowstone: Glowstone(p); break;
                case (int)Block.Gravel:
                    p.Noise(0xa49b8f, 15, 2); p.Flecks(29, 2, 2, 0x7b7973, 0xc4b9a7);
                    p.Flecks(18, 1, 1, 0x666c68, 0xd8c7b1); break;
                case (int)Block.NetherGold:
                    Netherrack(p); OreVeins(p, 0xe9ba4d, 0xffdf83, 0xa7742d); break;
                case (int)Block.NetherWart: Wart(p); break;
                case (int)Block.Spawner: Spawner(p); break;
                case (int)Block.EndStone:
                    p.Noise(0xdcdfaa, 10, 2); p.Flecks(18, 2, 2, 0xb8bb83, 0xebe9be);
                    p.Flecks(10, 1, 1, 0x9eaa79, 0xf3efca); break;
                case (int)Block.EndPortal: Portal(p, true); break;
                case (int)Block.Farmland:
                    Dirt(p);
                    for (int x = 1; x < 16; x += 4) { p.Rect(x, 0, 2, 16, 0x4f3b2b); p.Rect(x + 2, 0, 1, 16, 0xa17848); }
                    p.Flecks(18, 1, 1, 0x785433, 0x88613c); break;
                case (int)Block.Crop: Wheat(p); break;
                case (int)Block.Door: Door(p); break;
                case (int)Block.EndFrame:
                    p.Noise(0xced5a1, 8, 2); p.Rect(0, 0, 16, 4, 0x527e68); p.Rect(0, 4, 16, 1, 0x344f49);
                    for (int x = 1; x < 16; x += 5) { p.Rect(x, 7, 3, 5, 0x7f9d7c); p.Rect(x + 1, 8, 1, 3, 0x456d5d); }
                    p.Rect(0, 14, 16, 2, 0xa4b689); break;
                case GrassSide:
                    Dirt(p);
                    for (int x = 0; x < 16; x++)
                    {
                        int depth = 3 + (int)(Hash(x / 2, 17, 83) % 3);
                        p.Rect(x, 0, 1, depth, Shade(0x709644, (int)(Hash(x, 6, 71) % 15) - 7));
                        p.Pixel(x, depth, 0x547638); p.Pixel(x, 0, Shade(0x89ac52, (int)(Hash(x, 0, 13) % 10) - 5));
                    }
                    break;
                case LogEnd: LogRings(p); break;
                case WorkbenchTop: Workbench(p, true); break;
                case FurnaceFront:
                    Cobble(p, 0x919a93, 0x68736b, 0xacb4a8);
                    p.Frame(0, 0, 16, 16, 0x606d65); p.Rect(3, 3, 10, 4, 0x35403c); p.Rect(4, 4, 8, 2, 0x172725);
                    p.Rect(2, 10, 12, 4, 0x37423b); p.Rect(3, 11, 10, 2, 0x1d2826);
                    p.Rect(3, 8, 10, 1, 0xc3c6b6); p.Rect(4, 12, 2, 1, 0x566056); break;
                case FurnaceTop:
                    p.Noise(0x8f9991, 8, 2); p.Frame(0, 0, 16, 16, 0x5f6b62);
                    p.Frame(2, 2, 12, 12, 0xa8b0a3); p.Rect(4, 4, 8, 1, 0x778379); break;
                case ChestTop: Chest(p, 1); break;
                case ChestFront: Chest(p, 2); break;
                case BedHeadTop: Bed(p, true); break;
                case BedSide:
                    p.Noise(0xa93743, 6, 2); p.Rect(0, 0, 16, 1, 0xda6068); p.Rect(0, 5, 16, 1, 0x782d36);
                    p.Rect(0, 6, 16, 10, 0x94633a); p.Rect(0, 7, 16, 2, 0xba8750); p.Rect(0, 11, 16, 5, 0x654c33);
                    p.Rect(2, 12, 12, 4, 0, 0); break;
                case EndFrameTop:
                    p.Noise(0x537c65, 8, 2); p.Frame(0, 0, 16, 16, 0xb9c997); p.Frame(2, 2, 12, 12, 0x89ab80);
                    p.Rect(4, 4, 8, 8, 0x223e37); p.Rect(5, 5, 6, 6, 0x142d2d); p.Rect(4, 4, 8, 1, 0x365c4b);
                    p.Rect(1, 1, 3, 1, 0xe0dda8); p.Rect(12, 14, 3, 1, 0x365e50); break;
            }
            return p.Pixels;
        }

        private static void Dirt(Tile p)
        {
            p.Noise(0x936744, 10, 2);
            p.Flecks(20, 2, 1, 0xaf8054, 0x6f5038);
            p.Flecks(9, 1, 1, 0xc19869, 0x647064);
        }

        private static void Grass(Tile p)
        {
            p.Noise(0x81a34c, 11, 2);
            p.Flecks(16, 2, 1, 0x71933e, 0x92b35b);
            for (int n = 0; n < 13; n++)
            {
                int x = (int)(Hash(n, 4, 67) % 16), y = (int)(Hash(n, 7, 97) % 16);
                p.Rect(x, y, 1, 2, 0xa0bb65); p.Pixel(x + 1, y + 1, 0x6d8d3f);
            }
        }

        private static void Stone(Tile p)
        {
            p.Noise(0x90998f, 8, 3); p.Flecks(14, 2, 1, 0xa7ada0, 0x7e897f);
            p.Rect(1, 4, 5, 1, 0x788379); p.Rect(2, 5, 5, 1, 0x9da69a);
            p.Rect(10, 10, 5, 1, 0x748176); p.Rect(11, 11, 4, 1, 0xa4ab9e);
            p.Rect(6, 14, 3, 1, 0x7d887c);
        }

        private static void Cobble(Tile p, uint face, uint joint, uint highlight)
        {
            p.Noise(face, 9, 2);
            for (int row = 0; row < 4; row++)
            {
                int y = row * 4;
                for (int x = -4 + (row % 2) * 4; x < 16; x += 8)
                {
                    int shift = (row + x + 16) % 3;
                    p.Rect(x, y, 7, 1, joint); p.Rect(x, y, 1, 3, joint);
                    p.Pixel(x + 1, y + 3, joint); p.Rect(x + 2, y + 3, 5, 1, joint);
                    p.Rect(x + 1, y + 1, 5 - shift, 1, highlight);
                    p.Pixel(x + 6, y + 2, Shade(face, -20));
                }
            }
        }

        private static void Bark(Tile p)
        {
            p.Noise(0x85623d, 10, 2);
            for (int x = 1; x < 16; x += 4)
            {
                int bend = x % 3;
                p.Rect(x, 0, 1, 6 + bend, 0x523f2b); p.Rect(x + 1, 6 + bend, 1, 10, 0x523f2b);
                p.Rect(x + 1, 0, 1, 5 + bend, 0xb58a50); p.Rect(x + 2, 7 + bend, 1, 6, 0xa57a46);
            }
            p.Rect(6, 5, 3, 5, 0x634a30); p.Rect(7, 6, 1, 3, 0xa17a47); p.Pixel(7, 7, 0x493c29);
        }

        private static void LogRings(Tile p)
        {
            p.Noise(0xbd965b, 7, 2); p.Frame(0, 0, 16, 16, 0x624c30);
            p.Frame(1, 1, 14, 14, 0x8c673e);
            for (int edge = 3; edge < 8; edge += 2)
            {
                p.Frame(edge, edge - 1, 16 - edge * 2, 17 - edge * 2, 0x8d693c);
                p.Rect(edge + 1, edge, 13 - edge * 2, 1, 0xd3b47a);
            }
            p.Rect(8, 1, 1, 4, 0x694f31); p.Rect(11, 12, 4, 1, 0x8b673b);
        }

        private static void Planks(Tile p)
        {
            p.Noise(0xc09a63, 6, 2);
            for (int y = 3; y < 16; y += 4)
            {
                p.Rect(0, y, 16, 1, 0x8c673b); p.Rect(0, (y + 1) % 16, 16, 1, 0xd6b07a);
                int seam = (y * 3) % 13;
                p.Rect(seam, y - 3, 1, 3, 0x997242); p.Pixel(seam + 1, y - 1, 0x6c5335);
            }
            p.Rect(2, 1, 4, 1, 0xb58a51); p.Rect(8, 6, 5, 1, 0xb28b52); p.Rect(4, 10, 6, 1, 0xcfa46a);
        }

        private static void Leaves(Tile p)
        {
            p.Noise(0x4f7638, 8, 3);
            for (int y = -1; y < 16; y += 4)
                for (int x = -1 + ((y + 1) / 4 % 2) * 2; x < 16; x += 4)
                {
                    p.Rect(x + 1, y, 2, 1, 0x74934b); p.Rect(x, y + 1, 4, 2, 0x618a42);
                    p.Rect(x + 1, y + 1, 2, 1, 0x8ca75d); p.Rect(x + 1, y + 3, 2, 1, 0x36592f);
                    p.Pixel(x + 3, y + 2, 0x456d34);
                }
        }

        private static void Bricks(Tile p, uint face, uint mortar, uint highlight)
        {
            p.Noise(face, 10, 2);
            for (int y = 3; y < 16; y += 4)
            {
                p.Rect(0, y, 16, 1, mortar);
                for (int x = y % 8 == 3 ? 0 : 4; x < 16; x += 8)
                {
                    p.Rect(x, y - 3, 1, 3, mortar); p.Rect(x + 1, y - 3, 6, 1, highlight);
                    p.Rect(x + 2, y - 1, 4, 1, Shade(face, -13));
                }
            }
        }

        private static void Glass(Tile p)
        {
            p.Fill(0xa3d1cc, 36); p.Frame(0, 0, 16, 16, 0x92bdb6, 230);
            p.Rect(0, 0, 16, 1, 0xd8eee3); p.Rect(0, 0, 1, 16, 0xd8eee3);
            for (int n = 0; n < 6; n++) { p.Pixel(3 + n, 8 - n, 0xe8f4e8, 185); p.Pixel(8 + n, 13 - n, 0xd3e8de, 150); }
        }

        private static void Ore(Tile p, uint ore, uint shine, uint dark)
        {
            Stone(p); OreVeins(p, ore, shine, dark);
        }

        private static void OreVeins(Tile p, uint ore, uint shine, uint dark)
        {
            int[] xs = { 2, 10, 6, 12, 2, 8 }, ys = { 2, 1, 6, 9, 11, 13 };
            for (int n = 0; n < xs.Length; n++)
            {
                int x = xs[n], y = ys[n]; p.Rect(x, y, 3, 2, dark); p.Rect(x + 1, y + 1, 2, 2, ore);
                p.Rect(x, y, 2, 1, shine); p.Pixel(x + 2, y + 2, dark);
            }
        }

        private static void Water(Tile p)
        {
            p.Noise(0x4b929e, 4, 4);
            for (int row = 0; row < 4; row++)
            {
                int y = 2 + row * 4, x = row * 5 % 12;
                p.Rect(x, y, 4, 1, 0x6baeb3); p.Rect((x + 4) % 16, y + 1, 3, 1, 0x5c9faa);
                p.Rect((x + 7) % 16, y + 2, 4, 1, 0x3e8496);
            }
        }

        private static void Lava(Tile p)
        {
            p.Noise(0xe77b27, 16, 3);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                float wave = Mathf.Sin(x * .7f + Mathf.Sin(y * .8f)) + Mathf.Cos(y * .7f - x * .25f);
                if (wave > 1.15f) p.Pixel(x, y, 0xffdd75);
                else if (wave > .5f) p.Pixel(x, y, 0xffb744);
                else if (wave < -1.1f) p.Pixel(x, y, 0xb7471f);
                else if (wave < -.5f) p.Pixel(x, y, 0xd25720);
            }
        }

        private static void Portal(Tile p, bool end)
        {
            if (end)
            {
                p.Noise(0x142c32, 7, 2); p.Flecks(18, 1, 1, 0x367775, 0x7aafa4);
                p.Pixel(3, 4, 0xd5e0c3); p.Pixel(12, 9, 0xc6e2cc); return;
            }
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                float wave = Mathf.Sin(x * .6f + Mathf.Sin(y * .5f) * 2) + Mathf.Cos(y * .8f + x * .3f);
                uint color = wave > 1 ? 0xbe77dcU : wave > 0 ? 0x9855b9U : wave > -1 ? 0x6c3c8cU : 0x4d326aU;
                p.Pixel(x, y, color);
            }
            p.Flecks(10, 1, 1, 0xe0b3ed, 0xae7ccd);
        }

        private static void Netherrack(Tile p)
        {
            p.Noise(0x91463f, 12, 2); p.Flecks(22, 3, 2, 0x703a38, 0xae594a);
            p.Flecks(11, 1, 2, 0x593330, 0xc57658);
        }

        private static void Glowstone(Tile p)
        {
            p.Noise(0xab7946, 9, 2);
            for (int y = -1; y < 16; y += 4) for (int x = (y % 8 == 3 ? -2 : 1); x < 16; x += 5)
            {
                p.Rect(x, y, 4, 3, 0xe2b46b); p.Rect(x + 1, y, 2, 2, 0xffeab0);
                p.Rect(x, y + 2, 2, 1, 0xf9d489); p.Pixel(x + 3, y + 3, 0x8c6541);
            }
        }

        private static void Workbench(Tile p, bool top)
        {
            Planks(p); p.Frame(0, 0, 16, 16, 0x69482e);
            if (top)
            {
                p.Rect(2, 2, 12, 12, 0x8c6038);
                for (int n = 2; n <= 14; n += 4) { p.Rect(n, 2, 1, 12, 0xd3a56a); p.Rect(2, n, 12, 1, 0xd3a56a); }
                p.Rect(3, 3, 3, 1, 0xaa7847); return;
            }
            p.Rect(0, 0, 16, 3, 0xac7a46); p.Rect(1, 3, 2, 13, 0x775230); p.Rect(13, 3, 2, 13, 0x775230);
            p.Rect(4, 5, 3, 6, 0x69472d); p.Rect(9, 5, 3, 6, 0x69472d);
            p.Rect(5, 6, 1, 6, 0xdcba78); p.Rect(9, 6, 3, 2, 0x9eaaa0); p.Rect(10, 8, 1, 5, 0xcfaa6a);
        }

        private static void Chest(Tile p, int face)
        {
            p.Noise(0xb38448, 7, 2); p.Frame(0, 0, 16, 16, 0x57432d); p.Frame(1, 1, 14, 14, 0x805a34);
            p.Rect(2, 2, 12, 1, 0xd4a862); p.Rect(2, 13, 12, 1, 0x916534);
            if (face == 1) { p.Rect(3, 3, 1, 10, 0x9b703c); p.Rect(12, 3, 1, 10, 0x9b703c); return; }
            p.Rect(1, 6, 14, 1, 0x57432d); p.Rect(1, 7, 14, 1, 0xd09d58);
            if (face == 2)
            {
                p.Rect(6, 5, 4, 5, 0x5f5038); p.Rect(7, 5, 2, 4, 0xe1d5a1);
                p.Pixel(8, 8, 0x807960); p.Rect(7, 5, 2, 1, 0xf3e8ba);
            }
        }

        private static void Bed(Tile p, bool head)
        {
            p.Noise(0xb6424c, 6, 2); p.Rect(0, 0, 16, 1, 0xd9676c); p.Rect(0, 0, 1, 16, 0xd15a61);
            p.Rect(15, 0, 1, 16, 0x943540); p.Rect(0, 15, 16, 1, 0x96323d);
            p.Rect(2, 3, 1, 10, 0xc44e57); p.Rect(13, 2, 1, 11, 0xa73743);
            if (head)
            {
                p.Rect(1, 1, 14, 7, 0xdddcc7); p.Rect(2, 1, 12, 6, 0xf1edda);
                p.Rect(3, 2, 10, 1, 0xfff7df); p.Rect(2, 7, 12, 1, 0xbdbdaa);
            }
        }

        private static void Door(Tile p)
        {
            p.Noise(0xaa7e48, 7, 2); p.Frame(0, 0, 16, 16, 0x6e512f); p.Frame(1, 1, 14, 14, 0xd0a36a);
            p.Rect(3, 3, 4, 5, 0x5d4d35); p.Rect(9, 3, 4, 5, 0x5d4d35);
            p.Rect(4, 4, 2, 3, 0x97a48b, 90); p.Rect(10, 4, 2, 3, 0x97a48b, 90);
            p.Rect(3, 10, 10, 4, 0x8f663b); p.Rect(4, 11, 8, 2, 0xbd9052);
            p.Rect(12, 8, 2, 2, 0x514635); p.Pixel(12, 8, 0xd8c789);
        }

        private static void Lantern(Tile p)
        {
            p.Noise(0xe4a858, 8, 2); p.Frame(0, 0, 16, 16, 0x655648);
            p.Frame(1, 1, 14, 14, 0x9d8560); p.Rect(3, 3, 10, 10, 0xf8cb77); p.Rect(5, 4, 6, 8, 0xffeab0);
            p.Rect(7, 0, 2, 16, 0x8f7656); p.Rect(0, 7, 16, 2, 0x8f7656);
        }

        private static void Torch(Tile p)
        {
            p.Fill(0, 0); p.Rect(6, 5, 4, 11, 0x8d653b); p.Rect(6, 6, 1, 10, 0xc89856); p.Rect(9, 7, 1, 9, 0x674a2e);
            p.Rect(5, 2, 6, 5, 0xe69738); p.Rect(6, 1, 4, 5, 0xffcc65); p.Rect(7, 1, 2, 4, 0xffefd0);
        }

        private static void Fire(Tile p)
        {
            p.Fill(0, 0); p.Rect(1, 12, 14, 3, 0x765333); p.Rect(3, 14, 11, 2, 0xa27440);
            p.Rect(4, 5, 8, 8, 0xdc772b); p.Rect(6, 2, 5, 10, 0xf7b344); p.Rect(5, 8, 7, 5, 0xffcf64);
            p.Rect(7, 6, 3, 7, 0xffe6a1); p.Rect(3, 7, 2, 4, 0xef9230); p.Rect(11, 5, 2, 6, 0xef9230);
        }

        private static void Wheat(Tile p)
        {
            p.Fill(0, 0);
            for (int n = 0; n < 4; n++)
            {
                int x = 2 + n * 4, y = 1 + n % 3;
                p.Rect(x, y + 4, 1, 16 - y - 4, 0x799040); p.Rect(x - 1, y, 2, 7, 0xc9b65b);
                p.Rect(x, y + 1, 2, 5, 0xe3cb78); p.Pixel(x, y, 0xf0d68b);
                p.Rect(x - 2, 10 + n % 2, 2, 1, 0xa2ae52); p.Pixel(x - 2, 9 + n % 2, 0xa2ae52);
                p.Rect(x + 1, 12 - n % 2, 2, 1, 0x89a043);
            }
        }

        private static void Tuft(Tile p, bool flower)
        {
            p.Fill(0, 0);
            for (int blade = 0; blade < 5; blade++)
            {
                int x = 2 + blade * 3, top = 4 + (int)(Hash(blade, 47, 19) % 6);
                int lean = blade < 2 ? -1 : 1;
                p.Rect(x, top + 3, 1, 13 - top, 0x688b3c);
                p.Rect(x + lean, top, 1, 5, 0x8caa50);
                p.Pixel(x + lean * 2, top - 1, 0xa3b863);
                p.Rect(x - lean, 12, 1, 4, 0x527437);
            }
            if (!flower) return;
            p.Rect(7, 4, 1, 12, 0x557d3b);
            p.Rect(5, 1, 4, 2, 0xe3b076); p.Rect(4, 2, 6, 2, 0xffe5af);
            p.Rect(5, 4, 4, 1, 0xf2cea0); p.Rect(6, 2, 2, 2, 0xe1a546);
            p.Pixel(6, 2, 0xf7cf66); p.Rect(8, 8, 3, 1, 0x82a04d); p.Pixel(10, 7, 0x82a04d);
        }

        private static void Wart(Tile p)
        {
            p.Fill(0, 0);
            for (int n = 0; n < 3; n++)
            {
                int x = 1 + n * 5, y = 5 + n % 2 * 3;
                p.Rect(x + 1, y + 3, 2, 12 - y, 0x743039); p.Rect(x, y, 4, 4, 0xb23b49);
                p.Rect(x + 1, y - 1, 2, 2, 0xda6360); p.Pixel(x + 3, y + 2, 0x7e2c3d);
            }
        }

        private static void Spawner(Tile p)
        {
            p.Fill(0x283037, 0); p.Frame(0, 0, 16, 16, 0x434f53);
            for (int x = 3; x < 16; x += 4) { p.Rect(x, 0, 2, 16, 0x343e47); p.Rect(x, 0, 1, 16, 0x687075); }
            for (int y = 3; y < 16; y += 4) { p.Rect(0, y, 16, 2, 0x343e47); p.Rect(0, y, 16, 1, 0x687075); }
            p.Pixel(1, 1, 0x889293); p.Pixel(14, 14, 0x202a31);
        }

        private static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                value = (value ^ (value >> 13)) * 1274126177;
                return value ^ (value >> 16);
            }
        }

        private static uint Shade(uint color, int amount)
        {
            return (uint)(Mathf.Clamp((int)(color >> 16 & 255) + amount, 0, 255) << 16 |
                Mathf.Clamp((int)(color >> 8 & 255) + amount, 0, 255) << 8 |
                Mathf.Clamp((int)(color & 255) + amount, 0, 255));
        }

        private sealed class Tile
        {
            public readonly Color32[] Pixels = new Color32[TileSize * TileSize];
            private readonly int seed;
            public Tile(int index) { seed = index * 73 + 81743; }
            public void Fill(uint color, byte alpha = 255) => Rect(0, 0, TileSize, TileSize, color, alpha);
            public void Pixel(int x, int y, uint color, byte alpha = 255)
            {
                if (x >= 0 && x < TileSize && y >= 0 && y < TileSize)
                    Pixels[x + y * TileSize] = new Color32((byte)(color >> 16), (byte)(color >> 8), (byte)color, alpha);
            }
            public void Rect(int x, int y, int width, int height, uint color, byte alpha = 255)
            {
                for (int dy = 0; dy < height; dy++) for (int dx = 0; dx < width; dx++) Pixel(x + dx, y + dy, color, alpha);
            }
            public void Frame(int x, int y, int width, int height, uint color, byte alpha = 255)
            {
                Rect(x, y, width, 1, color, alpha); Rect(x, y + height - 1, width, 1, color, alpha);
                Rect(x, y, 1, height, color, alpha); Rect(x + width - 1, y, 1, height, color, alpha);
            }
            public void Noise(uint color, int strength, int patch)
            {
                for (int y = 0; y < TileSize; y++) for (int x = 0; x < TileSize; x++)
                {
                    int broad = (int)(Hash(x / patch, y / patch, seed) % (uint)(strength * 2 + 1)) - strength;
                    int grain = (int)(Hash(x, y, seed + 113) % 5) - 2;
                    Pixel(x, y, Shade(color, broad + grain));
                }
            }
            public void Flecks(int count, int width, int height, uint a, uint b)
            {
                for (int n = 0; n < count; n++)
                {
                    int x = (int)(Hash(n, 83, seed) % 16), y = (int)(Hash(n, 149, seed) % 16);
                    Rect(x, y, width, height, n % 2 == 0 ? a : b);
                }
            }
        }
    }
}
