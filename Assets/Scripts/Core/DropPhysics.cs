using System;

namespace VoxelWilds.Core
{
    public static class DropPhysics
    {
        public const float HalfHeight = .14f;
        public static float Fall(World world, float x, float y, float z, ref float velocity, float delta)
        {
            if (delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return y;
            delta = Math.Min(delta, .1f);
            velocity = Math.Max(-10, velocity - 16 * delta);
            float next = y + velocity * delta;
            int cx = (int)Math.Floor(x), cz = (int)Math.Floor(z);
            int first = (int)Math.Floor(y - HalfHeight), last = (int)Math.Floor(next - HalfHeight);
            for (int cy = first; cy >= last; cy--)
            {
                Cell cell = new Cell(cx, cy, cz);
                if (!world.Solid(cell)) continue;
                float surface = cy + (Blocks.IsBed(world.GetBlock(cell)) ? BedRules.Height : 1) + HalfHeight;
                if (next > surface || y < surface - .001f) continue;
                velocity = 0;
                return surface;
            }
            return next;
        }
    }
}
