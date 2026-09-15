using System;
using System.Collections.Generic;
using System.Linq;
using VoxelWilds.Core;

public static class WorldTests
{
    public static void Run()
    {
        Spec.Run("Cell coordinates, equality, hashing and negative chunk division", Cells);
        Spec.Run("Block ids retain legacy save compatibility and fluid metadata", BlockMetadata);
        Spec.Run("Chunks and cross-boundary decorations are independent of loading order", ChunkDeterminism);
        Spec.Run("Terrain seed changes the generated landscape", SeedVariation);
        Spec.Run("Generated terrain includes trees, ores, caves and natural fluids", NaturalFeatures);
        Spec.Run("Overworld spawn and End landing are safe", SafeSpawns);
        Spec.Run("Village houses, crops, villagers and loot generate together", Village);
        Spec.Run("Generated dungeon and fortress spawners match markers", Spawners);
        Spec.Run("Nether fortress and bastion have solid floors and loot", NetherStructures);
        Spec.Run("End island has eight crystal pillars, dragon and exit", EndStructures);
        Spec.Run("Edits persist before and after chunk generation with fluid levels", Edits);
        Spec.Run("Block changes are observable and idempotent", ChangeEvents);
        Spec.Run("Sky visibility respects solid roofs", Sky);
        Spec.Run("Water waits five ticks, falls first and spreads after landing", WaterFalls);
        Spec.Run("Water spread has levels one to seven then stops", WaterRange);
        Spec.Run("Unsupported water columns do not spread sideways in mid-air", DownwardPriority);
        Spec.Run("Water flows toward a nearby drop before other directions", PreferredDrop);
        Spec.Run("Removing a water source drains its horizontal flow", WaterDrains);
        Spec.Run("Removing a waterfall source drains the vertical column and pool", WaterfallDrains);
        Spec.Run("Opening the floor under a source drains unsupported side flow", RemovedSupport);
        Spec.Run("Two supported adjacent water sources regenerate a source", InfiniteWater);
        Spec.Run("Two adjacent sources cannot regenerate water above air", UnsupportedInfiniteWater);
        Spec.Run("Overworld lava takes thirty ticks and spreads three blocks", OverworldLava);
        Spec.Run("Nether lava takes ten ticks and spreads seven blocks", NetherLava);
        Spec.Run("Lava source removal drains lava without source regeneration", LavaDrains);
        Spec.Run("Water touching lava source creates obsidian", Obsidian);
        Spec.Run("Water touching flowing lava creates cobblestone", Cobblestone);
        Spec.Run("Lava flowing downward into water creates stone", Stone);
        Spec.Run("Water above lava source creates obsidian below", WaterAboveLava);
        Spec.Run("Water evaporates in the Nether", NetherWater);
        Spec.Run("Fluid updates obey their work budget and never generate chunks", BoundedFluid);
        Spec.Run("Loading a neighboring chunk wakes fluid at the old boundary", BoundaryWake);
        Spec.Run("Reloaded flow resumes and settles without losing its level", FluidReload);
        Spec.Run("Placed barriers block flow and removing them wakes sources", Barrier);
        Spec.Run("Invalid tick durations cannot corrupt the scheduler", InvalidTime);
    }
    private static void Cells()
    {
        Cell p = new Cell(-1, 5, 16);
        Spec.Equal(new Cell(-1, 6, 16), p.Up);
        Spec.Equal(p, p.Down.Up);
        Spec.Equal(new Cell(0, 5, 16), p + new Cell(1, 0, 0));
        Spec.True(p != new Cell(-1, 4, 16));
        var lookup = new Dictionary<Cell, int> { [p] = 7 };
        Spec.Equal(7, lookup[new Cell(-1, 5, 16)]);
        Spec.Equal(-1, World.FloorDiv(-1, 16)); Spec.Equal(-1, World.FloorDiv(-16, 16));
        Spec.Equal(-2, World.FloorDiv(-17, 16)); Spec.Equal(1, World.FloorDiv(16, 16));
    }
    private static void BlockMetadata()
    {
        Spec.Equal(25, (int)Block.Bed); Spec.Equal(33, (int)Block.Obsidian); Spec.Equal(47, (int)Block.Spawner);
        Spec.Equal(48, (int)Block.EndStone); Spec.True(Blocks.IsFluid(Block.Water) && !Blocks.IsSolid(Block.Water));
        Spec.True(Blocks.IsTransparent(Block.Glass) && Blocks.IsSolid(Block.Glass));
        Spec.True(Blocks.IsSolid(Block.Spawner)); Spec.Equal(0, Blocks.Drop(Block.Spawner));
        Spec.Equal(25, Blocks.Drop(Block.BedWestHead)); Spec.Equal(8, Blocks.Drop(Block.Stone));
        Spec.True(float.IsPositiveInfinity(Blocks.Hardness(Block.Bedrock)));
        Spec.Equal((byte)8, new Voxel(Block.Water, 99).Level);
        Spec.Equal((byte)0, new Voxel(Block.Stone, 7).Level);
    }
    private static void ChunkDeterminism()
    {
        foreach (Dimension dimension in Enum.GetValues(typeof(Dimension)))
        {
            var a = new World(9182, dimension); var b = new World(9182, dimension);
            var chunks = new[] { new Cell(-1, 0, -1), new Cell(0, 0, 0), new Cell(1, 0, 0), new Cell(1, 0, 2), new Cell(2, 0, 2), new Cell(3, 0, 2), new Cell(3, 0, 3), new Cell(6, 0, 6) };
            foreach (Cell c in chunks) a.EnsureChunk(c.X, c.Z);
            foreach (Cell c in chunks.Reverse()) b.EnsureChunk(c.X, c.Z);
            foreach (Cell c in chunks)
            for (int y = 0; y < World.Height; y++)
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                Cell p = new Cell(c.X * 16 + x, y, c.Z * 16 + z);
                if (!a.Get(p).Equals(b.Get(p))) throw new Exception(dimension + " differs at " + p);
            }
            Spec.Equal(MarkerSignature(a), MarkerSignature(b), dimension + " marker order independence");
        }
    }
    private static string MarkerSignature(World world) => string.Join("|", world.Markers.Select(m => m.Position + ":" + m.Kind + ":" + m.Mob).OrderBy(s => s, StringComparer.Ordinal));
    private static void SeedVariation()
    {
        int different = 0;
        for (int x = -100; x <= 100; x += 5)
            if (Terrain.Surface(13, Dimension.Overworld, x, 85) != Terrain.Surface(777, Dimension.Overworld, x, 85)) different++;
        Spec.True(different > 20, "Seeds should substantially change terrain");
    }
    private static void NaturalFeatures()
    {
        var world = new World(9182, Dimension.Overworld);
        for (int z = -2; z <= 3; z++) for (int x = -2; x <= 5; x++) world.EnsureChunk(x, z);
        var ids = new HashSet<Block>(world.GeneratedVoxels.Select(e => e.Value.Id));
        foreach (Block id in new[] { Block.Log, Block.Leaves, Block.CoalOre, Block.IronOre, Block.CrystalOre, Block.Water, Block.Lava, Block.Bedrock })
            Spec.True(ids.Contains(id), "Generated " + id);
        bool cave = false;
        for (int x = -30; x < 0; x++)
        for (int z = -30; z < 0; z++)
        for (int y = 10; y < 20; y++) cave |= world.GetBlock(new Cell(x, y, z)) == Block.Air;
        Spec.True(cave, "Caves below the natural surface");
    }
    private static void SafeSpawns()
    {
        foreach (int seed in new[] { 0, 1, -200, int.MaxValue })
        {
            var world = new World(seed, Dimension.Overworld);
            Spec.Equal(32, world.HeightAt(8, 8)); Spec.True(world.Solid(new Cell(8, 32, 8)));
            Spec.Equal(Block.Air, world.GetBlock(new Cell(8, 33, 8))); Spec.Equal(Block.Air, world.GetBlock(new Cell(8, 34, 8)));
            var end = new World(seed, Dimension.End);
            Spec.Equal(Block.Obsidian, end.GetBlock(new Cell(8, 44, 8)));
            Spec.Equal(Block.Air, end.GetBlock(new Cell(8, 45, 8))); Spec.Equal(Block.Air, end.GetBlock(new Cell(8, 46, 8)));
            Spec.Equal(Block.EndPortal, world.GetBlock(new Cell(24, 33, 8)));
        }
    }
    private static void Village()
    {
        var world = new World(123, Dimension.Overworld);
        for (int z = 1; z <= 3; z++) for (int x = 1; x <= 3; x++) world.EnsureChunk(x, z);
        int y = Terrain.Surface(123, Dimension.Overworld, 40, 40);
        Spec.True(world.Markers.Any(m => m.Kind == "village" && m.Position == new Cell(40, y + 1, 40)));
        Spec.Equal(4, world.Markers.Count(m => m.Kind == "mob" && m.Mob == "villager"));
        Spec.Equal(Block.Crop, world.GetBlock(new Cell(42, y + 1, 50)));
        Spec.Equal(Block.Water, world.GetBlock(new Cell(40, y, 50)));
        Spec.Equal(Block.Farmland, world.GetBlock(new Cell(42, y, 50)));
        foreach (var marker in world.Markers.Where(m => m.Kind == "mob" && m.Mob == "villager").ToArray())
        {
            Spec.True(world.Solid(marker.Position.Down), "Villager floor");
            Spec.True(!world.Solid(marker.Position) && !world.Solid(marker.Position.Up), "Villager room");
            Spec.Equal(Block.Air, world.GetBlock(marker.Position + new Cell(0, 0, 3)), "Doorway is walkable");
        }
        Spec.True(world.Markers.Count(m => m.Kind == "chest" && m.Mob == "village") >= 2);
    }
    private static void Spawners()
    {
        var world = new World(1, Dimension.Overworld); world.EnsureChunk(1, 2);
        var marker = world.Markers.Single(m => m.Kind == "spawner");
        Spec.Equal("zombie", marker.Mob); Spec.Equal(new Cell(16, 26, 40), marker.Position);
        Spec.Equal(Block.Spawner, world.GetBlock(marker.Position)); Spec.Equal(Block.Cobble, world.GetBlock(marker.Position.Down));
        Spec.Equal(Block.Air, world.GetBlock(marker.Position + new Cell(2, 0, 0)));
        var nether = new World(1, Dimension.Nether); nether.EnsureChunk(3, 3);
        var blaze = nether.Markers.Single(m => m.Kind == "spawner");
        Spec.Equal("blaze", blaze.Mob); Spec.Equal(new Cell(48, 37, 63), blaze.Position);
        Spec.Equal(Block.Spawner, nether.GetBlock(blaze.Position)); Spec.True(nether.Solid(blaze.Position.Down));
    }
    private static void NetherStructures()
    {
        var world = new World(1, Dimension.Nether);
        Spec.Equal(Block.NetherBricks, world.GetBlock(new Cell(48, 36, 40)));
        Spec.Equal(Block.Air, world.GetBlock(new Cell(48, 37, 40)));
        Spec.Equal(Block.Chest, world.GetBlock(new Cell(43, 37, 35)));
        Spec.Equal(Block.GoldBlock, world.GetBlock(new Cell(108, 35, 108)));
        Spec.Equal(Block.Chest, world.GetBlock(new Cell(108, 36, 108)));
        Spec.Equal(Block.Lava, world.GetBlock(new Cell(111, 34, 108)));
        Spec.Equal(Block.Blackstone, world.GetBlock(new Cell(102, 34, 100)));
        Spec.Equal(Block.Air, world.GetBlock(new Cell(102, 35, 100)));
        Spec.True(world.Markers.Any(m => m.Kind == "chest" && m.Mob == "bastion"));
    }
    private static void EndStructures()
    {
        var world = new World(12, Dimension.End);
        for (int z = -4; z <= 3; z++) for (int x = -4; x <= 3; x++) world.EnsureChunk(x, z);
        Spec.Equal(8, world.Markers.Count(m => m.Kind == "crystal"));
        Spec.Equal(1, world.Markers.Count(m => m.Kind == "dragon" && m.Mob == "ender_dragon"));
        Spec.Equal(Block.EndPortal, world.GetBlock(new Cell(0, 45, 0)));
        Spec.Equal(Block.Air, world.GetBlock(new Cell(130, 30, 130)));
        Spec.Equal(Block.Air, world.GetBlock(new Cell(100, -1, 100)), "End void below world");
        foreach (var crystal in world.Markers.Where(m => m.Kind == "crystal").ToArray())
        {
            Spec.Equal(Block.Bedrock, world.GetBlock(crystal.Position.Down));
            Spec.Equal(Block.Obsidian, world.GetBlock(crystal.Position.Down.Down));
        }
    }
    private static void Edits()
    {
        var source = new World(5, Dimension.Overworld);
        source.Set(new Cell(-2, 70, 17), Block.Water, 6); source.Set(new Cell(8, 32, 8), Block.Air);
        var saved = source.Edits.ToArray(); var target = new World(5, Dimension.Overworld);
        target.ApplyEdits(saved); Spec.Equal(0, target.LoadedChunks.Count(), "Loading edits should not generate terrain");
        Spec.Equal(new Voxel(Block.Water, 6), target.Get(new Cell(-2, 70, 17)));
        Spec.Equal(Block.Air, target.GetBlock(new Cell(8, 32, 8)));
        target.ApplyEdits(new[] { new KeyValuePair<Cell, Voxel>(new Cell(8, 32, 8), new Voxel(Block.GoldBlock)) });
        Spec.Equal(Block.GoldBlock, target.GetBlock(new Cell(8, 32, 8)));
        target.ApplyEdits(new[] { new KeyValuePair<Cell, Voxel>(new Cell(0, -1, 0), new Voxel(Block.Water)), new KeyValuePair<Cell, Voxel>(new Cell(0, 70, 0), new Voxel((Block)999)) });
        Spec.Equal(2, target.Edits.Count(), "Invalid edits are ignored");
    }
    private static void ChangeEvents()
    {
        var world = new World(1, Dimension.Overworld); int changes = 0;
        world.Changed += p => changes++;
        world.Set(new Cell(8, 70, 8), Block.Water, 2); world.Set(new Cell(8, 70, 8), Block.Water, 2);
        world.Set(new Cell(8, 70, 8), Block.Water, 3); world.Set(new Cell(8, 96, 8), Block.Stone);
        Spec.Equal(2, changes);
    }
    private static void Sky()
    {
        var world = new World(1, Dimension.Overworld); var p = new Cell(8, 65, 8);
        Spec.True(world.SkyVisible(p)); world.Set(p.Up, Block.Stone); Spec.True(!world.SkyVisible(p));
        world.Set(p.Up, Block.Glass); Spec.True(world.SkyVisible(p));
    }
    private static World Arena(Dimension dimension = Dimension.Overworld)
    {
        var world = new World(9182, dimension); world.EnsureChunk(0, 0);
        foreach (var entry in world.GeneratedVoxels.Where(e => Blocks.IsFluid(e.Value.Id)).ToArray()) world.Set(entry.Key, Block.Stone);
        for (int z = 0; z < 16; z++)
        for (int x = 0; x < 16; x++)
        {
            world.Set(new Cell(x, 64, z), Block.Stone);
            for (int y = 65; y <= 80; y++) world.Set(new Cell(x, y, z), x == 0 || x == 15 || z == 0 || z == 15 ? Block.Stone : Block.Air);
        }
        return world;
    }
    private static readonly Cell Source = new Cell(7, 65, 7);
    private static void Run(FluidSimulation fluid, float seconds = 8)
    {
        for (int i = 0; i < (int)Math.Ceiling(seconds * 20); i++) fluid.Tick(.05f, 10000);
        fluid.Tick(0, 10000);
    }
    private static void WaterFalls()
    {
        var world = Arena(); var source = new Cell(7, 68, 7);
        using var fluid = new FluidSimulation(world); world.Set(source, Block.Water);
        fluid.Tick(.2f); Spec.Equal(Block.Air, world.GetBlock(source.Down));
        fluid.Tick(.051f); Spec.Equal(new Voxel(Block.Water, 8), world.Get(source.Down));
        Run(fluid, 3); Spec.Equal(Block.Water, world.GetBlock(Source)); Spec.Equal((byte)8, world.Get(Source).Level);
        Spec.Equal(new Voxel(Block.Water, 1), world.Get(Source + new Cell(1, 0, 0)));
    }
    private static void WaterRange()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Water); Run(fluid);
        for (int distance = 1; distance <= 7; distance++) Spec.Equal(new Voxel(Block.Water, (byte)distance), world.Get(Source + new Cell(distance, 0, 0)), "Water distance " + distance);
        Spec.Equal(Block.Air, world.GetBlock(Source + new Cell(5, 0, 3)), "Water must not exceed taxicab distance seven");
        Spec.Equal(0, fluid.PendingCount, "Still water must stop scheduling work");
    }
    private static void DownwardPriority()
    {
        var world = Arena(); var source = new Cell(7, 70, 7); using var fluid = new FluidSimulation(world); world.Set(source, Block.Water); Run(fluid, 3);
        Spec.Equal(Block.Air, world.GetBlock(source + new Cell(1, 0, 0)));
        Spec.Equal(Block.Air, world.GetBlock(source.Down + new Cell(1, 0, 0)));
        Spec.Equal(Block.Water, world.GetBlock(Source));
    }
    private static void PreferredDrop()
    {
        var world = Arena(); world.Set(Source.Down + new Cell(2, 0, 0), Block.Air);
        using var fluid = new FluidSimulation(world); world.Set(Source, Block.Water); fluid.Tick(.251f, 1000);
        Spec.Equal(Block.Water, world.GetBlock(Source + new Cell(1, 0, 0)));
        Spec.Equal(Block.Air, world.GetBlock(Source + new Cell(-1, 0, 0)));
        Spec.Equal(Block.Air, world.GetBlock(Source + new Cell(0, 0, 1)));
    }
    private static void WaterDrains()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Water); Run(fluid);
        world.Set(Source, Block.Air); Run(fluid, 12);
        Spec.Equal(0, world.GeneratedVoxels.Count(e => e.Value.Id == Block.Water)); Spec.Equal(0, fluid.PendingCount);
    }
    private static void WaterfallDrains()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); var p = new Cell(7, 72, 7); world.Set(p, Block.Water); Run(fluid, 12);
        world.Set(p, Block.Air); Run(fluid, 20);
        Spec.Equal(0, world.GeneratedVoxels.Count(e => e.Value.Id == Block.Water), "Disconnected falling and lateral water must vanish");
    }
    private static void RemovedSupport()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Water); Run(fluid, 6);
        Spec.Equal(Block.Water, world.GetBlock(Source + new Cell(1, 0, 0)));
        world.Set(Source.Down, Block.Air); world.Set(Source.Down.Down, Block.Stone); Run(fluid, 12);
        Spec.Equal(Block.Water, world.GetBlock(Source.Down));
        Spec.Equal(Block.Air, world.GetBlock(Source + new Cell(1, 0, 0)), "Source now feeds downward instead of leaving a suspended side pool");
    }
    private static void InfiniteWater()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world);
        world.Set(Source + new Cell(-1, 0, 0), Block.Water); world.Set(Source + new Cell(1, 0, 0), Block.Water); Run(fluid, 2);
        Spec.Equal(new Voxel(Block.Water, 0), world.Get(Source));
        world.Set(Source, Block.Air); Run(fluid, 2); Spec.Equal(new Voxel(Block.Water, 0), world.Get(Source), "Removed middle source regenerates");
    }
    private static void UnsupportedInfiniteWater()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); world.Set(Source.Down, Block.Air);
        world.Set(Source + new Cell(-1, 0, 0), Block.Water); world.Set(Source + new Cell(1, 0, 0), Block.Water); Run(fluid, 2);
        Spec.Equal(Block.Water, world.GetBlock(Source)); Spec.True(world.Get(Source).Level != 0);
    }
    private static void OverworldLava()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Lava);
        fluid.Tick(1.45f); Spec.Equal(Block.Air, world.GetBlock(Source + new Cell(1, 0, 0)));
        fluid.Tick(.051f); Spec.Equal(new Voxel(Block.Lava, 2), world.Get(Source + new Cell(1, 0, 0)));
        Run(fluid, 15);
        Spec.Equal(new Voxel(Block.Lava, 6), world.Get(Source + new Cell(3, 0, 0)));
        Spec.Equal(Block.Air, world.GetBlock(Source + new Cell(4, 0, 0))); Spec.Equal(0, fluid.PendingCount);
    }
    private static void NetherLava()
    {
        var world = Arena(Dimension.Nether); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Lava);
        fluid.Tick(.45f); Spec.Equal(Block.Air, world.GetBlock(Source + new Cell(1, 0, 0)));
        fluid.Tick(.051f); Spec.Equal(new Voxel(Block.Lava, 1), world.Get(Source + new Cell(1, 0, 0)));
        Run(fluid, 10); Spec.Equal(new Voxel(Block.Lava, 7), world.Get(Source + new Cell(7, 0, 0)));
        Spec.Equal(Block.Air, world.GetBlock(Source + new Cell(5, 0, 3)));
    }
    private static void LavaDrains()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Lava); Run(fluid, 15);
        world.Set(Source, Block.Air); Run(fluid, 30); Spec.Equal(0, world.GeneratedVoxels.Count(e => e.Value.Id == Block.Lava));
        world.Set(Source + new Cell(-1, 0, 0), Block.Lava); world.Set(Source + new Cell(1, 0, 0), Block.Lava); Run(fluid, 5);
        Spec.Equal(new Voxel(Block.Lava, 2), world.Get(Source), "Lava does not regenerate sources");
    }
    private static void Obsidian()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Lava); world.Set(Source + new Cell(1, 0, 0), Block.Water); Run(fluid, 1);
        Spec.Equal(Block.Obsidian, world.GetBlock(Source));
    }
    private static void Cobblestone()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Lava, 2); world.Set(Source + new Cell(1, 0, 0), Block.Water); Run(fluid, 1);
        Spec.Equal(Block.Cobble, world.GetBlock(Source));
    }
    private static void Stone()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); var lava = new Cell(7, 70, 7); world.Set(lava, Block.Lava); world.Set(lava.Down, Block.Water); Run(fluid, 2);
        Spec.Equal(Block.Stone, world.GetBlock(lava.Down)); Spec.Equal(Block.Lava, world.GetBlock(lava));
    }
    private static void WaterAboveLava()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Lava); world.Set(Source.Up, Block.Water); Run(fluid, 1);
        Spec.Equal(Block.Obsidian, world.GetBlock(Source));
    }
    private static void NetherWater()
    {
        var world = Arena(Dimension.Nether); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Water); Run(fluid, 1);
        Spec.Equal(0, world.GeneratedVoxels.Count(e => e.Value.Id == Block.Water));
    }
    private static void BoundedFluid()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Water);
        fluid.Tick(60, 1); Spec.Equal(1, fluid.LastProcessedCount); Spec.True(fluid.PendingCount > 0); Spec.True(fluid.PendingCount <= 100000);
        for (int i = 0; i < 100; i++) { fluid.Tick(0, 1); Spec.True(fluid.LastProcessedCount <= 1); }
        Spec.Equal(1, world.LoadedChunks.Count(), "Fluid simulation cannot generate adjacent chunks");
        fluid.Tick(0, 10000); Spec.Equal(0, fluid.PendingCount);
    }
    private static void BoundaryWake()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); var p = new Cell(15, 70, 7); world.Set(p, Block.Water);
        world.Set(p.Down, Block.Stone); Run(fluid, 2);
        Spec.Equal(1, world.LoadedChunks.Count()); world.EnsureChunk(1, 0);
        world.Set(new Cell(16, 69, 7), Block.Stone); Run(fluid, 2);
        Spec.Equal(Block.Water, world.GetBlock(new Cell(16, 70, 7))); Spec.Equal(2, world.LoadedChunks.Count());
    }
    private static void FluidReload()
    {
        var original = Arena(); original.Set(Source, Block.Water); original.Set(Source + new Cell(1, 0, 0), Block.Water, 1);
        var world = new World(original.Seed, original.Dimension); world.ApplyEdits(original.Edits.ToArray()); world.EnsureChunk(0, 0);
        using var fluid = new FluidSimulation(world); Run(fluid, 5);
        Spec.Equal(new Voxel(Block.Water, 7), world.Get(Source + new Cell(7, 0, 0)));
        world.Set(Source, Block.Air); Run(fluid, 10); Spec.Equal(0, world.GeneratedVoxels.Count(e => e.Value.Id == Block.Water));
    }
    private static void Barrier()
    {
        var world = Arena(); foreach (var side in Cell.Sides) world.Set(Source + side, Block.Glass);
        using var fluid = new FluidSimulation(world); world.Set(Source, Block.Water); Run(fluid, 1);
        Spec.Equal(1, world.GeneratedVoxels.Count(e => e.Value.Id == Block.Water));
        world.Set(Source + new Cell(1, 0, 0), Block.Air); Run(fluid, 2); Spec.Equal(Block.Water, world.GetBlock(Source + new Cell(1, 0, 0)));
    }
    private static void InvalidTime()
    {
        var world = Arena(); using var fluid = new FluidSimulation(world); world.Set(Source, Block.Water);
        fluid.Tick(float.NaN); fluid.Tick(float.PositiveInfinity); fluid.Tick(-1); fluid.Tick(1, 0);
        Spec.Equal(0L, fluid.ElapsedTicks); Spec.Equal(Block.Air, world.GetBlock(Source + new Cell(1, 0, 0)));
        fluid.Tick(.251f); Spec.Equal(Block.Water, world.GetBlock(Source + new Cell(1, 0, 0)));
    }
}
