using System;
using VoxelWilds.Core;

public static class MiningTests
{
    static readonly Cell First = new Cell(10, 50, 10), Second = new Cell(11, 50, 10);
    static void Near(double expected, double actual)
        => Spec.True(Math.Abs(expected - actual) < .00001, "Expected " + expected + ", actual " + actual);
    static bool Step(MiningState state, float dt = .05f, Cell? target = null, Block block = Block.Stone,
        int tool = Items.WoodenPickaxe, bool creative = false, bool held = true, bool hasTarget = true,
        bool grounded = true, bool submerged = false, int identity = 0)
        => state.Step(dt, held, hasTarget, target ?? First, block, tool, creative, grounded, submerged, identity);

    public static void Run()
    {
        Spec.Run("Block hardness matches implemented material types", () =>
        {
            Spec.Equal(50f, Blocks.Hardness(Block.Obsidian));
            Spec.Equal(1.5f, Blocks.Hardness(Block.Stone));
            foreach (var block in new[] { Block.Log, Block.Planks, Block.Cobble, Block.Bricks, Block.NetherBricks, Block.Campfire })
                Spec.Equal(2f, Blocks.Hardness(block));
            foreach (var block in new[] { Block.CoalOre, Block.IronOre, Block.CrystalOre, Block.NetherGold, Block.Door, Block.EndStone })
                Spec.Equal(3f, Blocks.Hardness(block));
            Spec.Equal(.4f, Blocks.Hardness(Block.Netherrack));
            Spec.Equal(.2f, Blocks.Hardness(Block.Bed));
            Spec.Equal(.6f, Blocks.Hardness(Block.Grass));
            Spec.Equal(0f, Blocks.Hardness(Block.Torch));
            Spec.True(float.IsInfinity(Blocks.Hardness(Block.Water)));
        });
        Spec.Run("Survival mining durations round up to twenty hertz ticks", () =>
        {
            Spec.Equal(150, MiningRules.RequiredTicks(Block.Stone, 0));
            Spec.Equal(23, MiningRules.RequiredTicks(Block.Stone, Items.WoodenPickaxe));
            Spec.Equal(12, MiningRules.RequiredTicks(Block.Stone, Items.StonePickaxe));
            Spec.Equal(8, MiningRules.RequiredTicks(Block.Stone, Items.IronPickaxe));
            Spec.Equal(6, MiningRules.RequiredTicks(Block.Stone, Items.CrystalPickaxe));
            Spec.Equal(188, MiningRules.RequiredTicks(Block.Obsidian, Items.CrystalPickaxe));
            Spec.Equal(834, MiningRules.RequiredTicks(Block.Obsidian, Items.IronPickaxe));
            Spec.Equal(5000, MiningRules.RequiredTicks(Block.Obsidian, 0));
            Spec.Equal(60, MiningRules.RequiredTicks(Block.Log, 0));
            Spec.Equal(10, MiningRules.RequiredTicks(Block.Log, Items.IronAxe));
            Spec.Equal(1, MiningRules.RequiredTicks(Block.Torch, 0));
        });
        Spec.Run("Mining penalties multiply for head submersion and being airborne", () =>
        {
            double dry = MiningRules.DamagePerTick(Block.Stone, Items.IronPickaxe, true, false);
            Near(dry / 5, MiningRules.DamagePerTick(Block.Stone, Items.IronPickaxe, false, false));
            Near(dry / 5, MiningRules.DamagePerTick(Block.Stone, Items.IronPickaxe, true, true));
            Near(dry / 25, MiningRules.DamagePerTick(Block.Stone, Items.IronPickaxe, false, true));
            Spec.Equal(38, MiningRules.RequiredTicks(Block.Stone, Items.IronPickaxe, false, false));
            Spec.Equal(188, MiningRules.RequiredTicks(Block.Stone, Items.IronPickaxe, false, true));
        });
        Spec.Run("Correct tool determines harvest drops separately from speed", () =>
        {
            Spec.True(!Items.CanHarvest(Items.IronPickaxe, Block.Obsidian));
            Spec.Equal(6f, Items.MiningSpeed(Items.IronPickaxe, Block.Obsidian));
            Spec.True(Items.CanHarvest(Items.CrystalPickaxe, Block.Obsidian));
            Spec.True(!Items.CanHarvest(Items.WoodenPickaxe, Block.IronOre));
            Spec.True(Items.CanHarvest(Items.StonePickaxe, Block.IronOre));
            Spec.True(!Items.CanHarvest(0, Block.Lantern));
            Spec.True(Items.CanHarvest(Items.WoodenPickaxe, Block.Lantern));
            Spec.True(!Items.CanHarvest(0, Block.Snow));
            Spec.True(Items.CanHarvest(Items.IronShovel, Block.Snow));
            Spec.Equal(6f, Items.MiningSpeed(Items.IronHoe, Block.Leaves));
            Spec.Equal(6f, Items.MiningSpeed(Items.IronAxe, Block.Bed));
            Spec.Equal(6f, Items.MiningSpeed(Items.IronAxe, Block.Campfire));
        });
        Spec.Run("Mining advances in whole ticks and clears cracks after completion", () =>
        {
            var state = new MiningState();
            Spec.True(!Step(state, .024f));
            Spec.True(state.Active);
            Near(0, state.Progress);
            Spec.True(!Step(state, .026f));
            Near(2d / 45, state.Progress);
            for (int tick = 1; tick < 22; tick++) Spec.True(!Step(state));
            Spec.True(Step(state));
            Spec.True(!state.Active);
            Near(0, state.Progress);
        });
        Spec.Run("Mining outcome is independent of rendering frame rate", () =>
        {
            foreach (int fps in new[] { 20, 30, 60, 144 })
            {
                var state = new MiningState();
                int frames = 0;
                while (!Step(state, 1f / fps) && frames < fps * 2) frames++;
                double elapsed = (frames + 1d) / fps;
                Spec.True(elapsed >= 1.15 - .00001 && elapsed < 1.15 + 1d / fps + .00001, "fps=" + fps + " time=" + elapsed);
            }
        });
        Spec.Run("Changing mining target or material resets accumulated damage", () =>
        {
            var state = new MiningState();
            Step(state, .1f);
            Step(state, .1f);
            Step(state, target: Second);
            Near(2d / 45, state.Progress);
            Spec.Equal(Second, state.Target);
            Step(state, block: Block.Cobble, target: Second);
            Near(2d / 60, state.Progress);
            Spec.Equal(Block.Cobble, state.Block);
        });
        Spec.Run("Changing tool type or replacing the held stack resets mining", () =>
        {
            var state = new MiningState();
            Step(state, .1f);
            Step(state, .1f);
            Step(state, tool: Items.IronPickaxe);
            Near(6d / 45, state.Progress);
            Step(state, tool: Items.IronPickaxe, identity: 1);
            Near(6d / 45, state.Progress);
        });
        Spec.Run("Releasing attack or losing the target clears the mining state", () =>
        {
            var state = new MiningState();
            Step(state, .1f);
            Step(state, held: false);
            Spec.True(!state.Active);
            Near(0, state.Progress);
            Step(state);
            Near(2d / 45, state.Progress);
            Step(state, hasTarget: false);
            Spec.True(!state.Active);
            Near(0, state.Progress);
            Step(state);
            state.Reset();
            Spec.True(!state.Active);
            Spec.Equal(Block.Air, state.Block);
        });
        Spec.Run("Water and grounding changes adjust ongoing mining without resetting", () =>
        {
            var state = new MiningState();
            Step(state);
            double before = state.Progress;
            Step(state, submerged: true, grounded: false);
            Near(before + 2d / 45 / 25, state.Progress);
        });
        Spec.Run("Creative breaks instantly then waits five ticks while held", () =>
        {
            var state = new MiningState();
            Spec.True(Step(state, .001f, creative: true));
            Spec.True(!state.Active);
            for (int i = 0; i < 4; i++) Spec.True(!Step(state, creative: true, target: Second));
            Spec.True(Step(state, creative: true, target: Second));
            Step(state, held: false, creative: true);
            Spec.True(Step(state, .001f, creative: true));
        });
        Spec.Run("Creative switching targets does not bypass the hold delay", () =>
        {
            var state = new MiningState();
            Spec.True(Step(state, creative: true));
            Spec.True(!Step(state, .1f, creative: true, hasTarget: false));
            Spec.True(!Step(state, .1f, creative: true, target: Second, identity: 1));
            Spec.True(Step(state, creative: true, target: Second, identity: 1));
        });
        Spec.Run("Creative swords do not break blocks and forbidden targets never mine", () =>
        {
            foreach (int sword in new[] { Items.IronSword, Items.CrystalSword })
                Spec.True(!Step(new MiningState(), .1f, creative: true, tool: sword));
            foreach (var block in new[] { Block.Air, Block.Water, Block.Lava, Block.PortalX, Block.PortalZ, Block.EndPortal })
            {
                Spec.True(!MiningRules.CanBreak(block, 0, true));
                Spec.True(!Step(new MiningState(), .1f, block: block));
            }
            foreach (var block in new[] { Block.Bedrock, Block.EndFrame })
            {
                Spec.True(!Step(new MiningState(), .1f, block: block));
                Spec.True(Step(new MiningState(), .001f, block: block, creative: true));
                Spec.Equal(int.MaxValue, MiningRules.RequiredTicks(block, Items.CrystalPickaxe));
            }
        });
        Spec.Run("Instant blocks use one tick between held breaks instead of the normal delay", () =>
        {
            var state = new MiningState();
            Spec.True(Step(state, .001f, block: Block.Torch));
            Spec.True(!Step(state, .025f, block: Block.Crop, target: Second));
            Spec.True(Step(state, .025f, block: Block.Crop, target: Second));
            Spec.True(Step(state, .05f, block: Block.Leaves, tool: Items.IronHoe));
            Step(state, held: false);
            Spec.True(Step(state, .001f, block: Block.Torch));
        });
        Spec.Run("Instant held mining caps at twenty blocks per second across frame rates", () =>
        {
            foreach (int fps in new[] { 20, 30, 60, 120, 144 })
            {
                var state = new MiningState();
                int broken = 0;
                for (int i = 0; i < fps; i++)
                    if (Step(state, 1f / fps, target: new Cell(broken, 50, 10), block: Block.Torch)) broken++;
                Spec.Equal(20, broken, "fps=" + fps);
            }
        });
        Spec.Run("Mining completion preserves fractional frame time in the next hold delay", () =>
        {
            foreach (int fps in new[] { 20, 30, 60, 120, 144 })
            {
                var state = new MiningState();
                int broken = 0;
                for (int i = 0; i < fps * 3; i++)
                    if (Step(state, 1f / fps, target: new Cell(broken, 50, 10), tool: Items.CrystalPickaxe)) broken++;
                Spec.Equal(5, broken, "fps=" + fps);
            }
        });
        Spec.Run("Survival mining uses a five tick delay after noninstant completion", () =>
        {
            var state = new MiningState();
            for (int i = 0; i < 22; i++) Spec.True(!Step(state));
            Spec.True(Step(state));
            for (int i = 0; i < 5; i++) { Spec.True(!Step(state, target: Second)); Near(0, state.Progress); }
            Step(state, target: Second);
            Near(2d / 45, state.Progress);
        });
        Spec.Run("Switching game mode resets stale progress and hold delay", () =>
        {
            var state = new MiningState();
            Step(state);
            Spec.True(Step(state, .001f, creative: true));
            Spec.True(!Step(state, creative: false));
            Near(2d / 45, state.Progress);
        });
        Spec.Run("Invalid mining delta times cannot corrupt progress", () =>
        {
            var state = new MiningState();
            Step(state);
            double previous = state.Progress;
            foreach (float dt in new[] { float.NaN, float.PositiveInfinity, -1f, 0f })
            { Spec.True(!Step(state, dt)); Near(previous, state.Progress); }
            Spec.True(!Step(new MiningState(), 0, creative: true));
        });
        Spec.Run("Block cracks use ten stages and hide at zero progress", () =>
        {
            Spec.Equal(-1, MiningRules.CrackStage(0));
            Spec.Equal(-1, MiningRules.CrackStage(float.NaN));
            Spec.Equal(0, MiningRules.CrackStage(.01f));
            for (int i = 1; i < 10; i++) Spec.Equal(i, MiningRules.CrackStage(i * .1f));
            Spec.Equal(9, MiningRules.CrackStage(1));
        });
    }
}
