using UnityEngine;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class MobActor : MonoBehaviour
    {
        MobDirector director;
        MobVisual visual;
        Vector3 velocity, wanderTarget, home;
        float thinkTime, cooldown, hurtTime, environmentTime, attackTime, fuse, panic, gaze, phaseTime, orbit;
        float air = 15;
        int dragonPhase;
        bool grounded, attackLanded;
        LineRenderer healingBeam;
        Material beamMaterial;
        public MobKind Kind { get; private set; }
        public MobDefinition Definition { get; private set; }
        public float Health { get; private set; }
        public float Anger { get; private set; }
        public bool Dead { get; private set; }
        public bool Persistent { get; set; }
        public bool IsHostile => Kind == MobKind.Enderman ? Anger > 0 : Definition.Hostile && !Pacified;
        public Bounds HitBounds => new Bounds(transform.position + Vector3.up * Definition.Height * .5f,
            new Vector3(Definition.Radius * 2, Definition.Height, Definition.Radius * 2));
        GameSession Game => director.Game;
        bool Pacified
        {
            get
            {
                if (Kind == MobKind.Piglin && Anger <= 0 && Game.Player.Inventory != null)
                    foreach (var armor in Game.Player.Inventory.Armor) if (armor != null && !armor.Empty && armor.Id == Items.GoldHelmet) return true;
                return Kind == MobKind.Spider && Anger <= 0 && !MobRules.IsNight(Game.TimeOfDay) && Game.World.SkyVisible(MobDirector.CellAt(transform.position));
            }
        }

        public void Init(MobDirector owner, MobKind kind)
        {
            director = owner; Kind = kind; Definition = MobRules.Definition(kind); Health = Definition.Health;
            home = transform.position; wanderTarget = home;
            cooldown = Random.Range(.4f, 2);
            phaseTime = 16;
            orbit = Random.value * Mathf.PI * 2;
            visual = gameObject.AddComponent<MobVisual>();
            visual.Init(kind);
            if (kind == MobKind.EndDragon)
            {
                healingBeam = gameObject.AddComponent<LineRenderer>();
                healingBeam.positionCount = 2; healingBeam.useWorldSpace = true;
                healingBeam.startWidth = .14f; healingBeam.endWidth = .30f;
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) { beamMaterial = new Material(shader); healingBeam.sharedMaterial = beamMaterial; }
                healingBeam.startColor = new Color(.9f, .2f, 1, .75f);
                healingBeam.endColor = new Color(.5f, .15f, 1, .3f);
                healingBeam.enabled = false;
            }
        }
        void OnDestroy() { if (beamMaterial != null) Destroy(beamMaterial); }
        public void Tick(float dt)
        {
            if (Dead) return;
            float playerDistanceSquared = (transform.position - Game.Player.transform.position).sqrMagnitude;
            float drawDistance = Mathf.Clamp(Game.Settings.EntityDistance, 24, 160);
            visual.SetVisible(Kind == MobKind.EndDragon || playerDistanceSquared <= drawDistance * drawDistance);
            hurtTime = Mathf.Max(0, hurtTime - dt);
            cooldown = Mathf.Max(0, cooldown - dt);
            Anger = Mathf.Max(0, Anger - dt);
            panic = Mathf.Max(0, panic - dt);
            if (Game.Difficulty == 0 && Definition.Hostile && Kind != MobKind.EndDragon) { Dead = true; return; }
            if (Kind == MobKind.EndCrystal) { visual.Animate(dt, 0, 0, hurtTime, 0, false); return; }
            if (Kind == MobKind.EndDragon) { TickDragon(dt); return; }
            if ((transform.position - Game.Player.transform.position).sqrMagnitude > 100 * 100 && Persistent) return;
            var feet = Game.World.GetBlock(MobDirector.CellAt(transform.position + Vector3.up * .1f));
            bool wet = feet == Block.Water;
            Environment(dt, feet);
            if (Dead) return;
            if (Kind == MobKind.Enderman) TickEnderman(dt, wet);
            Vector3 target = Game.Player.transform.position;
            float distance = Vector3.Distance(target, transform.position);
            bool seesPlayer = !Game.Player.Dead && !Game.Player.IsCreative && distance < 26 && director.LineClear(transform.position + Vector3.up * Definition.Height * .8f, Game.Player.Eye.transform.position);
            bool chase = IsHostile && (seesPlayer || Anger > 0 && distance < 40) && !Game.Player.Dead && !Game.Player.IsCreative;
            if (Kind == MobKind.Villager)
            {
                foreach (var threat in director.Actors)
                {
                    if (threat == this || threat.Dead || !threat.IsHostile) continue;
                    float threatDistance = Vector3.Distance(threat.transform.position, transform.position);
                    if (threatDistance > 8 || !director.LineClear(transform.position + Vector3.up, threat.transform.position + Vector3.up)) continue;
                    target = threat.transform.position; distance = threatDistance; panic = 2; break;
                }
            }
            MobActor villager = null;
            if (Kind == MobKind.Zombie && (!chase || distance > 12))
            {
                foreach (var actor in director.Actors)
                {
                    if (actor == this || actor.Dead || actor.Kind != MobKind.Villager) continue;
                    float d = Vector3.Distance(actor.transform.position, transform.position);
                    if (d >= Mathf.Min(distance, 12) || !director.LineClear(transform.position + Vector3.up, actor.transform.position + Vector3.up)) continue;
                    target = actor.transform.position; distance = d; villager = actor; chase = true;
                }
            }
            if (chase && seesPlayer) Anger = Mathf.Max(Anger, 6);
            Vector3 desired = Vector3.zero;
            if (panic > 0 && !Definition.Hostile && Kind != MobKind.Enderman)
            {
                desired = Flat(transform.position - target).normalized * Definition.Speed * 2.2f;
            }
            else if (chase)
            {
                Vector3 direction = Flat(target - transform.position).normalized;
                bool ranged = Kind == MobKind.Skeleton || Kind == MobKind.Blaze;
                if (Kind == MobKind.Creeper)
                {
                    if (distance < 3 && seesPlayer) fuse += dt;
                    else fuse = Mathf.Max(0, fuse - dt * 2);
                    if (fuse > 1.5f) { Dead = true; Game.Explode(transform.position + Vector3.up * .5f, 3); return; }
                    desired = distance > 2.1f ? direction * Definition.Speed : Vector3.zero;
                }
                else if (ranged)
                {
                    desired = direction * Definition.Speed * (distance < 5 ? -.65f : distance > 12 ? 1 : 0);
                    if (distance < 18 && cooldown <= 0 && seesPlayer) BeginAttack();
                    if (attackTime > 0)
                    {
                        attackTime -= dt;
                        if (attackTime < .2f && !attackLanded)
                        {
                            attackLanded = true;
                            Vector3 start = transform.position + Vector3.up * Definition.Height * .75f + direction * .45f;
                            Vector3 aim = Game.Player.Eye.transform.position - start;
                            if (Kind == MobKind.Skeleton) aim.y += aim.magnitude * .065f;
                            director.Projectile(start, aim.normalized * (Kind == MobKind.Blaze ? 13 : 19), Definition.Damage, false, Kind == MobKind.Blaze, this);
                        }
                    }
                }
                else
                {
                    desired = distance > 1.5f ? direction * Definition.Speed : Vector3.zero;
                    if (distance < 2 && cooldown <= 0) BeginAttack();
                    if (attackTime > 0)
                    {
                        attackTime -= dt;
                        if (attackTime <= .15f && !attackLanded)
                        {
                            attackLanded = true;
                            if (distance < 2.35f && director.LineClear(transform.position + Vector3.up, target + Vector3.up))
                            {
                                if (villager != null) villager.Hurt(Definition.Damage, transform.position, false);
                                else Game.Player.Damage(MobRules.Damage(Definition.Damage, Game.Difficulty), transform.position);
                            }
                        }
                    }
                }
                Turn(direction, dt * 7);
            }
            else
            {
                attackTime = 0;
                fuse = Mathf.Max(0, fuse - dt * 2);
                thinkTime -= dt;
                if (thinkTime <= 0)
                {
                    thinkTime = Random.Range(2, 5);
                    Vector2 offset = Random.insideUnitCircle * (Kind == MobKind.Villager ? 9 : 5);
                    wanderTarget = (Kind == MobKind.Villager ? home : transform.position) + new Vector3(offset.x, 0, offset.y);
                    if (Random.value < .3f) wanderTarget = transform.position;
                }
                var held = Game.Player.Inventory?.Held;
                bool food = held != null && (held.Id == Items.Wheat && (Kind == MobKind.Cow || Kind == MobKind.Sheep) || held.Id == Items.Seeds && Kind == MobKind.Chicken || held.Id == Items.Apple && Kind == MobKind.Pig);
                if (food && distance < 10 && distance > 2) wanderTarget = target;
                Vector3 direction = Flat(wanderTarget - transform.position);
                if (direction.magnitude > .7f) { desired = direction.normalized * Definition.Speed * .55f; Turn(direction, dt * 5); }
            }
            if (wet) desired *= .55f;
            foreach (var neighbor in director.Actors)
            {
                if (neighbor == this || neighbor.Dead || Mathf.Abs(neighbor.transform.position.y - transform.position.y) > 1) continue;
                Vector3 apart = Flat(transform.position - neighbor.transform.position);
                float separation = Definition.Radius + neighbor.Definition.Radius;
                if (apart.sqrMagnitude > .0001f && apart.sqrMagnitude < separation * separation)
                    desired += apart.normalized * Mathf.Min(1.5f, (separation - apart.magnitude) * 3);
            }
            if (hurtTime <= .25f)
            {
                float blend = 1 - Mathf.Exp(-dt * 7);
                velocity.x = Mathf.Lerp(velocity.x, desired.x, blend);
                velocity.z = Mathf.Lerp(velocity.z, desired.z, blend);
            }
            Move(dt, wet, chase);
            visual.Animate(dt, Flat(velocity).magnitude, attackTime / .55f, hurtTime, fuse, chase);
        }
        void BeginAttack() { attackTime = .55f; attackLanded = false; cooldown = Kind == MobKind.Blaze ? 2.2f : Kind == MobKind.Skeleton ? 2.6f : 1.25f; }
        void Environment(float dt, Block feet)
        {
            environmentTime += dt;
            if (Game.World.GetBlock(MobDirector.CellAt(transform.position + Vector3.up * Definition.Height * .9f)) == Block.Water) air -= dt;
            else air = 15;
            if (transform.position.y < -8) { Die(false); return; }
            if (environmentTime < 1) return;
            environmentTime = 0;
            if (air <= 0) Hurt(2, transform.position, false, true);
            if ((feet == Block.Lava || feet == Block.Campfire) && !MobRules.IsFireImmune(Kind)) Hurt(4, transform.position, false, true);
            if (Kind == MobKind.Enderman && feet == Block.Water) Hurt(1, transform.position, false, true);
            if (Definition.Undead && !MobRules.IsFireImmune(Kind) && Game.World.Dimension == Dimension.Overworld && !MobRules.IsNight(Game.TimeOfDay) && feet != Block.Water && Game.World.SkyVisible(MobDirector.CellAt(transform.position + Vector3.up * Definition.Height)))
                Hurt(1, transform.position, false, true);
        }
        void TickEnderman(float dt, bool wet)
        {
            Vector3 eyes = transform.position + Vector3.up * 2.7f;
            Vector3 toEyes = eyes - Game.Player.Eye.transform.position;
            float distance = toEyes.magnitude;
            float dot = Vector3.Dot(Game.Player.Eye.transform.forward, toEyes.normalized);
            if (!Game.Player.IsCreative && !Game.Player.Dead && MobRules.IsStaring(dot, distance, true) && director.LineClear(eyes, Game.Player.Eye.transform.position))
            {
                gaze += dt;
                if (gaze > .45f && Anger <= 0) { Anger = 60; Game.Notify("An enderman noticed your stare."); }
            }
            else gaze = 0;
            if (wet && cooldown <= 0) { TryTeleport(); cooldown = .7f; }
            else if (Anger > 0 && toEyes.magnitude > 16 && cooldown <= 0)
            { TryTeleport(Game.Player.transform.position); cooldown = 3; }
        }
        public bool TryTeleport(Vector3? around = null)
        {
            Vector3 center = around ?? transform.position;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int x = Mathf.FloorToInt(center.x + Random.Range(-16, 17)), z = Mathf.FloorToInt(center.z + Random.Range(-16, 17));
                int top = Game.World.Dimension == Dimension.Nether ? Mathf.Min(76, Mathf.CeilToInt(center.y + 8)) : Mathf.Min(World.Height - 4, Mathf.CeilToInt(center.y + 12));
                if (!director.FindGround(x, z, top, out Vector3 position, Definition.Height)) continue;
                if ((position - Game.Player.transform.position).sqrMagnitude < 4 || director.CountNear(position, 1) > 0) continue;
                transform.position = position; velocity = Vector3.zero; wanderTarget = position; return true;
            }
            return false;
        }
        void Move(float dt, bool wet, bool chase)
        {
            Vector3 position = transform.position;
            grounded = !director.ClearBody(position + Vector3.down * .07f, Definition.Radius, Definition.Height);
            if (Definition.Flying)
            {
                float targetY = Game.Player.transform.position.y + 2.2f;
                velocity.y = Mathf.Lerp(velocity.y, Mathf.Clamp(targetY - position.y, -2, 2), dt * 2);
            }
            else if (wet) velocity.y = Mathf.Lerp(velocity.y, 2.2f, dt * 4);
            else velocity.y = grounded && velocity.y < 0 ? -.5f : Mathf.Max(velocity.y - dt * 24, Kind == MobKind.Chicken ? -3 : -22);
            Vector3 movement = velocity * dt;
            bool jumpRequested = false;
            int steps = Mathf.Max(1, Mathf.CeilToInt(movement.magnitude / .15f));
            for (int step = 0; step < steps; step++)
            {
                Vector3 delta = movement / steps;
                bool blocked = false;
                Vector3 next = position + new Vector3(delta.x, 0, 0);
                if (director.ClearBody(next, Definition.Radius, Definition.Height) && (chase || Definition.Flying || wet || Supported(next))) position = next;
                else { velocity.x = 0; blocked = true; }
                next = position + new Vector3(0, 0, delta.z);
                if (director.ClearBody(next, Definition.Radius, Definition.Height) && (chase || Definition.Flying || wet || Supported(next))) position = next;
                else { velocity.z = 0; blocked = true; }
                if (blocked && grounded && director.ClearBody(position + Vector3.up * 1.1f, Definition.Radius, Definition.Height))
                {
                    if (chase || Random.value < dt * 2) jumpRequested = true;
                    else { thinkTime = 0; }
                }
                next = position + new Vector3(0, delta.y, 0);
                if (director.ClearBody(next, Definition.Radius, Definition.Height)) position = next;
                else
                {
                    if (delta.y < 0)
                    {
                        float low = 0, high = 1;
                        for (int i = 0; i < 7; i++)
                        {
                            float mid = (low + high) * .5f;
                            if (director.ClearBody(position + new Vector3(0, delta.y * mid, 0), Definition.Radius, Definition.Height)) low = mid; else high = mid;
                        }
                        position += new Vector3(0, delta.y * low, 0);
                    }
                    velocity.y = 0;
                }
            }
            if (jumpRequested) velocity.y = 7.4f;
            transform.position = position;
        }
        bool Supported(Vector3 position)
        {
            var cell = MobDirector.CellAt(position);
            for (int i = 1; i <= 3; i++) if (Game.World.Solid(new Cell(cell.X, cell.Y - i, cell.Z))) return !Blocks.IsFluid(Game.World.GetBlock(cell));
            return false;
        }
        void TickDragon(float dt)
        {
            phaseTime -= dt;
            if (phaseTime <= 0) { dragonPhase = (dragonPhase + 1) % 3; phaseTime = dragonPhase == 0 ? 18 : dragonPhase == 1 ? 8 : 12; }
            orbit += dt * .21f;
            Vector3 center = Game.World.Dimension == Dimension.End ? new Vector3(.5f, 47, .5f) : home;
            Vector3 target;
            if (dragonPhase == 0) target = center + new Vector3(Mathf.Cos(orbit) * 38, 20 + Mathf.Sin(orbit * 2) * 5, Mathf.Sin(orbit) * 38);
            else if (dragonPhase == 1) target = Game.Player.transform.position + Vector3.up * 6 + Flat(Game.Player.transform.position - transform.position).normalized * 8;
            else target = center + Vector3.up * .6f;
            Vector3 direction = target - transform.position;
            float speed = dragonPhase == 2 ? Mathf.Min(10, direction.magnitude * .65f) : Definition.Speed;
            velocity = Vector3.Lerp(velocity, direction.normalized * speed, 1 - Mathf.Exp(-dt * 1.3f));
            transform.position += velocity * dt;
            Vector3 facing = dragonPhase == 2 && direction.magnitude < 2 ? Game.Player.transform.position - transform.position : velocity;
            Turn(facing, dt * 1.3f);
            MobActor crystal = null;
            float nearest = 42;
            foreach (var candidate in director.Actors)
            {
                if (candidate.Dead || candidate.Kind != MobKind.EndCrystal) continue;
                float distance = Vector3.Distance(candidate.transform.position, transform.position);
                if (distance < nearest) { nearest = distance; crystal = candidate; }
            }
            Health = MobRules.HealDragon(Health, dt, crystal != null);
            if (healingBeam != null)
            {
                healingBeam.enabled = crystal != null;
                if (crystal != null) { healingBeam.SetPosition(0, crystal.transform.position + Vector3.up); healingBeam.SetPosition(1, transform.position + Vector3.up * 2); }
            }
            bool combat = !Game.Player.IsCreative && !Game.Player.Dead && Game.Difficulty > 0;
            if (combat && cooldown <= 0 && dragonPhase != 0)
            {
                Vector3 origin = transform.position + transform.forward * 4 + Vector3.up * 2;
                Vector3 aim = Game.Player.Eye.transform.position - origin;
                if (aim.magnitude < 64)
                { director.Projectile(origin, aim.normalized * 15, 6, false, true, this); attackTime = .7f; cooldown = dragonPhase == 2 ? .9f : 1.8f; }
            }
            if (combat && Vector3.Distance(HitBounds.ClosestPoint(Game.Player.transform.position + Vector3.up), Game.Player.transform.position + Vector3.up) < .8f && hurtTime <= 0)
            { Game.Player.Damage(MobRules.Damage(10, Game.Difficulty), transform.position); hurtTime = .65f; }
            attackTime = Mathf.Max(0, attackTime - dt);
            visual.Animate(dt, velocity.magnitude, attackTime, hurtTime, 0, true);
        }
        public bool Hurt(float amount, Vector3 source, bool projectile = false, bool environment = false)
        {
            if (Dead || amount <= 0 || float.IsNaN(amount)) return false;
            if (Kind == MobKind.Enderman && projectile && TryTeleport()) { Anger = 60; return false; }
            if (hurtTime > .15f && !environment) return false;
            Health -= amount;
            hurtTime = .4f;
            if (!environment)
            {
                Anger = 60; panic = 4;
                Vector3 direction = Flat(transform.position - source).normalized;
                velocity = direction * 5 + Vector3.up * 3.5f;
            }
            if (Health <= 0) Die(true);
            else if (Kind == MobKind.Enderman && environment) TryTeleport();
            return true;
        }
        void Die(bool loot)
        {
            if (Dead) return;
            Dead = true; Health = 0;
            if (Kind == MobKind.EndCrystal) { Game.Explode(transform.position + Vector3.up, 4); return; }
            if (Kind == MobKind.EndDragon)
            {
                if (healingBeam != null) healingBeam.enabled = false;
                if (Game.World.Dimension == Dimension.End && !Game.EndDragonDefeated) Game.DefeatDragon();
                return;
            }
            if (!loot) return;
            switch (Kind)
            {
                case MobKind.Cow: Drop(Items.RawBeef, Random.Range(1, 4)); Drop(Items.Leather, Random.Range(0, 3)); break;
                case MobKind.Pig: Drop(Items.RawPorkchop, Random.Range(1, 4)); break;
                case MobKind.Sheep: Drop(Items.RawMutton, Random.Range(1, 3)); Drop((int)Block.Wool, 1); break;
                case MobKind.Chicken: Drop(Items.RawChicken, 1); Drop(Items.Feather, Random.Range(0, 3)); break;
                case MobKind.Zombie: Drop(Items.RottenFlesh, Random.Range(0, 3)); break;
                case MobKind.Skeleton: case MobKind.WitherSkeleton: Drop(Items.Bone, Random.Range(0, 3)); Drop(Items.Arrow, Random.Range(0, 3)); break;
                case MobKind.Spider: Drop(Items.String, Random.Range(0, 3)); break;
                case MobKind.Blaze: Drop(Items.BlazeRod, Random.Range(0, 2)); break;
                case MobKind.Piglin: Drop(Items.GoldNugget, Random.Range(0, 4)); break;
                case MobKind.Brute: Drop(Items.GoldIngot, Random.Range(0, 2)); break;
                case MobKind.Enderman: Drop(Items.EnderPearl, Random.Range(0, 2)); break;
            }
        }
        void Drop(int id, int count)
        {
            if (count <= 0) return;
            bool burning = Game.World.GetBlock(MobDirector.CellAt(transform.position)) == Block.Lava;
            if (burning && (id == Items.RawMutton || id == Items.RawBeef || id == Items.RawPorkchop || id == Items.RawChicken)) id++;
            Game.DropLoot(transform.position + Vector3.up * .5f, id, count);
        }
        void Turn(Vector3 direction, float amount)
        {
            direction = Flat(direction);
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Mathf.Clamp01(amount));
        }
        static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0, value.z);
        public MobSnapshot Capture() => new MobSnapshot
        {
            Kind = (int)Kind, X = transform.position.x, Y = transform.position.y, Z = transform.position.z,
            Yaw = transform.eulerAngles.y, Health = Health, Anger = Anger, Persistent = Persistent,
            HomeX = home.x, HomeY = home.y, HomeZ = home.z
        };
        public void Restore(MobSnapshot snapshot)
        {
            Health = Mathf.Clamp(snapshot.Health, .1f, Definition.Health); Anger = Mathf.Clamp(snapshot.Anger, 0, 60);
            Persistent = snapshot.Persistent; home = new Vector3(snapshot.HomeX, snapshot.HomeY, snapshot.HomeZ);
            transform.rotation = Quaternion.Euler(0, snapshot.Yaw, 0);
        }
    }
}
