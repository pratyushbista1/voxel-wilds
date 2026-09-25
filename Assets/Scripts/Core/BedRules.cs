namespace VoxelWilds.Core
{
    public static class BedRules
    {
        public const float Height = .5625f;
        public static int Facing(Block bed) => ((int)bed - (int)Block.Bed) / 2;
        public static bool IsHead(Block bed) => Blocks.IsBed(bed) && (((int)bed - (int)Block.Bed) & 1) != 0;
        public static Block Foot(int facing) => (Block)((int)Block.Bed + (facing & 3) * 2);
        public static Cell Direction(int facing)
        {
            switch (facing & 3)
            {
                case 0: return new Cell(0, 0, 1);
                case 1: return new Cell(1, 0, 0);
                case 2: return new Cell(0, 0, -1);
                default: return new Cell(-1, 0, 0);
            }
        }
        public static Cell Partner(Cell position, Block bed)
        {
            Cell offset = Direction(Facing(bed));
            return position + (IsHead(bed) ? new Cell(-offset.X, 0, -offset.Z) : offset);
        }
        public static bool Paired(Block first, Block second)
            => Blocks.IsBed(first) && Blocks.IsBed(second) && Facing(first) == Facing(second)
                && IsHead(first) != IsHead(second);
        public static bool TryPlace(World world, Cell foot, int facing)
        {
            Cell head = foot + Direction(facing);
            if (foot.Y <= 0 || foot.Y >= World.Height - 1 || !DoorRules.Supports(world.GetBlock(foot.Down))
                || !DoorRules.Supports(world.GetBlock(head.Down)) || !Blocks.IsReplaceable(world.GetBlock(foot))
                || !Blocks.IsReplaceable(world.GetBlock(head))) return false;
            Block block = Foot(facing);
            world.Set(foot, block);
            world.Set(head, (Block)((int)block + 1));
            return true;
        }
        public static bool IsComplete(World world, Cell position)
        {
            Block block = world.GetBlock(position);
            return Blocks.IsBed(block) && Paired(block, world.GetBlock(Partner(position, block)));
        }
    }
}
