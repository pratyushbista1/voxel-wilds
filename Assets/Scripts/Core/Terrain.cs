using System;

namespace VoxelWilds.Core
{
    public static class Terrain
    {
        public const int SeaLevel = 26;
        public static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 ^ y * 1442695041 ^ z * 668265263 ^ seed);
                h = (h ^ (h >> 13)) * 1274126177U;
                return h ^ (h >> 16);
            }
        }
        public static double Noise(double x, double z, int seed)
        {
            int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z);
            double fx = Smooth(x - ix), fz = Smooth(z - iz);
            double a = Hash(ix, 0, iz, seed) / (double)uint.MaxValue;
            double b = Hash(ix + 1, 0, iz, seed) / (double)uint.MaxValue;
            double c = Hash(ix, 0, iz + 1, seed) / (double)uint.MaxValue;
            double d = Hash(ix + 1, 0, iz + 1, seed) / (double)uint.MaxValue;
            return (a + (b - a) * fx) * (1 - fz) + (c + (d - c) * fx) * fz;
        }
        private static double Smooth(double v) => v * v * (3 - 2 * v);
        public static int Surface(int seed, Dimension dimension, int x, int z)
        {
            if (dimension == Dimension.End)
            {
                double r = Math.Sqrt((double)x * x + (double)z * z);
                if (r > 86 + Noise(x / 19.0, z / 19.0, seed + 20) * 15) return -1;
                return 40 + (int)(Noise(x / 23.0, z / 23.0, seed + 21) * 4);
            }
            double spawnDistance = Math.Sqrt((double)(x - 8) * (x - 8) + (double)(z - 8) * (z - 8));
            if (dimension == Dimension.Nether)
            {
                int h = 19 + (int)(Noise(x / 42.0, z / 42.0, seed + 80) * 20);
                return spawnDistance < 15 ? 32 : h;
            }
            if (spawnDistance < 16) return 32;
            double broad = 25 + Noise(x / 80.0, z / 80.0, seed) * 15;
            double detail = Noise(x / 19.0, z / 19.0, seed + 7) * 5;
            double ridges = Math.Max(0, Noise(x / 70.0, z / 70.0, seed + 41) - .58) * 48;
            double river = Math.Abs(x - (76 + Math.Sin(z / 48.0) * 16));
            double height = broad + detail + ridges;
            if (river < 10) height = height * river / 10 + 21 * (1 - river / 10);
            if (spawnDistance < 26) height = 32 + (height - 32) * (spawnDistance - 16) / 10;
            return Math.Max(8, Math.Min(65, (int)height));
        }
        internal static void Fill(int seed, Dimension dimension, int cx, int cz, Voxel[] data)
        {
            for (int z = 0; z < World.ChunkSize; z++)
            for (int x = 0; x < World.ChunkSize; x++)
            {
                int wx = cx * World.ChunkSize + x, wz = cz * World.ChunkSize + z;
                int surface = Surface(seed, dimension, wx, wz);
                for (int y = 0; y < World.Height; y++)
                {
                    Block id = Block.Air;
                    if (dimension == Dimension.End)
                    {
                        double radius = Math.Sqrt((double)wx * wx + (double)wz * wz);
                        int bottom = 12 + (int)(radius * .22);
                        if (surface >= 0 && y >= bottom && y <= surface) id = Block.EndStone;
                    }
                    else if (dimension == Dimension.Nether)
                    {
                        if (y == 0 || y == World.Height - 1) id = Block.Bedrock;
                        else if (y <= surface || y >= 84 + (int)(Noise(wx / 12.0, wz / 12.0, seed + 3) * 6))
                        {
                            id = y == surface && Noise(wx / 36.0, wz / 36.0, seed + 89) > .65 ? Block.SoulSand : Block.Netherrack;
                            if (y < surface - 3 && Hash(wx, y, wz, seed + 91) % 137 == 0) id = Block.NetherGold;
                            if (y > 80 && Hash(wx, y, wz, seed + 96) % 17 == 0) id = Block.Glowstone;
                        }
                        else if (y <= 25) id = Block.Lava;
                    }
                    else
                    {
                        if (y == 0) id = Block.Bedrock;
                        else if (y > surface) { if (y <= SeaLevel) id = Block.Water; }
                        else if (y == surface) id = surface <= SeaLevel + 1 ? Block.Sand : surface > 48 ? Block.Snow : Block.Grass;
                        else if (y >= surface - 3) id = surface <= SeaLevel + 1 ? Block.Sand : Block.Dirt;
                        else
                        {
                            double cave = Noise(wx / 16.0 + y / 11.0, wz / 16.0 - y / 13.0, seed + 303);
                            double cave2 = Noise(wx / 11.0 - y / 9.0, wz / 12.0 + y / 8.0, seed + 304);
                            if (y > 4 && y < surface - 5 && cave > .67 && cave2 > .54) id = y < 9 ? Block.Lava : Block.Air;
                            else
                            {
                                uint ore = Hash(wx, y, wz, seed + 55);
                                id = y < 15 && ore % 173 == 0 ? Block.CrystalOre : y < 35 && ore % 73 == 0 ? Block.IronOre : ore % 47 == 0 ? Block.CoalOre : Block.Stone;
                            }
                        }
                    }
                    data[World.Index(x, y, z)] = new Voxel(id);
                }
            }
            if (dimension == Dimension.Overworld) Trees(seed, cx, cz, data);
        }
        private static void Trees(int seed, int cx, int cz, Voxel[] data)
        {
            int minX = cx * 16, minZ = cz * 16;
            for (int gz = World.FloorDiv(minZ - 3, 8); gz <= World.FloorDiv(minZ + 18, 8); gz++)
            for (int gx = World.FloorDiv(minX - 3, 8); gx <= World.FloorDiv(minX + 18, 8); gx++)
            {
                uint h = Hash(gx, 0, gz, seed + 99);
                if (h % 5 > 1) continue;
                int x = gx * 8 + (int)(h % 5), z = gz * 8 + (int)((h >> 5) % 5);
                int y = Surface(seed, Dimension.Overworld, x, z), tall = 4 + (int)((h >> 9) % 3);
                if (y <= SeaLevel + 1 || y > 48 || (x - 8) * (x - 8) + (z - 8) * (z - 8) < 26 * 26) continue;
                for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                for (int dy = tall - 2; dy <= tall + 1; dy++)
                {
                    if (dy == tall + 1 && Math.Abs(dx) + Math.Abs(dz) > 1) continue;
                    PutClipped(data, minX, minZ, x + dx, y + dy, z + dz, Block.Leaves, true);
                }
                for (int dy = 1; dy <= tall; dy++) PutClipped(data, minX, minZ, x, y + dy, z, Block.Log, false);
            }
        }
        internal static void PutClipped(Voxel[] data, int minX, int minZ, int x, int y, int z, Block id, bool airOnly = false)
        {
            if (x < minX || x >= minX + 16 || z < minZ || z >= minZ + 16 || y < 0 || y >= World.Height) return;
            int i = World.Index(x - minX, y, z - minZ);
            if (!airOnly || data[i].Id == Block.Air) data[i] = new Voxel(id);
        }
    }
}
