using System;

namespace VoxelWilds.Core
{
    public static class MiningRules
    {
        public const float TickDuration = .05f;
        public const float HoldDelay = .25f;

        public static bool CanBreak(Block block, int tool, bool creative)
        {
            if (block == Block.Air || Blocks.IsFluid(block) || block == Block.PortalX
                || block == Block.PortalZ || block == Block.EndPortal) return false;
            if (creative) return tool != Items.IronSword && tool != Items.CrystalSword;
            return !float.IsInfinity(Blocks.Hardness(block));
        }

        public static double DamagePerTick(Block block, int tool, bool grounded, bool headSubmerged)
        {
            if (!CanBreak(block, tool, false)) return 0;
            double hardness = Blocks.Hardness(block);
            if (hardness <= 0) return 1;
            double speed = Items.MiningSpeed(tool, block);
            if (!grounded) speed /= 5;
            if (headSubmerged) speed /= 5;
            return speed / hardness / (Items.CanHarvest(tool, block) ? 30 : 100);
        }

        public static int RequiredTicks(Block block, int tool, bool grounded = true, bool headSubmerged = false)
        {
            double damage = DamagePerTick(block, tool, grounded, headSubmerged);
            return damage <= 0 ? int.MaxValue : Math.Max(1, (int)Math.Ceiling(1 / damage - .000001));
        }

        public static int CrackStage(float progress)
            => float.IsNaN(progress) || progress <= 0 ? -1 : progress >= 1 ? 9 : (int)(progress * 10);
    }

    public sealed class MiningState
    {
        public float Progress => (float)progress;
        public bool Active { get; private set; }
        public Cell Target { get; private set; }
        public Block Block { get; private set; }
        private double progress, tickTime, cooldown;
        private int tool, identity;
        private bool creative, holding;

        public void Reset()
        {
            ClearTarget();
            cooldown = 0;
            holding = false;
        }

        private void ClearTarget()
        {
            Active = false;
            progress = tickTime = 0;
            Target = default;
            Block = Block.Air;
        }

        public bool Step(float dt, bool held, bool hasTarget, Cell target, Block block, int heldTool,
            bool isCreative, bool grounded, bool headSubmerged, int toolIdentity = 0)
        {
            if (!held) { Reset(); return false; }
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0) return false;
            double elapsed = Math.Min(dt, .1f);
            if (holding && creative != isCreative) Reset();
            holding = true;
            creative = isCreative;
            bool valid = hasTarget && MiningRules.CanBreak(block, heldTool, isCreative);
            if (!valid) ClearTarget();
            else if (!Active || target != Target || block != Block || heldTool != tool || toolIdentity != identity)
            {
                ClearTarget();
                Target = target;
                Block = block;
                tool = heldTool;
                identity = toolIdentity;
            }
            bool waited = cooldown > 0;
            if (waited)
            {
                double waiting = Math.Min(cooldown, elapsed);
                cooldown -= waiting;
                elapsed -= waiting;
                if (cooldown > .0000001) return false;
            }
            if (!valid || dt <= 0) return false;
            double damage = MiningRules.DamagePerTick(block, heldTool, grounded, headSubmerged);
            if (isCreative || damage >= 1 - .0000001)
            {
                ClearTarget();
                cooldown = Math.Max(0, (isCreative ? MiningRules.HoldDelay : MiningRules.TickDuration) - (waited ? elapsed : 0));
                return true;
            }
            Active = true;
            tickTime += elapsed;
            while (tickTime + .0000001 >= MiningRules.TickDuration)
            {
                tickTime = Math.Max(0, tickTime - MiningRules.TickDuration);
                progress += damage;
                if (progress < 1 - .0000001) continue;
                double remainder = tickTime;
                ClearTarget();
                cooldown = Math.Max(0, MiningRules.HoldDelay - remainder);
                return true;
            }
            return false;
        }
    }
}
