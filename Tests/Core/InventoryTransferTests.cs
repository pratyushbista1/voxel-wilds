using System.Linq;
using VoxelWilds.Core;

public static class InventoryTransferTests
{
    public static void Run()
    {
        Spec.Run("inventory right click takes the larger half and places single items", () =>
        {
            ItemStack cursor = null, slot = new ItemStack(Items.Coal, 9);
            InventoryTransfer.Click(ref cursor, ref slot, true);
            Spec.Equal(5, cursor.Count); Spec.Equal(4, slot.Count);
            InventoryTransfer.Click(ref cursor, ref slot, true);
            Spec.Equal(4, cursor.Count); Spec.Equal(5, slot.Count);
        });
        Spec.Run("left drag distributes equal shares and keeps remainder on cursor", () =>
        {
            ItemStack cursor = new ItemStack(Items.Coal, 20);
            var slots = new ItemStack[3];
            InventoryTransfer.Distribute(ref cursor, slots, false);
            Spec.True(slots.All(s => s.Count == 6)); Spec.Equal(2, cursor.Count);
            Spec.True(!ReferenceEquals(slots[0], slots[1]));
        });
        Spec.Run("right drag places one into each eligible slot", () =>
        {
            ItemStack cursor = new ItemStack(Items.Coal, 10);
            var slots = new[] { new ItemStack(Items.Coal, 63), null, new ItemStack(Items.Coal, 5) };
            InventoryTransfer.Distribute(ref cursor, slots, true);
            Spec.Equal(64, slots[0].Count); Spec.Equal(1, slots[1].Count); Spec.Equal(6, slots[2].Count); Spec.Equal(7, cursor.Count);
        });
        Spec.Run("drag respects stack limits and keeps overflow", () =>
        {
            ItemStack cursor = new ItemStack(Items.EmptyBucket, 16);
            var slots = new[] { new ItemStack(Items.EmptyBucket, 15), null };
            InventoryTransfer.Distribute(ref cursor, slots, false);
            Spec.Equal(16, slots[0].Count); Spec.Equal(8, slots[1].Count); Spec.Equal(7, cursor.Count);
            Spec.True(!InventoryTransfer.CanDistribute(cursor, slots[0]));
            Spec.True(!InventoryTransfer.CanDistribute(cursor, new ItemStack(Items.Coal)));
        });
        Spec.Run("inventory swaps preserve tool durability and right click cannot swap", () =>
        {
            ItemStack cursor = new ItemStack(Items.IronPickaxe, 1, 37), slot = new ItemStack(Items.IronSword, 1, 22);
            InventoryTransfer.Click(ref cursor, ref slot, true);
            Spec.Equal(37, cursor.Durability);
            InventoryTransfer.Click(ref cursor, ref slot, false);
            Spec.Equal(22, cursor.Durability); Spec.Equal(37, slot.Durability);
        });
        Spec.Run("output slots never accept cursor items", () =>
        {
            ItemStack cursor = new ItemStack(Items.Coal, 12), slot = null;
            InventoryTransfer.Click(ref cursor, ref slot, false, true);
            Spec.Equal<ItemStack>(null, slot); Spec.Equal(12, cursor.Count);
            slot = new ItemStack(Items.Coal, 60);
            InventoryTransfer.Click(ref cursor, ref slot, false, true);
            Spec.Equal(64, cursor.Count); Spec.Equal(8, slot.Count);
        });
        Spec.Run("armor rejects invalid cursor and number key transfers", () =>
        {
            ItemStack cursor = new ItemStack(Items.Coal), slot = null;
            InventoryTransfer.Click(ref cursor, ref slot, false, false, s => Items.ArmorSlot(s.Id) == 0);
            Spec.Equal<ItemStack>(null, slot);
            Spec.True(!InventoryTransfer.SwapHotbar(ref slot, ref cursor, false, s => Items.ArmorSlot(s.Id) == 0));
            cursor = new ItemStack(Items.IronHelmet, 1, 65);
            Spec.True(InventoryTransfer.SwapHotbar(ref slot, ref cursor, false, s => Items.ArmorSlot(s.Id) == 0));
            Spec.Equal(65, slot.Durability); Spec.Equal<ItemStack>(null, cursor);
        });
        Spec.Run("output hotbar transfer only uses an empty destination", () =>
        {
            ItemStack slot = new ItemStack(Items.IronIngot, 5), hotbar = new ItemStack(Items.Coal, 20);
            Spec.True(!InventoryTransfer.SwapHotbar(ref slot, ref hotbar, true));
            Spec.Equal(5, slot.Count); Spec.Equal(20, hotbar.Count);
            hotbar = null;
            Spec.True(InventoryTransfer.SwapHotbar(ref slot, ref hotbar, true));
            Spec.Equal<ItemStack>(null, slot); Spec.Equal(5, hotbar.Count);
        });
    }
}
