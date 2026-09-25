using System.Linq;
using VoxelWilds.Core;

public static class BedTests
{
    public static void Run()
    {
        Spec.Run("Beds place with matching halves in all four directions", () =>
        {
            for (int facing = 0; facing < 4; facing++)
            {
                var world = Fixture(); var foot = new Cell(0, 80, 0);
                Spec.True(BedRules.TryPlace(world, foot, facing));
                Cell head = foot + BedRules.Direction(facing);
                Spec.Equal(BedRules.Foot(facing), world.GetBlock(foot));
                Spec.True(BedRules.IsHead(world.GetBlock(head)));
                Spec.True(BedRules.IsComplete(world, foot) && BedRules.IsComplete(world, head));
                Spec.Equal(foot, BedRules.Partner(head, world.GetBlock(head)));
            }
        });
        Spec.Run("Bed placement is atomic when the head is blocked or unsupported", () =>
        {
            var world = Fixture(); var foot = new Cell(0, 80, 0); var head = foot + BedRules.Direction(1);
            world.Set(head, Block.Stone);
            Spec.True(!BedRules.TryPlace(world, foot, 1)); Spec.Equal(Block.Air, world.GetBlock(foot));
            world.Set(head, Block.Air); world.Set(head.Down, Block.Air);
            Spec.True(!BedRules.TryPlace(world, foot, 1)); Spec.Equal(Block.Air, world.GetBlock(foot));
        });
        Spec.Run("Breaking either bed half removes only its own matching partner", () =>
        {
            for (int facing = 0; facing < 4; facing++) for (int half = 0; half < 2; half++)
            {
                var world = Fixture(); var foot = new Cell(0, 80, 0); var head = foot + BedRules.Direction(facing);
                BedRules.TryPlace(world, foot, facing);
                world.Set(half == 0 ? foot : head, Block.Air);
                Spec.Equal(Block.Air, world.GetBlock(foot)); Spec.Equal(Block.Air, world.GetBlock(head));
            }
            var neighbor = Fixture(); var origin = new Cell(0, 80, 0);
            neighbor.Set(origin, Block.Bed); neighbor.Set(origin + BedRules.Direction(0), Block.Stone);
            neighbor.Set(origin, Block.Air);
            Spec.Equal(Block.Stone, neighbor.GetBlock(origin + BedRules.Direction(0)));
        });
        Spec.Run("Bed orientation and removal survive saved world edits", () =>
        {
            var world = Fixture(); var foot = new Cell(0, 80, 0);
            BedRules.TryPlace(world, foot, 3);
            var restored = new World(314, Dimension.Overworld); restored.ApplyEdits(world.Edits.ToArray());
            Spec.True(BedRules.IsComplete(restored, foot)); Spec.Equal(Block.BedWest, restored.GetBlock(foot));
            restored.Set(BedRules.Partner(foot, restored.GetBlock(foot)), Block.Air);
            var again = new World(314, Dimension.Overworld); again.ApplyEdits(restored.Edits.ToArray());
            Spec.Equal(Block.Air, again.GetBlock(foot));
        });
        Spec.Run("Incomplete beds cannot become a valid respawn point", () =>
        {
            var world = Fixture(); var foot = new Cell(0, 80, 0);
            world.Set(foot, Block.Bed);
            Spec.True(!BedRules.IsComplete(world, foot));
        });
    }
    private static World Fixture()
    {
        var world = new World(314, Dimension.Overworld);
        for (int z = -2; z <= 2; z++) for (int x = -2; x <= 2; x++)
        { world.Set(new Cell(x, 79, z), Block.Stone); world.Set(new Cell(x, 80, z), Block.Air); }
        return world;
    }
}
