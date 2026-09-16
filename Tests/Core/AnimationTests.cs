using VoxelWilds.Core;

public static class AnimationTests
{
    public static void Run()
    {
        Spec.Run("Biped walking alternates left and right legs", () =>
        {
            foreach (var kind in new[] { MobKind.Zombie, MobKind.Skeleton, MobKind.Enderman, MobKind.Villager })
                Spec.Equal(-MobRules.GaitSign(kind, "leg_-1"), MobRules.GaitSign(kind, "leg_1"));
            Spec.Equal(-MobRules.GaitSign(MobKind.Chicken, "leg_-1_0"), MobRules.GaitSign(MobKind.Chicken, "leg_1_0"));
        });
        Spec.Run("Quadruped walking pairs diagonal legs", () =>
        {
            foreach (var kind in new[] { MobKind.Cow, MobKind.Pig, MobKind.Sheep, MobKind.Creeper })
            {
                Spec.Equal(MobRules.GaitSign(kind, "leg_-1_-1"), MobRules.GaitSign(kind, "leg_1_1"));
                Spec.Equal(-MobRules.GaitSign(kind, "leg_-1_-1"), MobRules.GaitSign(kind, "leg_1_-1"));
            }
        });
        Spec.Run("Spider leg pairs use alternating phases", () =>
        {
            Spec.Equal(-MobRules.GaitSign(MobKind.Spider, "leg_-1_0"), MobRules.GaitSign(MobKind.Spider, "leg_-1_1"));
            Spec.Equal(MobRules.GaitSign(MobKind.Spider, "leg_-1_0"), MobRules.GaitSign(MobKind.Spider, "leg_-1_2"));
            Spec.Equal(-MobRules.GaitSign(MobKind.Spider, "leg_-1_0"), MobRules.GaitSign(MobKind.Spider, "leg_1_0"));
        });
    }
}
