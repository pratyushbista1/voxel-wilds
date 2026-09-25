using System.Linq;
using VoxelWilds.Core;

public static class PortalTests
{
    public static void Run()
    {
        Spec.Run("Nether frames accept both axes and all valid rectangular sizes", Dimensions);
        Spec.Run("Nether frame corners are optional and decorative corners remain untouched", Corners);
        Spec.Run("Nether ignition rejects undersized oversized obstructed and incomplete frames", Invalid);
        Spec.Run("Nether ignition does not spend another activation on an already lit frame", Relight);
        Spec.Run("Nether portals cannot be activated in the End", End);
        Spec.Run("Breaking any required frame edge collapses the entire large portal", Collapse);
        Spec.Run("Removing an optional corner leaves the portal active", KeepCorner);
        Spec.Run("Blocking a portal interior collapses only that connected portal", Obstruction);
        Spec.Run("Shared obsidian walls preserve the adjacent valid portal", SharedFrame);
        Spec.Run("Saved portal orientation persists and invalid saved frames collapse", Persistence);
        Spec.Run("Nearby existing portals are reused without accepting unlit or broken frames", Linking);
    }

    private static PortalFrame Build(World world, int width = 2, int height = 3, bool x = true, Cell? origin = null)
    {
        var frame = new PortalFrame(origin ?? new Cell(4, 62, 4), width, height, x);
        for (int y = 0; y <= height + 1; y++) for (int u = 0; u <= width + 1; u++)
            world.Set(frame.At(u, y), u == 0 || u == width + 1 || y == 0 || y == height + 1 ? Block.Obsidian : Block.Air);
        return frame;
    }
    private static void Dimensions()
    {
        foreach (bool x in new[] { true, false })
        foreach (int width in new[] { 2, 3, 7, 21 })
        foreach (int height in new[] { 3, 5, 21 })
        {
            var world = new World(1, Dimension.Overworld);
            var expected = Build(world, width, height, x);
            Spec.True(PortalRules.TryIgnite(world, expected.At(width, height), out var actual));
            Spec.Equal(expected.Origin, actual.Origin); Spec.Equal(width, actual.Width); Spec.Equal(height, actual.Height);
            Spec.Equal(x, actual.AlongX); Spec.True(PortalRules.IsLit(world, actual));
            Spec.Equal(width * height, world.Edits.Count(e => e.Value.Id == actual.Portal));
        }
    }
    private static void Corners()
    {
        foreach (Block corner in new[] { Block.Air, Block.Planks, Block.GoldBlock })
        {
            var world = new World(1, Dimension.Overworld); var frame = Build(world);
            foreach (Cell p in new[] { frame.At(0, 0), frame.At(3, 0), frame.At(0, 4), frame.At(3, 4) }) world.Set(p, corner);
            Spec.True(PortalRules.TryIgnite(world, frame.At(1, 1), out _));
            Spec.Equal(corner, world.GetBlock(frame.Origin));
        }
    }
    private static void Invalid()
    {
        foreach (var size in new[] { new[] { 1, 3 }, new[] { 2, 2 }, new[] { 22, 3 }, new[] { 2, 22 } })
        {
            var world = new World(1, Dimension.Overworld); var frame = Build(world, size[0], size[1]);
            Spec.True(!PortalRules.TryIgnite(world, frame.At(1, 1), out _));
            Spec.True(!world.Edits.Any(e => PortalRules.IsPortal(e.Value.Id)));
        }
        foreach (Block obstruction in new[] { Block.Stone, Block.Water, Block.Torch, Block.PortalZ })
        {
            var world = new World(1, Dimension.Overworld); var frame = Build(world, 6, 7);
            world.Set(frame.At(4, 4), obstruction);
            Spec.True(!PortalRules.TryIgnite(world, frame.At(1, 1), out _));
        }
        foreach (Cell offset in new[] { new Cell(0, 2, 0), new Cell(3, 2, 0), new Cell(1, 0, 0), new Cell(2, 4, 0) })
        {
            var world = new World(1, Dimension.Overworld); var frame = Build(world);
            world.Set(frame.Origin + offset, Block.Air);
            Spec.True(!PortalRules.TryIgnite(world, frame.At(1, 1), out _));
        }
    }
    private static void Relight()
    {
        var world = new World(1, Dimension.Overworld); var frame = Build(world);
        Spec.True(PortalRules.TryIgnite(world, frame.At(1, 1), out _));
        Spec.True(!PortalRules.TryIgnite(world, frame.At(2, 2), out _));
    }
    private static void End()
    {
        var world = new World(1, Dimension.End); var frame = Build(world);
        Spec.True(!PortalRules.TryIgnite(world, frame.At(1, 1), out _));
    }
    private static void Collapse()
    {
        foreach (Cell edge in new[] { new Cell(0, 10, 0), new Cell(22, 10, 0), new Cell(10, 0, 0), new Cell(10, 22, 0) })
        {
            var world = new World(1, Dimension.Overworld); var frame = Build(world, 21, 21);
            using var simulation = new PortalSimulation(world);
            PortalRules.TryIgnite(world, frame.At(1, 1), out _); simulation.Tick();
            Spec.True(PortalRules.IsLit(world, frame));
            world.Set(frame.Origin + edge, Block.Air); simulation.Tick();
            Spec.True(!frame.Interior.Any(p => PortalRules.IsPortal(world.GetBlock(p))));
        }
    }
    private static void KeepCorner()
    {
        var world = new World(1, Dimension.Overworld); var frame = Build(world);
        using var simulation = new PortalSimulation(world);
        PortalRules.TryIgnite(world, frame.At(1, 1), out _); simulation.Tick();
        world.Set(frame.Origin, Block.Air); simulation.Tick(); Spec.True(PortalRules.IsLit(world, frame));
    }
    private static void Obstruction()
    {
        var world = new World(1, Dimension.Overworld); var frame = Build(world, 8, 8);
        using var simulation = new PortalSimulation(world);
        PortalRules.TryIgnite(world, frame.At(1, 1), out _); simulation.Tick();
        world.Set(frame.At(4, 4), Block.Stone); simulation.Tick();
        Spec.Equal(Block.Stone, world.GetBlock(frame.At(4, 4)));
        Spec.True(!frame.Interior.Any(p => PortalRules.IsPortal(world.GetBlock(p))));
    }
    private static void SharedFrame()
    {
        var world = new World(1, Dimension.Overworld); var first = Build(world);
        var second = Build(world, origin: first.At(3, 0));
        using var simulation = new PortalSimulation(world);
        PortalRules.TryIgnite(world, first.At(1, 1), out _); PortalRules.TryIgnite(world, second.At(1, 1), out _); simulation.Tick();
        world.Set(first.At(0, 2), Block.Air); simulation.Tick();
        Spec.True(!PortalRules.IsLit(world, first)); Spec.True(PortalRules.IsLit(world, second));
    }
    private static void Persistence()
    {
        var original = new World(1, Dimension.Nether); var frame = Build(original, 5, 6, false);
        PortalRules.TryIgnite(original, frame.At(2, 3), out _);
        var world = new World(1, Dimension.Nether); world.ApplyEdits(original.Edits);
        using var simulation = new PortalSimulation(world); simulation.Tick();
        Spec.True(PortalRules.IsLit(world, frame)); Spec.Equal(Block.PortalZ, world.GetBlock(frame.At(1, 1)));
        world.Set(frame.At(0, 2), Block.Air);
        var broken = new World(1, Dimension.Nether); broken.ApplyEdits(world.Edits);
        using var check = new PortalSimulation(broken); check.Tick();
        Spec.True(!frame.Interior.Any(p => PortalRules.IsPortal(broken.GetBlock(p))));
    }
    private static void Linking()
    {
        var world = new World(1, Dimension.Nether); var frame = Build(world, origin: new Cell(-8, 62, -8));
        Spec.True(!PortalRules.TryNearest(world, new Cell(0, 63, 0), 16, out _));
        PortalRules.TryIgnite(world, frame.At(1, 1), out _);
        Spec.True(PortalRules.TryNearest(world, new Cell(0, 63, 0), 16, out var found)); Spec.Equal(frame.Origin, found.Origin);
        Spec.True(!PortalRules.TryNearest(world, new Cell(100, 63, 100), 16, out _));
        world.Set(frame.At(0, 1), Block.Air);
        Spec.True(!PortalRules.TryNearest(world, new Cell(0, 63, 0), 16, out _));
    }
}
