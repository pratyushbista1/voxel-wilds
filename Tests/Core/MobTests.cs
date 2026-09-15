using System;
using VoxelWilds.Core;

public static class MobTests
{
    public static void Run()
    {
        Spec.Run("Spawners require a nearby survival player and respect six-mob cap", () =>
        {
            Spec.True(MobRules.CanSpawnerActivate(16, 5, 2), "Boundary activates");
            Spec.True(!MobRules.CanSpawnerActivate(16.1f, 0, 2), "Outside range sleeps");
            Spec.True(!MobRules.CanSpawnerActivate(4, 6, 2), "Nearby cap");
            Spec.True(!MobRules.CanSpawnerActivate(4, 0, 0), "Peaceful disables hostile spawns");
        });
        Spec.Run("Spawner candidates reject walls, fluids and overworld light", () =>
        {
            Spec.True(MobRules.CanSpawnerSpawn(MobKind.Zombie, true, true, false, false), "Dark solid ground valid");
            Spec.True(!MobRules.CanSpawnerSpawn(MobKind.Zombie, false, true, false, false), "Wall blocked");
            Spec.True(!MobRules.CanSpawnerSpawn(MobKind.Zombie, true, false, false, false), "Floating invalid");
            Spec.True(!MobRules.CanSpawnerSpawn(MobKind.Zombie, true, true, true, false), "Water blocked");
            Spec.True(!MobRules.CanSpawnerSpawn(MobKind.Zombie, true, true, false, true), "Light prevents zombie");
            Spec.True(MobRules.CanSpawnerSpawn(MobKind.Blaze, true, true, false, true), "Blaze tolerates light");
        });
        Spec.Run("Enderman requires direct visible eye contact", () =>
        {
            Spec.True(MobRules.IsStaring(1, 20, true), "Direct gaze");
            Spec.True(!MobRules.IsStaring(.98f, 20, true), "Peripheral view not gaze");
            Spec.True(!MobRules.IsStaring(1, 20, false), "Wall hides player");
            Spec.True(!MobRules.IsStaring(1, 49, true), "Detection range");
        });
        Spec.Run("Enderman teleports only onto dry supported clear ground", () =>
        {
            Spec.True(MobRules.SafeTeleport(true, true, Block.Air), "Dry air above ground");
            Spec.True(!MobRules.SafeTeleport(false, true, Block.Air), "Void rejected");
            Spec.True(!MobRules.SafeTeleport(true, false, Block.Air), "Wall rejected");
            Spec.True(!MobRules.SafeTeleport(true, true, Block.Water), "Water rejected");
            Spec.True(!MobRules.SafeTeleport(true, true, Block.Lava), "Lava rejected");
        });
        Spec.Run("Dragon healing stops without crystals and caps at 200", () =>
        {
            Spec.Equal(200f, MobRules.HealDragon(199, 10, true), "Health cap");
            Spec.Equal(150f, MobRules.HealDragon(150, 10, false), "No crystal no heal");
            Spec.Equal(151f, MobRules.HealDragon(150, 1, true), "Crystal healing");
            Spec.Equal(200f, MobRules.Definition(MobKind.EndDragon).Health, "Boss starting HP");
        });
        Spec.Run("Difficulty changes mob damage without changing health", () =>
        {
            Spec.Equal(0f, MobRules.Damage(6, 0), "Peaceful");
            Spec.Equal(4f, MobRules.Damage(6, 1), "Easy");
            Spec.Equal(6f, MobRules.Damage(6, 2), "Normal");
            Spec.Equal(9f, MobRules.Damage(6, 3), "Hard");
            Spec.True(MobRules.IsFireImmune(MobKind.Blaze), "Blaze fire immune");
            Spec.True(!MobRules.IsFireImmune(MobKind.Zombie), "Zombie burns");
        });
        Spec.Run("Spawner reset delays stay between 200 and 800 game ticks", () =>
        {
            var rng = new Random(123);
            for (int i = 0; i < 1000; i++)
            {
                int delay = MobRules.SpawnDelay(rng);
                Spec.True(delay >= 200 && delay <= 800, "Delay range");
            }
        });
    }
}
