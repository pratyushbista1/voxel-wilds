namespace VoxelWilds.Core
{
    public static class ItemUseRules
    {
        public static bool BlockTakesPriority(Block block, bool sneaking)
            => !sneaking && (block == Block.Workbench || block == Block.Chest || block == Block.Furnace
                || block == Block.Door || Blocks.IsBed(block));
        public static int MiningWear(int item)
        {
            if (item == Items.IronSword || item == Items.CrystalSword) return 2;
            return Items.MiningTier(item) > 0 || item == Items.IronAxe || item == Items.IronShovel || item == Items.IronHoe ? 1 : 0;
        }
        public static int AttackWear(int item)
        {
            if (item == Items.IronSword || item == Items.CrystalSword) return 1;
            return Items.MiningTier(item) > 0 || item == Items.IronAxe || item == Items.IronShovel || item == Items.IronHoe ? 2 : 0;
        }
    }
}
