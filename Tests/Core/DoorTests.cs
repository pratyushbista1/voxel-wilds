using System.Collections.Generic;
using System.Linq;
using VoxelWilds.Core;

public static class DoorTests
{
    private static readonly Cell Foot = new Cell(8, 70, 8);
    public static void Run()
    {
        Spec.Run("Door metadata keeps the original block id and all sixteen states", Metadata);
        Spec.Run("Door placement creates supported lower and upper halves", Placement);
        Spec.Run("Door placement rejects blocked heads and unsupported floors without editing", InvalidPlacement);
        Spec.Run("Using either half toggles both halves and navigation occupancy", Toggle);
        Spec.Run("Door selection follows the thin closed and open panels in all directions", Bounds);
        Spec.Run("Breaking either door half removes its partner only", Remove);
        Spec.Run("Removing a door floor removes the unsupported pair", Support);
        Spec.Run("Door orientation, upper half and open state survive saved world edits", Reload);
        Spec.Run("Legacy single-block doors stay usable without overwriting their neighbors", Legacy);
        Spec.Run("Existing village entrance edits override newly generated doors", VillageEdits);
    }
    private static World Arena()
    {
        var world = new World(7, Dimension.Overworld);
        world.Set(Foot.Down, Block.Stone);
        world.Set(Foot, Block.Air);
        world.Set(Foot.Up, Block.Air);
        return world;
    }
    private static void Metadata()
    {
        Spec.Equal(52, (int)Block.Door);
        for (byte value = 0; value < 16; value++) Spec.Equal(value, new Voxel(Block.Door, value).Level);
        Spec.Equal((byte)15, new Voxel(Block.Door, 255).Level);
        Spec.Equal((byte)8, new Voxel(Block.Water, 255).Level);
        Spec.Equal((byte)0, new Voxel(Block.Stone, 255).Level);
        Spec.Equal((int)Block.Door, Blocks.Drop(Block.Door));
    }
    private static void Placement()
    {
        for (int facing = 0; facing < 4; facing++)
        {
            var world = Arena();
            Spec.True(DoorRules.TryPlace(world, Foot, facing));
            Spec.Equal(new Voxel(Block.Door, DoorRules.State(facing)), world.Get(Foot));
            Spec.Equal(new Voxel(Block.Door, DoorRules.State(facing, false, true)), world.Get(Foot.Up));
            Spec.True(world.Solid(Foot) && world.Solid(Foot.Up));
        }
    }
    private static void InvalidPlacement()
    {
        foreach (Block block in new[] { Block.Stone, Block.Glass, Block.Door })
        {
            var world = Arena(); world.Set(Foot.Up, block);
            int count = world.Edits.Count();
            Spec.True(!DoorRules.TryPlace(world, Foot, 0));
            Spec.Equal(Block.Air, world.GetBlock(Foot)); Spec.Equal(block, world.GetBlock(Foot.Up));
            Spec.Equal(count, world.Edits.Count());
        }
        foreach (Block block in new[] { Block.Air, Block.Water, Block.Bed, Block.Farmland, Block.Door })
        {
            var world = Arena(); world.Set(Foot.Down, block);
            Spec.True(!DoorRules.TryPlace(world, Foot, 0));
        }
        Spec.True(!DoorRules.TryPlace(Arena(), new Cell(8, World.Height-1, 8), 0));
    }
    private static void Toggle()
    {
        var world = Arena(); DoorRules.TryPlace(world, Foot, 3);
        Spec.True(DoorRules.Toggle(world, Foot.Up));
        Spec.True(DoorRules.IsOpen(world.Get(Foot)) && DoorRules.IsOpen(world.Get(Foot.Up)));
        Spec.True(!world.Solid(Foot) && !world.Solid(Foot.Up));
        DoorRules.Toggle(world, Foot);
        Spec.True(!DoorRules.IsOpen(world.Get(Foot)) && !DoorRules.IsOpen(world.Get(Foot.Up)));
        Spec.True(world.Solid(Foot)); Spec.True(!DoorRules.Toggle(world, Foot.Down));
    }
    private static void Bounds()
    {
        for (int facing = 0; facing < 4; facing++)
        for (int open = 0; open < 2; open++)
        {
            var door = new Voxel(Block.Door, DoorRules.State(facing, open != 0));
            Spec.True(!DoorRules.Contains(door, .5f, .5f, .5f), "Center of doorway is empty");
            int hits = 0;
            if (DoorRules.Contains(door, .5f, .5f, .05f)) hits++;
            if (DoorRules.Contains(door, .95f, .5f, .5f)) hits++;
            if (DoorRules.Contains(door, .5f, .5f, .95f)) hits++;
            if (DoorRules.Contains(door, .05f, .5f, .5f)) hits++;
            Spec.Equal(1, hits, "Only one edge contains the door panel");
            Spec.Equal((facing + (open != 0 ? 3 : 0)) & 3, DoorRules.Edge(door));
        }
    }
    private static void Remove()
    {
        foreach (Cell target in new[] { Foot, Foot.Up })
        {
            var world = Arena(); DoorRules.TryPlace(world, Foot, 2);
            world.Set(Foot + new Cell(1, 0, 0), Block.Planks);
            Spec.True(DoorRules.Remove(world, target));
            Spec.Equal(Block.Air, world.GetBlock(Foot)); Spec.Equal(Block.Air, world.GetBlock(Foot.Up));
            Spec.Equal(Block.Planks, world.GetBlock(Foot + new Cell(1, 0, 0)));
            Spec.True(!DoorRules.Remove(world, target), "Repeated break cannot yield another door");
        }
    }
    private static void Support()
    {
        var world = Arena(); DoorRules.TryPlace(world, Foot, 0); world.Set(Foot.Down, Block.Air);
        Spec.Equal(Block.Air, world.GetBlock(Foot)); Spec.Equal(Block.Air, world.GetBlock(Foot.Up));
    }
    private static void Reload()
    {
        var world = Arena(); DoorRules.TryPlace(world, Foot, 3); DoorRules.Toggle(world, Foot.Up);
        var reloaded = new World(world.Seed, world.Dimension); reloaded.ApplyEdits(world.Edits.ToArray());
        Spec.Equal(new Voxel(Block.Door, 7), reloaded.Get(Foot));
        Spec.Equal(new Voxel(Block.Door, 15), reloaded.Get(Foot.Up));
        DoorRules.Toggle(reloaded, Foot); Spec.Equal(new Voxel(Block.Door, 11), reloaded.Get(Foot.Up));
    }
    private static void Legacy()
    {
        var world = Arena(); world.Set(Foot, Block.Door); world.Set(Foot.Up, Block.Planks);
        var reloaded = new World(world.Seed, world.Dimension); reloaded.ApplyEdits(world.Edits.ToArray());
        Spec.Equal(Block.Door, reloaded.GetBlock(Foot)); Spec.Equal(Block.Planks, reloaded.GetBlock(Foot.Up));
        Spec.True(DoorRules.Toggle(world, Foot)); Spec.Equal(Block.Planks, world.GetBlock(Foot.Up));
        DoorRules.Remove(world, Foot); Spec.Equal(Block.Planks, world.GetBlock(Foot.Up));
    }
    private static void VillageEdits()
    {
        int y = Terrain.Surface(7, Dimension.Overworld, 40, 40);
        var foot = new Cell(29, y+1, 33);
        foreach (Cell edited in new[] { foot, foot.Up, foot.Down })
        foreach (bool generated in new[] { false, true })
        {
            var world = new World(7, Dimension.Overworld);
            if (generated) world.EnsureChunk(World.FloorDiv(foot.X, 16), World.FloorDiv(foot.Z, 16));
            world.ApplyEdits(new[] { new KeyValuePair<Cell,Voxel>(edited, new Voxel(Block.Air)) });
            Spec.Equal(Block.Air, world.GetBlock(foot));
            Spec.Equal(Block.Air, world.GetBlock(foot.Up));
        }
    }
}
