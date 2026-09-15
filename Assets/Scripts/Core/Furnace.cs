using System;

namespace VoxelWilds.Core
{
    [Serializable]
    public sealed class Furnace
    {
        public const float CookSeconds = 10;
        public ItemStack Input;
        public ItemStack Fuel;
        public ItemStack Output;
        public float BurnRemaining;
        public float BurnTotal;
        public float CookProgress;
        public int InputId;

        public static int SmeltingResult(int id)
        {
            switch (id)
            {
                case (int)Block.Sand: return (int)Block.Glass;
                case Items.RawIron: case (int)Block.IronOre: return Items.IronIngot;
                case Items.ClayBall: return Items.Brick; case (int)Block.Cobble: return (int)Block.Stone;
                case (int)Block.Log: return Items.Coal;
                case Items.RawMutton: return Items.CookedMutton; case Items.RawBeef: return Items.Steak;
                case Items.RawPorkchop: return Items.CookedPorkchop; case Items.RawChicken: return Items.CookedChicken;
                case (int)Block.NetherGold: return Items.GoldIngot; case (int)Block.Netherrack: return Items.NetherBrick;
                default: return 0;
            }
        }
        public static float FuelSeconds(int id)
        {
            switch (id)
            {
                case Items.Coal: return 80; case Items.LavaBucket: return 1000; case Items.BlazeRod: return 120;
                case Items.Stick: return 5; case Items.WoodenPickaxe: case Items.Bow: return 10;
                case (int)Block.Log: case (int)Block.Planks: case (int)Block.Workbench:
                case (int)Block.Chest: case (int)Block.Door: return 15; case (int)Block.Wool: return 5;
                default: return 0;
            }
        }
        public bool CanCook => Input != null && !Input.Empty && SmeltingResult(Input.Id) != 0 &&
            (Output == null || Output.Empty || Output.Id == SmeltingResult(Input.Id) && Output.Count < Items.MaxStack(Output.Id));

        public int Tick(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return 0;
            if (Input != null && Input.Empty) Input = null;
            if (Fuel != null && Fuel.Empty) Fuel = null;
            if (Output != null && Output.Empty) Output = null;
            int input = Input?.Id ?? 0;
            if (InputId != input) { InputId = input; CookProgress = 0; }
            if (float.IsNaN(BurnRemaining) || float.IsInfinity(BurnRemaining)) BurnRemaining = 0;
            BurnRemaining = Math.Max(0, BurnRemaining);
            if (float.IsNaN(CookProgress) || float.IsInfinity(CookProgress)) CookProgress = 0;
            CookProgress = Math.Max(0, Math.Min(CookSeconds, CookProgress));
            int cooked = 0;
            double remaining = seconds;
            while (remaining > 0.000001)
            {
                if (!CanCook)
                {
                    BurnRemaining = Math.Max(0, BurnRemaining - (float)remaining);
                    CookProgress = 0;
                    break;
                }
                if (BurnRemaining <= 0.000001f)
                {
                    if (!Ignite())
                    {
                        CookProgress = Math.Max(0, CookProgress - (float)remaining * 2);
                        break;
                    }
                }
                double elapsed = Math.Min(remaining, Math.Min(BurnRemaining, CookSeconds - CookProgress));
                CookProgress += (float)elapsed;
                BurnRemaining = Math.Max(0, BurnRemaining - (float)elapsed);
                remaining -= elapsed;
                if (CookProgress >= CookSeconds - 0.00001f)
                {
                    int result = SmeltingResult(Input.Id);
                    if (Output == null) Output = new ItemStack(result);
                    else Output.Count++;
                    Input.Count--;
                    CookProgress = 0;
                    cooked++;
                    if (Input.Empty) { Input = null; InputId = 0; }
                }
            }
            return cooked;
        }
        bool Ignite()
        {
            if (Fuel == null || Fuel.Empty) return false;
            float duration = FuelSeconds(Fuel.Id);
            if (duration <= 0 || Fuel.Id == Items.LavaBucket && Fuel.Count != 1) return false;
            bool bucket = Fuel.Id == Items.LavaBucket;
            Fuel.Count--;
            if (Fuel.Empty) Fuel = bucket ? new ItemStack(Items.EmptyBucket) : null;
            BurnRemaining = BurnTotal = duration;
            return true;
        }
    }
}
