using VoxelWilds.Core;

public static class ItemUseTests
{
    public static void Run()
    {
        Spec.Run("Stations, doors and beds take priority over held food and bows", () =>
        {
            foreach (var block in new[] { Block.Workbench, Block.Chest, Block.Furnace, Block.Door, Block.Bed, Block.BedWestHead })
            { Spec.True(ItemUseRules.BlockTakesPriority(block, false)); Spec.True(!ItemUseRules.BlockTakesPriority(block, true)); }
            Spec.True(!ItemUseRules.BlockTakesPriority(Block.Stone, false));
        });
        Spec.Run("Mining and melee do not damage unrelated held equipment", () =>
        {
            foreach (int item in new[] { Items.Shield, Items.Bow, Items.FlintSteel, Items.IronHelmet, Items.Berries, (int)Block.Log, 0 })
            { Spec.Equal(0, ItemUseRules.MiningWear(item)); Spec.Equal(0, ItemUseRules.AttackWear(item)); }
            Spec.Equal(1, ItemUseRules.MiningWear(Items.IronPickaxe)); Spec.Equal(2, ItemUseRules.AttackWear(Items.IronPickaxe));
            Spec.Equal(2, ItemUseRules.MiningWear(Items.IronSword)); Spec.Equal(1, ItemUseRules.AttackWear(Items.IronSword));
        });
    }
}
