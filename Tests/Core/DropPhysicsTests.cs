using System;
using VoxelWilds.Core;

public static class DropPhysicsTests
{
    public static void Run()
    {
        Spec.Run("Falling items cannot tunnel into a floor at low frame rates", () =>
        {
            foreach (int fps in new[] { 10, 30, 60, 144 })
            {
                var world = new World(91, Dimension.Overworld); world.Set(new Cell(0, 80, 0), Block.Stone);
                float y = 89.7f, velocity = 0;
                for (int frame = 0; frame < fps * 3; frame++) y = DropPhysics.Fall(world, .5f, y, .5f, ref velocity, 1f / fps);
                Spec.True(Math.Abs(y - 81.14f) < .001f, "The item must rest above the floor, not inside it");
                Spec.Equal(0f, velocity);
            }
        });
        Spec.Run("Dropped items rest on the actual bed surface", () =>
        {
            var world = new World(91, Dimension.Overworld); world.Set(new Cell(0, 80, 0), Block.Bed);
            float velocity = -10;
            float y = DropPhysics.Fall(world, .5f, 81.5f, .5f, ref velocity, .1f);
            Spec.True(Math.Abs(y - (80 + BedRules.Height + DropPhysics.HalfHeight)) < .001f);
        });
        Spec.Run("An open door does not suspend a falling item above its whole cell", () =>
        {
            var world = new World(91, Dimension.Overworld);
            world.Set(new Cell(0, 80, 0), Block.Door, DoorRules.State(0, true));
            float velocity = -10;
            Spec.True(DropPhysics.Fall(world, .5f, 81.5f, .5f, ref velocity, .1f) < 81);
        });
        Spec.Run("Invalid item fall durations leave the position and velocity unchanged", () =>
        {
            var world = new World(91, Dimension.Overworld);
            foreach (float delta in new[] { 0, -1, float.NaN, float.PositiveInfinity })
            { float velocity = -2; Spec.Equal(90f, DropPhysics.Fall(world, .5f, 90, .5f, ref velocity, delta)); Spec.Equal(-2f, velocity); }
        });
    }
}
