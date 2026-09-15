using System;

namespace VoxelWilds.Core
{
    public enum MobKind
    {
        Zombie, Skeleton, Creeper, Spider, Cow, Pig, Sheep, Chicken, Fox, Blaze,
        Piglin, Brute, WitherSkeleton, Enderman, Villager, EndDragon, EndCrystal
    }

    public readonly struct MobDefinition
    {
        public readonly float Health, Speed, Height, Radius, Damage;
        public readonly bool Hostile, Undead, Flying;
        public MobDefinition(float health, float speed, float height, float radius, float damage = 0,
            bool hostile = false, bool undead = false, bool flying = false)
        { Health = health; Speed = speed; Height = height; Radius = radius; Damage = damage; Hostile = hostile; Undead = undead; Flying = flying; }
    }

    public static class MobRules
    {
        public const int NearbySpawnerCap = 6;
        public static MobDefinition Definition(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Zombie: return new MobDefinition(20, 1.8f, 1.95f, .29f, 3, true, true);
                case MobKind.Skeleton: return new MobDefinition(20, 1.65f, 1.95f, .27f, 3, true, true);
                case MobKind.Creeper: return new MobDefinition(20, 1.6f, 1.8f, .28f, 0, true);
                case MobKind.Spider: return new MobDefinition(16, 2.3f, .85f, .62f, 2, true);
                case MobKind.Cow: return new MobDefinition(10, .9f, 1.45f, .42f);
                case MobKind.Pig: return new MobDefinition(10, .85f, 1.3f, .36f);
                case MobKind.Sheep: return new MobDefinition(8, .85f, 1.4f, .4f);
                case MobKind.Chicken: return new MobDefinition(4, 1, .8f, .22f);
                case MobKind.Fox: return new MobDefinition(10, 1.5f, .9f, .3f);
                case MobKind.Blaze: return new MobDefinition(20, 1.4f, 1.7f, .35f, 5, true, false, true);
                case MobKind.Piglin: return new MobDefinition(16, 2, 1.95f, .3f, 5, true);
                case MobKind.Brute: return new MobDefinition(50, 1.9f, 1.95f, .33f, 9, true);
                case MobKind.WitherSkeleton: return new MobDefinition(20, 2.1f, 2.35f, .3f, 6, true, true);
                case MobKind.Enderman: return new MobDefinition(40, 3.8f, 2.9f, .29f, 7);
                case MobKind.Villager: return new MobDefinition(20, 1, 1.95f, .3f);
                case MobKind.EndDragon: return new MobDefinition(200, 12, 4, 3.5f, 10, true, false, true);
                case MobKind.EndCrystal: return new MobDefinition(5, 0, 2, .7f, 0, false, false, true);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public static bool IsNight(float day) => day < .23f || day > .77f;
        public static bool IsFireImmune(MobKind kind) => kind == MobKind.Blaze || kind == MobKind.Piglin || kind == MobKind.Brute || kind == MobKind.WitherSkeleton || kind == MobKind.EndDragon || kind == MobKind.EndCrystal;
        public static bool IsAnimal(MobKind kind) => kind >= MobKind.Cow && kind <= MobKind.Fox;
        public static bool CanSpawnerActivate(float playerDistance, int nearbyCount, int difficulty)
            => difficulty > 0 && playerDistance <= 16 && nearbyCount < NearbySpawnerCap;
        public static bool CanSpawnerSpawn(MobKind kind, bool clear, bool grounded, bool wet, bool bright)
            => clear && grounded && !wet && (kind == MobKind.Blaze || !bright);
        public static bool IsStaring(float dot, float distance, bool lineOfSight)
            => lineOfSight && distance > 0 && distance <= 48 && dot > 1f - .025f / distance;
        public static bool SafeTeleport(bool grounded, bool clear, Block feet)
            => grounded && clear && !Blocks.IsFluid(feet) && feet != Block.Campfire;
        public static float Damage(float normal, int difficulty)
            => difficulty <= 0 ? 0 : difficulty == 1 ? Math.Min(normal * .5f + 1, normal) : difficulty >= 3 ? normal * 1.5f : normal;
        public static float HealDragon(float health, float delta, bool crystalNearby)
            => Math.Min(200, health + (crystalNearby ? Math.Max(0, delta) : 0));
        public static int SpawnDelay(Random random) => random.Next(200, 801);
        public static string ModelName(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Brute: return "piglin";
                case MobKind.WitherSkeleton: return "skeleton";
                case MobKind.EndDragon: return "end_dragon";
                case MobKind.EndCrystal: return "end_crystal";
                default: return kind.ToString().ToLowerInvariant();
            }
        }
        public static MobKind Parse(string name)
        {
            if (name == "dragon" || name == "ender_dragon" || name == "end_dragon") return MobKind.EndDragon;
            if (name == "wither_skeleton") return MobKind.WitherSkeleton;
            if (name == "end_crystal") return MobKind.EndCrystal;
            return Enum.TryParse(name, true, out MobKind kind) ? kind : MobKind.Zombie;
        }
    }
}
