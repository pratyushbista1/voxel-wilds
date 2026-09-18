using System;
using System.Collections.Generic;

namespace VoxelWilds.Core
{
    public static class InventoryTransfer
    {
        public static void Click(ref ItemStack cursor, ref ItemStack slot, bool right, bool output = false, Func<ItemStack, bool> accepts = null)
        {
            if (cursor != null && cursor.Empty) cursor = null;
            if (slot != null && slot.Empty) slot = null;
            if (cursor == null)
            {
                if (slot == null) return;
                int amount = right ? (slot.Count + 1) / 2 : slot.Count;
                cursor = new ItemStack(slot.Id, amount, slot.Durability);
                slot.Count -= amount;
            }
            else if (output)
            {
                if (!Inventory.Stackable(cursor, slot)) return;
                int amount = Math.Min(right ? 1 : slot.Count, Items.MaxStack(cursor.Id) - cursor.Count);
                cursor.Count += amount;
                slot.Count -= amount;
            }
            else if (accepts == null || accepts(cursor))
            {
                if (slot == null || Inventory.Stackable(cursor, slot))
                {
                    int amount = Math.Min(right ? 1 : cursor.Count, Items.MaxStack(cursor.Id) - (slot?.Count ?? 0));
                    if (slot == null) slot = new ItemStack(cursor.Id, amount, cursor.Durability);
                    else slot.Count += amount;
                    cursor.Count -= amount;
                }
                else if (!right)
                {
                    ItemStack previous = slot;
                    slot = cursor;
                    cursor = previous;
                }
            }
            if (slot != null && slot.Empty) slot = null;
            if (cursor != null && cursor.Empty) cursor = null;
        }

        public static bool CanDistribute(ItemStack cursor, ItemStack slot) => cursor != null && !cursor.Empty &&
            (slot == null || slot.Empty || Inventory.Stackable(cursor, slot) && slot.Count < Items.MaxStack(slot.Id));

        public static int DistributionAmount(ItemStack cursor, ItemStack slot, int targetCount, bool oneEach)
        {
            if (targetCount <= 0 || !CanDistribute(cursor, slot)) return 0;
            int share = oneEach ? 1 : cursor.Count / targetCount;
            return Math.Min(share, Items.MaxStack(cursor.Id) - (slot == null || slot.Empty ? 0 : slot.Count));
        }

        public static void Distribute(ref ItemStack cursor, IList<ItemStack> targets, bool oneEach)
        {
            if (cursor == null || cursor.Empty || targets == null || targets.Count == 0) return;
            int remaining = cursor.Count;
            for (int i = 0; i < targets.Count; i++)
            {
                int amount = Math.Min(remaining, DistributionAmount(cursor, targets[i], targets.Count, oneEach));
                if (amount <= 0) continue;
                if (targets[i] == null || targets[i].Empty) targets[i] = new ItemStack(cursor.Id, amount, cursor.Durability);
                else targets[i].Count += amount;
                remaining -= amount;
            }
            cursor.Count = remaining;
            if (cursor.Empty) cursor = null;
        }

        public static bool SwapHotbar(ref ItemStack slot, ref ItemStack hotbar, bool output = false, Func<ItemStack, bool> accepts = null)
        {
            if (output)
            {
                if (slot == null || slot.Empty || hotbar != null && !hotbar.Empty) return false;
            }
            else if (hotbar != null && !hotbar.Empty && accepts != null && !accepts(hotbar)) return false;
            ItemStack previous = slot;
            slot = hotbar;
            hotbar = previous;
            return true;
        }
    }
}
