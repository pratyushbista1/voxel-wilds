namespace VoxelWilds.Core
{
    public static class DoorRules
    {
        public const float Thickness = .125f;
        public static byte State(int facing, bool open = false, bool upper = false)
            => (byte)((facing & 3) | (open ? 4 : 0) | (upper ? 8 : 0));
        public static int Facing(Voxel door) => door.Level & 3;
        public static bool IsOpen(Voxel door) => door.Id == Block.Door && (door.Level & 4) != 0;
        public static bool IsUpper(Voxel door) => door.Id == Block.Door && (door.Level & 8) != 0;
        public static int Edge(Voxel door) => (Facing(door) + (IsOpen(door) ? 3 : 0)) & 3;
        public static bool Supports(Block block)
            => Blocks.IsSolid(block) && block != Block.Door && !Blocks.IsBed(block) && block != Block.Farmland;
        public static bool CanPlace(World world, Cell foot)
            => foot.Y > 0 && foot.Y < World.Height - 1 && Supports(world.GetBlock(foot.Down))
                && Blocks.IsReplaceable(world.GetBlock(foot)) && Blocks.IsReplaceable(world.GetBlock(foot.Up));
        public static bool TryPlace(World world, Cell foot, int facing)
        {
            if (!CanPlace(world, foot)) return false;
            world.Set(foot, Block.Door, State(facing));
            world.Set(foot.Up, Block.Door, State(facing, false, true));
            return true;
        }
        public static bool Paired(Voxel a, Voxel b)
            => a.Id == Block.Door && b.Id == Block.Door && IsUpper(a) != IsUpper(b) && Facing(a) == Facing(b);
        public static Cell Partner(Cell position, Voxel door) => IsUpper(door) ? position.Down : position.Up;
        public static bool Toggle(World world, Cell position)
        {
            Voxel door = world.Get(position);
            if (door.Id != Block.Door) return false;
            Cell partner = Partner(position, door);
            Voxel other = world.Get(partner);
            bool open = !IsOpen(door);
            world.Set(position, Block.Door, State(Facing(door), open, IsUpper(door)));
            if (Paired(door, other)) world.Set(partner, Block.Door, State(Facing(other), open, IsUpper(other)));
            return true;
        }
        public static bool Remove(World world, Cell position)
        {
            if (world.GetBlock(position) != Block.Door) return false;
            world.Set(position, Block.Air);
            return true;
        }
        public static bool Contains(Voxel door, float x, float y, float z)
        {
            if (door.Id != Block.Door || y < 0 || y > 1 || x < 0 || x > 1 || z < 0 || z > 1) return false;
            switch (Edge(door))
            {
                case 0: return z <= Thickness;
                case 1: return x >= 1 - Thickness;
                case 2: return z >= 1 - Thickness;
                default: return x <= Thickness;
            }
        }
    }
}
