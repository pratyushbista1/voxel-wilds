using System;
using System.Collections.Generic;
using System.Linq;
using VoxelWilds.Core;

public static class StructureTests
{
    private static readonly int[] Seeds = { 1, 7, 123, 9182 };

    public static void Run()
    {
        Spec.Run("Village paths connect all four doorways and avoid the well and irrigated field", ConnectedPaths);
        Spec.Run("Village houses have timber corners, windows, walkable interiors and pitched roofs", HouseGeometry);
        Spec.Run("Raised village foundations meet natural ground instead of floating above it", Foundations);
        Spec.Run("Village roads approach natural terrain with steps of at most one block", Approaches);
        Spec.Run("Village grading leaves the outer meadow outside building and road pads intact", Meadow);
        Spec.Run("Village grading removes the whole generated crown when clearing a tree trunk", ClearedTrees);
        Spec.Run("Village generation stays identical across forward and reverse chunk loading", ChunkSeams);
        Spec.Run("Saved roof, floor and doorway edits override rebuilt village structures", SavedEdits);
    }

    private static void ConnectedPaths()
    {
        foreach (int seed in Seeds)
        {
            var world = new World(seed, Dimension.Overworld);
            int y = Terrain.Surface(seed, Dimension.Overworld, 40, 40);
            var visited = new HashSet<Cell>();
            var pending = new Queue<Cell>();
            var center = new Cell(40, y, 40);
            visited.Add(center); pending.Enqueue(center);
            while (pending.Count > 0)
            {
                Cell cell = pending.Dequeue();
                foreach (Cell side in Cell.Sides)
                {
                    if (side.Y != 0) continue;
                    Cell next = cell + side;
                    if (Math.Abs(next.X - 40) > 20 || Math.Abs(next.Z - 40) > 20 || visited.Contains(next)) continue;
                    if (world.GetBlock(next) != Block.Gravel || world.Solid(next.Up) || world.Solid(next.Up.Up)) continue;
                    visited.Add(next); pending.Enqueue(next);
                }
            }
            foreach (int x in new[] { 29, 51 })
            foreach (int z in new[] { 30, 50 })
            {
                Spec.True(visited.Contains(new Cell(x, y, z + 4)), "Disconnected house path: " + seed + " " + x + "," + z);
                Spec.Equal(Block.Air, world.GetBlock(new Cell(x, y + 1, z + 2)), "Door has interior clearance");
                Spec.Equal(Block.Air, world.GetBlock(new Cell(x, y + 2, z + 4)), "Door has exterior headroom");
            }
            foreach (Cell edge in new[] { new Cell(20, y, 40), new Cell(60, y, 40), new Cell(40, y, 20), new Cell(40, y, 60) })
                Spec.True(visited.Contains(edge), "Village exit blocked by a building, well or field: " + edge);
        }
    }

    private static void HouseGeometry()
    {
        var world = new World(123, Dimension.Overworld);
        int y = Terrain.Surface(world.Seed, Dimension.Overworld, 40, 40);
        foreach (int x in new[] { 29, 51 })
        foreach (int z in new[] { 30, 50 })
        {
            bool workshop = (x == 51) != (z == 50);
            Spec.Equal(Block.Planks, world.GetBlock(new Cell(x, y, z)), "Timber interior floor");
            Spec.Equal(Block.Log, world.GetBlock(new Cell(x - 3, y + 2, z - 3)), "Structural corner timber");
            Spec.Equal(Block.Glass, world.GetBlock(new Cell(x - 3, y + 2, z)), "Side window");
            Spec.Equal(Block.Glass, world.GetBlock(new Cell(x - 2, y + 2, z + 3)), "Entrance window");
            for (int dy = 1; dy <= 3; dy++)
            for (int dz = 0; dz <= 2; dz++)
                Spec.Equal(Block.Air, world.GetBlock(new Cell(x, y + dy, z + dz)), "Interior walking space");
            for (int across = -4; across <= 4; across++)
            {
                int roof = y + 4 + Math.Max(0, 3 - Math.Abs(across));
                Cell roofCell = workshop ? new Cell(x, roof, z + across) : new Cell(x + across, roof, z);
                Spec.Equal(Block.Planks, world.GetBlock(roofCell), "Continuous gabled roof");
                Spec.Equal(Block.Air, world.GetBlock(roofCell.Up), "No flat slab above roof slope");
            }
            Spec.True(!world.SkyVisible(new Cell(x, y + 2, z)), "Roof shelters the interior");
            Spec.Equal(Block.Bed, world.GetBlock(new Cell(x - 2, y + 1, z - 2)));
            Spec.Equal(Block.BedHead, world.GetBlock(new Cell(x - 2, y + 1, z - 1)));
            if (workshop)
                for (int dy = 2; dy <= 8; dy++) Spec.Equal(Block.Bricks, world.GetBlock(new Cell(x + 2, y + dy, z - 1)), "Chimney is supported continuously above furnace");
            else Spec.Equal(Block.Glass, world.GetBlock(new Cell(x, y + 5, z + 3)), "Cottage gable window");
        }
    }

    private static void Foundations()
    {
        foreach (int seed in Seeds)
        {
            var world = new World(seed, Dimension.Overworld);
            int y = Terrain.Surface(seed, Dimension.Overworld, 40, 40);
            foreach (int hx in new[] { 29, 51 })
            foreach (int hz in new[] { 30, 50 })
            for (int z = hz - 3; z <= hz + 3; z++)
            for (int x = hx - 3; x <= hx + 3; x++)
            {
                int natural = Terrain.Surface(seed, Dimension.Overworld, x, z);
                for (int fy = Math.Min(natural, y) - 2; fy <= y; fy++)
                    Spec.True(world.Solid(new Cell(x, fy, z)), "Unsupported foundation: " + seed + " " + x + "," + fy + "," + z);
            }
            for (int z = 48; z <= 58; z++)
                Spec.Equal(Block.Dirt, world.GetBlock(new Cell(40, y - 1, z)), "Irrigation channel has a sealed floor");
        }
    }

    private static void Approaches()
    {
        foreach (int seed in Seeds)
        {
            var world = new World(seed, Dimension.Overworld);
            int y = Terrain.Surface(seed, Dimension.Overworld, 40, 40);
            foreach (Cell direction in new[] { new Cell(1, 0, 0), new Cell(-1, 0, 0), new Cell(0, 0, 1), new Cell(0, 0, -1) })
            {
                int previous = y;
                bool joined = false;
                for (int distance = 21; distance <= 48; distance++)
                {
                    int x = 40 + direction.X * distance, z = 40 + direction.Z * distance;
                    int natural = Terrain.Surface(seed, Dimension.Overworld, x, z);
                    int height = previous + Math.Max(-1, Math.Min(1, natural - previous));
                    Spec.Equal(Block.Gravel, world.GetBlock(new Cell(x, height, z)), "No gap at chunk boundary or height transition");
                    Spec.Equal(Block.Air, world.GetBlock(new Cell(x, height + 1, z)), "Road is clear of terrain and trunks");
                    Spec.Equal(Block.Air, world.GetBlock(new Cell(x, height + 2, z)), "Road has headroom");
                    Spec.True(world.Solid(new Cell(x, height - 1, z)), "Road has support");
                    Spec.True(Math.Abs(height - previous) <= 1);
                    previous = height;
                    if (distance >= 28 && height == natural) { joined = true; break; }
                }
                Spec.True(joined, "Road must reach surrounding ground before ending");
            }
        }
    }

    private static void Meadow()
    {
        foreach (int seed in Seeds)
        {
            var world = new World(seed, Dimension.Overworld);
            foreach (Cell column in new[] { new Cell(20, 0, 20), new Cell(60, 0, 20) })
            {
                int natural = Terrain.Surface(seed, Dimension.Overworld, column.X, column.Z);
                Spec.Equal(Block.Grass, world.GetBlock(new Cell(column.X, natural, column.Z)), "Outer meadow keeps its original ground elevation");
                Spec.Equal(Block.Dirt, world.GetBlock(new Cell(column.X, natural - 1, column.Z)), "No raised square plateau");
            }
        }
    }

    private static void ChunkSeams()
    {
        var a = new World(9182, Dimension.Overworld); var b = new World(9182, Dimension.Overworld);
        var chunks = new[] { new Cell(-1, 0, 2), new Cell(0, 0, 2), new Cell(1, 0, 1), new Cell(2, 0, 1), new Cell(3, 0, 1), new Cell(1, 0, 3), new Cell(2, 0, 3), new Cell(3, 0, 3), new Cell(4, 0, 2), new Cell(2, 0, 4), new Cell(2, 0, 5) };
        foreach (Cell chunk in chunks) a.EnsureChunk(chunk.X, chunk.Z);
        foreach (Cell chunk in chunks.Reverse()) b.EnsureChunk(chunk.X, chunk.Z);
        foreach (Cell chunk in chunks)
        for (int z = 0; z < World.ChunkSize; z++)
        for (int x = 0; x < World.ChunkSize; x++)
        for (int y = 0; y < World.Height; y++)
        {
            var cell = new Cell(chunk.X * World.ChunkSize + x, y, chunk.Z * World.ChunkSize + z);
            Spec.Equal(a.Get(cell), b.Get(cell), "Village load-order seam");
        }
    }

    private static void ClearedTrees()
    {
        foreach (int seed in new[] { 1453, 123, 9182 })
        {
            var world = new World(seed, Dimension.Overworld);
            int cleared = 0, retained = 0;
            for (int gz = -2; gz <= 12; gz++)
            for (int gx = -2; gx <= 12; gx++)
            {
                uint hash = Terrain.Hash(gx, 0, gz, seed + 99);
                if (hash % 5 > 1) continue;
                int x = gx * 8 + (int)(hash % 5), z = gz * 8 + (int)((hash >> 5) % 5);
                int ground = Terrain.Surface(seed, Dimension.Overworld, x, z), tall = 4 + (int)((hash >> 9) % 3);
                if (ground <= Terrain.SeaLevel + 1 || ground > 48 || (x - 8) * (x - 8) + (z - 8) * (z - 8) < 26 * 26) continue;
                if (world.GetBlock(new Cell(x, ground + tall, z)) == Block.Log)
                {
                    retained++;
                    continue;
                }
                cleared++;
                for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                for (int dy = tall - 2; dy <= tall + 1; dy++)
                {
                    if (dy == tall + 1 && Math.Abs(dx) + Math.Abs(dz) > 1) continue;
                    var leaf = new Cell(x + dx, ground + dy, z + dz);
                    Spec.True(world.GetBlock(leaf) != Block.Leaves, "Disconnected crown at " + leaf + ", seed " + seed);
                }
            }
            Spec.True(cleared > 0, "Fixture must include trees cut by buildings or paths");
            Spec.True(retained > 0, "Unrelated meadow trees remain intact");
        }
        var saved = new World(1453, Dimension.Overworld);
        var placedLeaves = new Cell(30, 37, 62);
        saved.ApplyEdits(new[] { new KeyValuePair<Cell, Voxel>(placedLeaves, new Voxel(Block.Leaves)) });
        Spec.Equal(Block.Leaves, saved.GetBlock(placedLeaves), "Generation cleanup never removes saved player leaf blocks");
    }

    private static void SavedEdits()
    {
        int y = Terrain.Surface(123, Dimension.Overworld, 40, 40);
        var edits = new[]
        {
            new KeyValuePair<Cell, Voxel>(new Cell(29, y + 7, 30), new Voxel(Block.Glass)),
            new KeyValuePair<Cell, Voxel>(new Cell(29, y, 30), new Voxel(Block.GoldBlock)),
            new KeyValuePair<Cell, Voxel>(new Cell(29, y + 1, 33), new Voxel(Block.Air)),
            new KeyValuePair<Cell, Voxel>(new Cell(30, y + 1, 32), new Voxel(Block.Chest))
        };
        var world = new World(123, Dimension.Overworld);
        world.ApplyEdits(edits);
        foreach (var edit in edits) Spec.Equal(edit.Value, world.Get(edit.Key), "Existing player edits have priority");
        Spec.Equal(Block.Air, world.GetBlock(new Cell(29, y + 2, 33)), "Removed doorway stays removed");
    }
}
