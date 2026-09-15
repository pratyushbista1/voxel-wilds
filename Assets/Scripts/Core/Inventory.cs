using System;
using System.Collections.Generic;

namespace VoxelWilds.Core
{
    [Serializable]
    public sealed class Inventory
    {
        public ItemStack[] Slots = new ItemStack[36];
        public ItemStack[] Armor = new ItemStack[4];
        public ItemStack Offhand;
        public int Selected;
        public ItemStack Held => Selected >= 0 && Selected < Math.Min(9, Slots.Length) ? Slots[Selected] : null;
        public static bool Stackable(ItemStack a, ItemStack b) => a != null && b != null && !a.Empty && !b.Empty && a.Id == b.Id && Items.MaxStack(a.Id) > 1;

        public int CapacityFor(int id)
        {
            int cap = Items.MaxStack(id), free = 0;
            if (cap == 0) return 0;
            foreach (var stack in Slots)
                if (stack == null || stack.Empty) free += cap;
                else if (stack.Id == id && cap > 1) free += Math.Max(0, cap - stack.Count);
            return free;
        }
        public bool CanAdd(int id, int count) => count >= 0 && CapacityFor(id) >= count;
        public int Add(int id, int count, int durability = -1)
        {
            if (count <= 0) return 0;
            int cap = Items.MaxStack(id);
            if (cap == 0 || Items.Durability(id) > 0 && durability == 0) return count;
            if (cap > 1)
                foreach (var stack in Slots)
                {
                    if (stack == null || stack.Empty || stack.Id != id) continue;
                    int moved = Math.Min(count, Math.Max(0, cap - stack.Count));
                    stack.Count += moved;
                    count -= moved;
                    if (count == 0) return 0;
                }
            for (int i = 0; i < Slots.Length && count > 0; i++)
            {
                if (Slots[i] != null && !Slots[i].Empty) continue;
                int moved = Math.Min(count, cap);
                Slots[i] = new ItemStack(id, moved, durability);
                count -= moved;
            }
            return count;
        }
        public int Count(int id)
        {
            if (id <= 0) return 0;
            int total = 0;
            foreach (var stack in Slots) if (stack != null && stack.Id == id && !stack.Empty) total += stack.Count;
            return total;
        }
        public bool Remove(int id, int count)
        {
            if (count < 0 || Count(id) < count) return false;
            for (int i = 0; i < Slots.Length && count > 0; i++)
            {
                var stack = Slots[i];
                if (stack == null || stack.Empty || stack.Id != id) continue;
                int moved = Math.Min(count, stack.Count);
                stack.Count -= moved;
                count -= moved;
                if (stack.Empty) Slots[i] = null;
            }
            return true;
        }
        public bool WearSelected(int amount)
        {
            var held = Held;
            if (held == null || held.Empty || Items.Durability(held.Id) == 0 || amount <= 0) return false;
            held.Durability -= amount;
            if (held.Durability > 0) return false;
            Slots[Selected] = null;
            return true;
        }
        public void Transfer(int from, int to, bool half = false)
        {
            if (from == to || from < 0 || from >= Slots.Length || to < 0 || to >= Slots.Length) return;
            var source = Slots[from];
            var target = Slots[to];
            if (source == null || source.Empty) return;
            if (target != null && target.Empty) target = Slots[to] = null;
            int moved = half ? (source.Count + 1) / 2 : source.Count;
            if (target == null)
            {
                moved = Math.Min(moved, Items.MaxStack(source.Id));
                Slots[to] = new ItemStack(source.Id, moved, source.Durability);
            }
            else if (Stackable(source, target))
            {
                moved = Math.Min(moved, Math.Max(0, Items.MaxStack(target.Id) - target.Count));
                target.Count += moved;
            }
            else
            {
                if (!half) { Slots[from] = target; Slots[to] = source; }
                return;
            }
            source.Count -= moved;
            if (source.Empty) Slots[from] = null;
        }
        public bool QuickMove(int from)
        {
            if (from < 0 || from >= Slots.Length || Slots[from] == null || Slots[from].Empty) return false;
            int before = Slots[from].Count;
            int first = from < 9 ? 9 : 0;
            int end = from < 9 ? Slots.Length : Math.Min(9, Slots.Length);
            for (int pass = 0; pass < 2; pass++)
                for (int to = first; to < end; to++)
                {
                    if (Slots[from] == null || Slots[from].Empty) return true;
                    bool empty = Slots[to] == null || Slots[to].Empty;
                    if (pass == 0 ? !Stackable(Slots[from], Slots[to]) : !empty) continue;
                    Transfer(from, to);
                }
            return Slots[from] == null || Slots[from].Count != before;
        }
        public List<ItemStack> TakeAll()
        {
            var result = new List<ItemStack>();
            Drain(Slots, result);
            Drain(Armor, result);
            if (Offhand != null && !Offhand.Empty) result.Add(Offhand.Clone());
            Offhand = null;
            return result;
        }
        static void Drain(ItemStack[] stacks, List<ItemStack> result)
        {
            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i] != null && !stacks[i].Empty) result.Add(stacks[i].Clone());
                stacks[i] = null;
            }
        }
    }
}
