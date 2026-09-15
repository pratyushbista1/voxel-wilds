using System;
using System.Collections.Generic;

namespace VoxelWilds.Core
{
    public readonly struct StructureMarker
    {
        public Cell Position { get; }
        public string Kind { get; }
        public string Mob { get; }
        public StructureMarker(Cell position, string kind, string mob = "")
        { Position = position; Kind = kind; Mob = mob; }
    }

    public static class Structures
    {
        internal static void Decorate(int seed, Dimension dimension, int cx, int cz, Voxel[] data, List<StructureMarker> markers)
        {
            var painter = new Painter(cx, cz, data, markers);
            if (dimension == Dimension.Overworld)
            {
                for (int rz = World.FloorDiv(cz * 16 - 80, 192); rz <= World.FloorDiv(cz * 16 + 64, 192); rz++)
                for (int rx = World.FloorDiv(cx * 16 - 80, 192); rx <= World.FloorDiv(cx * 16 + 64, 192); rx++)
                {
                    int x = rx * 192 + 40, z = rz * 192 + 40;
                    if (painter.Intersects(x - 31, z - 31, x + 31, z + 31))
                        Village(painter, seed, x, Terrain.Surface(seed, dimension, x, z), z);
                    int dx = rx * 192 + 16, dz = rz * 192 + 40;
                    if (painter.Intersects(dx - 5, dz - 5, dx + 5, dz + 5)) Dungeon(painter, dx, 25, dz);
                }
                if (painter.Intersects(17, 1, 31, 15)) EndEntrance(painter);
            }
            else if (dimension == Dimension.Nether)
            {
                for (int rz = World.FloorDiv(cz * 16 - 145, 192); rz <= World.FloorDiv(cz * 16 + 16, 192); rz++)
                for (int rx = World.FloorDiv(cx * 16 - 145, 192); rx <= World.FloorDiv(cx * 16 + 16, 192); rx++)
                {
                    int x = rx * 192 + 48, z = rz * 192 + 40;
                    if (painter.Intersects(x - 25, z - 40, x + 25, z + 27)) Fortress(painter, x, 36, z);
                    x = rx * 192 + 108; z = rz * 192 + 108;
                    if (painter.Intersects(x - 14, z - 24, x + 14, z + 14)) Bastion(painter, x, 34, z);
                }
            }
            else End(painter);
        }

        private static void Village(Painter p, int seed, int x, int y, int z)
        {
            p.Box(x - 20, y - 5, z - 20, x + 20, y - 1, z + 20, Block.Dirt);
            p.Box(x - 20, y, z - 20, x + 20, y, z + 20, Block.Grass);
            p.Box(x - 20, y + 1, z - 20, x + 20, World.Height - 1, z + 20, Block.Air);
            p.Box(x - 20, y, z - 1, x + 20, y, z + 1, Block.Gravel);
            p.Box(x - 1, y, z - 20, x + 1, y, z + 20, Block.Gravel);
            for (int distance = 21; distance <= 31; distance++)
            for (int side = -1; side <= 1; side += 2)
            {
                int offset = side * distance;
                int hy = BlendApproach(y, Terrain.Surface(seed, Dimension.Overworld, x + offset, z), distance);
                p.Box(x + offset, hy - 3, z - 1, x + offset, hy - 1, z + 1, Block.Dirt);
                p.Box(x + offset, hy, z - 1, x + offset, hy, z + 1, Block.Gravel);
                p.Box(x + offset, hy + 1, z - 1, x + offset, hy + 4, z + 1, Block.Air);
                hy = BlendApproach(y, Terrain.Surface(seed, Dimension.Overworld, x, z + offset), distance);
                p.Box(x - 1, hy - 3, z + offset, x + 1, hy - 1, z + offset, Block.Dirt);
                p.Box(x - 1, hy, z + offset, x + 1, hy, z + offset, Block.Gravel);
                p.Box(x - 1, hy + 1, z + offset, x + 1, hy + 4, z + offset, Block.Air);
            }
            p.Marker(x, y + 1, z, "village");
            House(p, x - 11, y, z - 10, false);
            House(p, x + 11, y, z - 10, true);
            House(p, x - 11, y, z + 10, true);
            House(p, x + 11, y, z + 10, false);
            p.Box(x - 4, y, z + 8, x + 4, y, z + 18, Block.Farmland);
            p.Box(x, y, z + 8, x, y, z + 18, Block.Water);
            for (int fx = -4; fx <= 4; fx++)
            {
                if (fx == 0) continue;
                p.Box(x + fx, y + 1, z + 8, x + fx, y + 1, z + 18, Block.Crop);
            }
            p.Box(x - 2, y, z - 13, x + 2, y, z - 9, Block.Cobble);
            p.Box(x - 1, y, z - 12, x + 1, y, z - 10, Block.Water);
            for (int ix = -2; ix <= 2; ix += 4)
            for (int iz = -13; iz <= -9; iz += 4) p.Box(x + ix, y + 1, z + iz, x + ix, y + 3, z + iz, Block.Log);
            p.Box(x - 2, y + 4, z - 13, x + 2, y + 4, z - 9, Block.Planks);
        }
        private static int BlendApproach(int start, int end, int distance)
        {
            int step = distance - 20;
            return start + Math.Max(-step, Math.Min(step, (int)Math.Round((end - start) * step / 11.0)));
        }
        private static void House(Painter p, int x, int y, int z, bool workshop)
        {
            p.Box(x - 3, y, z - 3, x + 3, y, z + 3, Block.Cobble);
            p.Box(x - 3, y + 1, z - 3, x + 3, y + 4, z + 3, Block.Planks);
            p.Box(x - 2, y + 1, z - 2, x + 2, y + 3, z + 2, Block.Air);
            p.Box(x - 4, y + 5, z - 4, x + 4, y + 5, z + 4, Block.Log);
            p.Box(x - 3, y + 6, z - 2, x + 3, y + 6, z + 2, Block.Planks);
            p.Box(x - 3, y + 7, z, x + 3, y + 7, z, Block.Planks);
            p.Box(x, y + 1, z + 2, x, y + 2, z + 3, Block.Air);
            p.Put(x - 3, y + 2, z, Block.Glass); p.Put(x + 3, y + 2, z, Block.Glass);
            p.Put(x, y + 2, z - 3, Block.Glass);
            p.Put(x - 2, y + 1, z - 2, Block.Bed); p.Put(x - 2, y + 1, z - 1, Block.BedHead);
            p.Put(x + 2, y + 1, z - 2, workshop ? Block.Workbench : Block.Chest);
            if (!workshop) p.Marker(x + 2, y + 1, z - 2, "chest", "village");
            if (workshop) p.Put(x + 2, y + 1, z - 1, Block.Furnace);
            p.Put(x + 1, y + 3, z + 2, Block.Torch);
            p.Marker(x, y + 1, z, "mob", "villager");
            p.Box(x - 1, y, z + 4, x + 1, y, z + 9, Block.Gravel);
        }
        private static void Dungeon(Painter p, int x, int y, int z)
        {
            p.Box(x - 5, y, z - 5, x + 5, y + 5, z + 5, Block.Cobble);
            p.Box(x - 4, y + 1, z - 4, x + 4, y + 4, z + 4, Block.Air);
            p.Box(x - 1, y + 1, z - 5, x + 1, y + 3, z - 5, Block.Air);
            p.Put(x, y + 1, z, Block.Spawner); p.Marker(x, y + 1, z, "spawner", "zombie");
            p.Put(x - 3, y + 1, z + 3, Block.Chest); p.Marker(x - 3, y + 1, z + 3, "chest", "dungeon");
        }
        private static void EndEntrance(Painter p)
        {
            p.Box(17, 27, 1, 31, 31, 15, Block.Stone);
            p.Box(17, 32, 1, 31, 32, 15, Block.Bricks);
            p.Box(17, 33, 1, 31, 44, 15, Block.Air);
            p.Box(22, 33, 6, 26, 33, 10, Block.EndFrame);
            p.Box(23, 33, 7, 25, 33, 9, Block.EndPortal);
            p.Put(19, 33, 3, Block.Glowstone); p.Put(29, 33, 13, Block.Glowstone);
            p.Marker(24, 33, 8, "endEntrance");
        }
        private static void Fortress(Painter p, int x, int y, int z)
        {
            p.Box(x - 25, y, z - 3, x + 25, y, z + 3, Block.NetherBricks);
            p.Box(x - 3, y, z - 25, x + 3, y, z + 27, Block.NetherBricks);
            p.Box(x - 25, y + 1, z - 2, x + 25, y + 5, z + 2, Block.Air);
            p.Box(x - 2, y + 1, z - 25, x + 2, y + 5, z + 27, Block.Air);
            foreach (int edge in new[] { -3, 3 })
            {
                p.Box(x - 25, y + 1, z + edge, x + 25, y + 1, z + edge, Block.NetherBricks);
                p.Box(x + edge, y + 1, z - 25, x + edge, y + 1, z + 27, Block.NetherBricks);
            }
            foreach (int px in new[] { -20, 0, 20 })
            foreach (int pz in new[] { -2, 2 }) p.Box(x + px, 5, z + pz, x + px + 1, y - 1, z + pz, Block.NetherBricks);
            p.Box(x - 7, y, z - 7, x + 7, y + 7, z + 7, Block.NetherBricks);
            p.Box(x - 6, y + 1, z - 6, x + 6, y + 6, z + 6, Block.Air);
            p.Box(x - 1, y + 1, z - 7, x + 1, y + 3, z + 7, Block.Air);
            p.Box(x - 7, y + 1, z - 1, x + 7, y + 3, z + 1, Block.Air);
            p.Put(x - 5, y + 1, z - 5, Block.Chest); p.Marker(x - 5, y + 1, z - 5, "chest", "fortress");
            p.Box(x + 4, y, z - 5, x + 5, y, z - 3, Block.SoulSand);
            p.Box(x + 4, y + 1, z - 5, x + 5, y + 1, z - 3, Block.NetherWart);
            p.Box(x - 4, y, z + 19, x + 4, y, z + 27, Block.NetherBricks);
            p.Box(x - 4, y + 1, z + 19, x + 4, y + 5, z + 27, Block.Air);
            p.Put(x, y + 1, z + 23, Block.Spawner); p.Marker(x, y + 1, z + 23, "spawner", "blaze");
            p.Marker(x, y + 1, z, "fortress");
            for (int i = 0; i < 15; i++)
            {
                p.Box(x - 2, y - i, z - 26 - i, x + 2, y - i, z - 26 - i, Block.NetherBricks);
                p.Box(x - 2, y - i + 1, z - 26 - i, x + 2, y - i + 4, z - 26 - i, Block.Air);
            }
        }
        private static void Bastion(Painter p, int x, int y, int z)
        {
            p.Box(x - 13, 8, z - 13, x + 13, y, z + 13, Block.Blackstone);
            p.Box(x - 13, y + 1, z - 13, x + 13, y + 12, z + 13, Block.Blackstone);
            p.Box(x - 11, y + 1, z - 11, x + 11, y + 14, z + 11, Block.Air);
            p.Box(x - 2, y + 1, z - 13, x + 2, y + 4, z - 11, Block.Air);
            p.Box(x - 10, y + 5, z - 10, x - 7, y + 5, z + 10, Block.Blackstone);
            p.Box(x + 7, y + 5, z - 10, x + 10, y + 5, z + 10, Block.Blackstone);
            p.Box(x - 10, y + 5, z + 7, x + 10, y + 5, z + 10, Block.Blackstone);
            p.Box(x - 3, y, z - 3, x + 3, y, z + 3, Block.Lava);
            p.Box(x - 1, y, z - 1, x + 1, y + 1, z + 1, Block.GoldBlock);
            p.Put(x, y + 2, z, Block.Chest); p.Marker(x, y + 2, z, "chest", "bastion");
            p.Put(x - 9, y + 6, z + 8, Block.Chest); p.Marker(x - 9, y + 6, z + 8, "chest", "bastion");
            p.Marker(x - 8, y + 1, z - 6, "mob", "piglin");
            p.Marker(x + 8, y + 1, z + 6, "mob", "brute");
            p.Marker(x, y + 1, z - 7, "bastion");
            for (int i = 0; i < 10; i++)
            {
                p.Box(x - 2, y - i, z - 14 - i, x + 2, y - i, z - 14 - i, Block.Blackstone);
                p.Box(x - 2, y - i + 1, z - 14 - i, x + 2, y - i + 4, z - 14 - i, Block.Air);
            }
            for (int i = 0; i < 5; i++) p.Box(x - 10, y + i + 1, z - 8 + i, x - 8, y + i + 1, z - 8 + i, Block.Blackstone);
        }
        private static void End(Painter p)
        {
            if (p.Intersects(-5, -5, 5, 5))
            {
                p.Box(-4, 44, -4, 4, 44, 4, Block.Bedrock);
                p.Box(-3, 45, -3, 3, 48, 3, Block.Air);
                p.Box(-2, 45, -2, 2, 45, 2, Block.Bedrock);
                p.Box(-1, 45, -1, 1, 45, 1, Block.EndPortal);
                p.Marker(0, 45, 0, "endExit");
                p.Marker(0, 65, 0, "dragon", "ender_dragon");
            }
            if (p.Intersects(5, 5, 11, 11))
            {
                p.Box(5, 44, 5, 11, 44, 11, Block.Obsidian);
                p.Box(5, 45, 5, 11, 49, 11, Block.Air);
            }
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                int x = (int)Math.Round(Math.Cos(angle) * 48), z = (int)Math.Round(Math.Sin(angle) * 48);
                int top = 57 + i * 2;
                if (!p.Intersects(x - 3, z - 3, x + 3, z + 3)) continue;
                for (int dx = -3; dx <= 3; dx++)
                for (int dz = -3; dz <= 3; dz++)
                    if (dx * dx + dz * dz <= 9) p.Box(x + dx, 36, z + dz, x + dx, top, z + dz, Block.Obsidian);
                p.Put(x, top + 1, z, Block.Bedrock);
                p.Marker(x, top + 2, z, "crystal");
            }
        }

        private sealed class Painter
        {
            private readonly int minX, minZ;
            private readonly Voxel[] data;
            private readonly List<StructureMarker> markers;
            public Painter(int cx, int cz, Voxel[] data, List<StructureMarker> markers)
            { minX = cx * 16; minZ = cz * 16; this.data = data; this.markers = markers; }
            public bool Intersects(int x1, int z1, int x2, int z2) => x2 >= minX && x1 < minX + 16 && z2 >= minZ && z1 < minZ + 16;
            public void Put(int x, int y, int z, Block id) => Terrain.PutClipped(data, minX, minZ, x, y, z, id);
            public void Box(int x1, int y1, int z1, int x2, int y2, int z2, Block id)
            {
                for (int z = Math.Max(z1, minZ); z <= Math.Min(z2, minZ + 15); z++)
                for (int x = Math.Max(x1, minX); x <= Math.Min(x2, minX + 15); x++)
                for (int y = Math.Max(0, y1); y <= Math.Min(World.Height - 1, y2); y++) data[World.Index(x - minX, y, z - minZ)] = new Voxel(id);
            }
            public void Marker(int x, int y, int z, string kind, string mob = "")
            {
                if (x >= minX && x < minX + 16 && z >= minZ && z < minZ + 16) markers.Add(new StructureMarker(new Cell(x, y, z), kind, mob));
            }
        }
    }
}
