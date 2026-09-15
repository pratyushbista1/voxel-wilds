using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelWilds.Core;
using Random = UnityEngine.Random;

namespace VoxelWilds
{
    [Serializable]
    public sealed class MobSnapshot
    {
        public int Kind;
        public float X, Y, Z, Yaw, Health, Anger, HomeX, HomeY, HomeZ;
        public bool Persistent;
    }

    public sealed class MobDirector : MonoBehaviour
    {
        sealed class SpawnerState { public Cell Position; public MobKind Kind; public float Delay = 2; }
        readonly List<MobActor> actors = new List<MobActor>();
        readonly List<MobProjectile> projectiles = new List<MobProjectile>();
        readonly Dictionary<Cell, SpawnerState> spawners = new Dictionary<Cell, SpawnerState>();
        readonly HashSet<string> consumedMarkers = new HashSet<string>();
        readonly Dictionary<Cell, byte> blockLight = new Dictionary<Cell, byte>();
        readonly Queue<Cell> lightQueue = new Queue<Cell>();
        readonly HashSet<Cell> lightVolumes = new HashSet<Cell>();
        GameSession game;
        World observedWorld;
        float scanTimer, spawnTimer;
        public GameSession Game => game;
        public IReadOnlyList<MobActor> Actors => actors;
        public int ActiveCount
        {
            get
            {
                if (game == null || game.Player == null) return 0;
                int count = 0;
                foreach (var actor in actors) if (actor != null && !actor.Dead && (actor.transform.position - game.Player.transform.position).sqrMagnitude < 100 * 100) count++;
                return count;
            }
        }
        public int ActiveSpawnerCount => spawners.Count;
        public float DragonMaxHealth => 200;
        public float DragonHealth
        {
            get { foreach (var actor in actors) if (!actor.Dead && actor.Kind == MobKind.EndDragon) return actor.Health; return 0; }
        }

        public void Init(GameSession session)
        {
            Clear();
            game = session;
            observedWorld = game.World;
            observedWorld.Changed += OnBlockChanged;
            foreach (var edit in observedWorld.Edits) if (edit.Value.Id == Block.Spawner) RegisterSpawner(edit.Key);
            scanTimer = 0;
            spawnTimer = .5f;
        }
        void OnDestroy() { if (observedWorld != null) observedWorld.Changed -= OnBlockChanged; }
        void OnBlockChanged(Cell cell)
        {
            blockLight.Clear(); lightVolumes.Clear();
            if (observedWorld.GetBlock(cell) == Block.Spawner) RegisterSpawner(cell);
            else spawners.Remove(cell);
        }
        void RegisterSpawner(Cell cell, MobKind? kind = null)
        {
            if (spawners.ContainsKey(cell)) return;
            spawners.Add(cell, new SpawnerState { Position = cell, Kind = kind ?? (game.World.Dimension == Dimension.Nether ? MobKind.Blaze : MobKind.Zombie) });
        }
        public void Clear()
        {
            if (observedWorld != null) observedWorld.Changed -= OnBlockChanged;
            observedWorld = null;
            foreach (var actor in actors) if (actor != null) Destroy(actor.gameObject);
            foreach (var projectile in projectiles) if (projectile != null) Destroy(projectile.gameObject);
            actors.Clear(); projectiles.Clear(); spawners.Clear(); consumedMarkers.Clear(); blockLight.Clear(); lightVolumes.Clear(); lightQueue.Clear();
        }
        void Update()
        {
            if (game == null || !game.Playing || game.World == null || game.Player == null) return;
            float dt = Mathf.Min(Time.deltaTime, .06f);
            scanTimer -= dt;
            if (scanTimer <= 0) { ScanMarkers(); scanTimer = 1; }
            UpdateSpawners(dt);
            spawnTimer -= dt;
            if (spawnTimer <= 0) { SpawnAmbient(); spawnTimer = 5; }
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                var actor = actors[i];
                if (actor == null || actor.Dead) { if (actor != null) Destroy(actor.gameObject); actors.RemoveAt(i); continue; }
                actor.Tick(dt);
                if (!actor.Persistent && actor.Kind != MobKind.EndDragon && (actor.transform.position - game.Player.transform.position).sqrMagnitude > 160 * 160)
                { Destroy(actor.gameObject); actors.RemoveAt(i); }
            }
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                if (projectiles[i] == null || !projectiles[i].Tick(dt))
                { if (projectiles[i] != null) Destroy(projectiles[i].gameObject); projectiles.RemoveAt(i); }
            }
        }
        void ScanMarkers()
        {
            Vector3 player = game.Player.transform.position;
            int count = game.World.Markers.Count;
            for (int i = 0; i < count; i++)
            {
                var marker = game.World.Markers[i];
                Vector3 position = Position(marker.Position);
                if ((position - player).sqrMagnitude > 110 * 110) continue;
                if (marker.Kind == "spawner")
                {
                    if (game.World.GetBlock(marker.Position) == Block.Spawner) RegisterSpawner(marker.Position, MobRules.Parse(marker.Mob));
                    continue;
                }
                string key = marker.Kind + ":" + marker.Position;
                if (consumedMarkers.Contains(key)) continue;
                MobKind kind;
                if (marker.Kind == "crystal") kind = MobKind.EndCrystal;
                else if (marker.Kind == "dragon") kind = MobKind.EndDragon;
                else if (marker.Kind == "mob") kind = MobRules.Parse(marker.Mob);
                else continue;
                if ((kind == MobKind.EndDragon || kind == MobKind.EndCrystal) && game.EndDragonDefeated)
                { consumedMarkers.Add(key); continue; }
                if (kind == MobKind.EndDragon && DragonHealth > 0) { consumedMarkers.Add(key); continue; }
                var actor = Spawn(kind, position);
                if (actor != null) { actor.Persistent = true; consumedMarkers.Add(key); }
            }
        }
        void UpdateSpawners(float dt)
        {
            if (game.Player.Dead) return;
            foreach (var entry in spawners)
            {
                var spawner = entry.Value;
                Vector3 center = Position(spawner.Position);
                int nearby = CountNear(center, 9, spawner.Kind);
                if (!MobRules.CanSpawnerActivate(Vector3.Distance(center, game.Player.transform.position), nearby, game.Difficulty)) continue;
                spawner.Delay -= dt;
                if (spawner.Delay > 0) continue;
                if (spawner.Kind != MobKind.Blaze) PrepareLight(spawner.Position);
                bool spawned = false;
                for (int attempt = 0; attempt < 12 && nearby < MobRules.NearbySpawnerCap; attempt++)
                {
                    var cell = spawner.Position + new Cell(Random.Range(-4, 5), Random.Range(-1, 2), Random.Range(-4, 5));
                    Vector3 pos = Position(cell);
                    var definition = MobRules.Definition(spawner.Kind);
                    bool clear = ClearBody(pos, definition.Radius, definition.Height) && CountNear(pos, 1.2f) == 0;
                    bool wet = Blocks.IsFluid(game.World.GetBlock(cell));
                    if (!MobRules.CanSpawnerSpawn(spawner.Kind, clear, game.World.Solid(cell.Down), wet, Bright(cell))) continue;
                    if ((pos - game.Player.transform.position).sqrMagnitude < 2.25f) continue;
                    if (Spawn(spawner.Kind, pos) == null) continue;
                    nearby++; spawned = true;
                    if (nearby >= 4) break;
                }
                spawner.Delay = spawned ? Random.Range(10f, 40f) : 1;
            }
        }
        bool Bright(Cell cell)
        {
            if (game.World.Dimension == Dimension.Overworld && !MobRules.IsNight(game.TimeOfDay) && game.World.SkyVisible(cell)) return true;
            return blockLight.TryGetValue(cell, out byte level) && level > 0;
        }
        void PrepareLight(Cell center)
        {
            if (lightVolumes.Contains(center)) return;
            blockLight.Clear(); lightVolumes.Clear(); lightVolumes.Add(center);
            lightQueue.Clear();
            const int radius = 18;
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            for (int y = -15; y <= 16; y++)
            {
                Cell cell = center + new Cell(x, y, z);
                if (!game.World.IsLoaded(cell)) continue;
                var id = game.World.GetBlock(cell);
                byte emission = id == Block.Torch ? (byte)14 : id == Block.Lantern || id == Block.Glowstone || id == Block.Lava || id == Block.Campfire ? (byte)15 : (byte)0;
                if (emission == 0) continue;
                if (!blockLight.TryGetValue(cell, out byte current) || current < emission) { blockLight[cell] = emission; lightQueue.Enqueue(cell); }
            }
            while (lightQueue.Count > 0)
            {
                Cell cell = lightQueue.Dequeue();
                byte next = (byte)(blockLight[cell] - 1);
                if (next == 0) continue;
                foreach (var side in Cell.Cardinal)
                {
                    Cell neighbor = cell + side;
                    if (Math.Abs(neighbor.X - center.X) > radius || Math.Abs(neighbor.Z - center.Z) > radius || Math.Abs(neighbor.Y - center.Y) > 17 || !game.World.IsLoaded(neighbor)) continue;
                    if (!Blocks.IsTransparent(game.World.GetBlock(neighbor))) continue;
                    if (blockLight.TryGetValue(neighbor, out byte current) && current >= next) continue;
                    blockLight[neighbor] = next; lightQueue.Enqueue(neighbor);
                }
            }
        }
        void SpawnAmbient()
        {
            if (game.Player.Dead || ActiveCount >= 55) return;
            Vector3 player = game.Player.transform.position;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2;
                float distance = Random.Range(25, 55);
                int x = Mathf.FloorToInt(player.x + Mathf.Sin(angle) * distance), z = Mathf.FloorToInt(player.z + Mathf.Cos(angle) * distance);
                MobKind kind;
                if (game.World.Dimension == Dimension.End)
                {
                    if (CountKind(MobKind.Enderman) >= 12 || game.Difficulty == 0) return;
                    kind = MobKind.Enderman;
                }
                else if (game.World.Dimension == Dimension.Nether)
                {
                    if (game.Difficulty == 0 || CountHostile() >= 20) return;
                    kind = Random.value < .7f ? MobKind.Piglin : MobKind.WitherSkeleton;
                }
                else if (MobRules.IsNight(game.TimeOfDay) && game.Difficulty > 0)
                {
                    if (CountHostile() >= 20) return;
                    MobKind[] hostiles = { MobKind.Zombie, MobKind.Skeleton, MobKind.Creeper, MobKind.Spider, MobKind.Enderman };
                    kind = hostiles[Random.Range(0, hostiles.Length)];
                }
                else
                {
                    int animals = 0;
                    foreach (var mob in actors) if (!mob.Dead && MobRules.IsAnimal(mob.Kind) && (mob.transform.position - player).sqrMagnitude < 100 * 100) animals++;
                    if (animals >= 16) return;
                    kind = (MobKind)Random.Range((int)MobKind.Cow, (int)MobKind.Fox + 1);
                }
                if (!FindGround(x, z, game.World.Dimension == Dimension.Nether ? 76 : World.Height - 2, out Vector3 position, MobRules.Definition(kind).Height)) continue;
                if (game.World.Dimension == Dimension.Overworld && MobRules.IsAnimal(kind) && game.World.GetBlock(CellAt(position).Down) != Block.Grass) continue;
                if (Spawn(kind, position) != null) return;
            }
        }
        public MobActor Spawn(MobKind kind, Vector3 position)
        {
            if (game == null || game.World == null || actors.Count >= 512) return null;
            if (kind != MobKind.EndDragon && kind != MobKind.EndCrystal && ActiveCount >= 80 && (position - game.Player.transform.position).sqrMagnitude < 100 * 100) return null;
            if (kind == MobKind.EndDragon && DragonHealth > 0) return null;
            var definition = MobRules.Definition(kind);
            if (!definition.Flying && !ClearBody(position, definition.Radius, definition.Height))
            {
                bool found = false;
                for (int i = 1; i <= 3; i++)
                    if (ClearBody(position + Vector3.up * i, definition.Radius, definition.Height))
                    { position += Vector3.up * i; found = true; break; }
                if (!found) return null;
            }
            var obj = new GameObject(kind.ToString());
            obj.transform.SetParent(transform, false);
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            var actor = obj.AddComponent<MobActor>();
            actor.Init(this, kind);
            actors.Add(actor);
            return actor;
        }
        public bool Attack(Ray ray, float reach, float damage)
        {
            if (TraceMob(ray, reach, null, out MobActor actor, out float distance) && LineClear(ray.origin, ray.GetPoint(distance)))
            { actor.Hurt(damage, ray.origin, false); return true; }
            return false;
        }
        public bool TraceMob(Ray ray, float reach, MobActor ignore, out MobActor hit, out float distance)
        {
            hit = null; distance = reach;
            foreach (var actor in actors)
            {
                if (actor == null || actor.Dead || actor == ignore) continue;
                if (actor.HitBounds.IntersectRay(ray, out float d) && d >= 0 && d <= distance)
                { distance = d; hit = actor; }
                if (actor.Kind == MobKind.EndDragon)
                {
                    var head = new Bounds(actor.transform.position + Vector3.up * 2 + actor.transform.forward * 4, new Vector3(2.5f, 2.5f, 3));
                    if (head.IntersectRay(ray, out d) && d >= 0 && d <= distance) { distance = d; hit = actor; }
                }
            }
            return hit != null;
        }
        public bool HostileNear(Vector3 position, float radius)
        {
            foreach (var actor in actors) if (actor != null && !actor.Dead && actor.IsHostile && (actor.transform.position - position).sqrMagnitude < radius * radius) return true;
            return false;
        }
        public void DamageInRadius(Vector3 position, float radius, float maximumDamage = 24)
        {
            foreach (var actor in actors)
            {
                if (actor == null || actor.Dead) continue;
                float distance = Vector3.Distance(actor.HitBounds.ClosestPoint(position), position);
                if (distance <= radius && LineClear(position, actor.transform.position + Vector3.up)) actor.Hurt(maximumDamage * (1 - distance / Mathf.Max(.1f, radius)), position, false);
            }
        }
        public int CountNear(Vector3 position, float radius, MobKind? kind = null)
        {
            int count = 0;
            foreach (var actor in actors) if (actor != null && !actor.Dead && (!kind.HasValue || actor.Kind == kind.Value) && (actor.transform.position - position).sqrMagnitude <= radius * radius) count++;
            return count;
        }
        int CountKind(MobKind kind) { int count = 0; foreach (var actor in actors) if (!actor.Dead && actor.Kind == kind && (actor.transform.position - game.Player.transform.position).sqrMagnitude < 100 * 100) count++; return count; }
        int CountHostile() { int count = 0; foreach (var actor in actors) if (!actor.Dead && actor.Definition.Hostile && (actor.transform.position - game.Player.transform.position).sqrMagnitude < 100 * 100) count++; return count; }
        public bool FindGround(int x, int z, int top, out Vector3 position, float height = 2.9f)
        {
            position = Vector3.zero;
            for (int y = Mathf.Min(top, World.Height - 4); y > 1; y--)
            {
                var cell = new Cell(x, y, z);
                if (!game.World.Solid(cell.Down)) continue;
                Vector3 p = Position(cell);
                if (MobRules.SafeTeleport(true, ClearBody(p, .32f, height), game.World.GetBlock(cell))) { position = p; return true; }
            }
            return false;
        }
        public bool ClearBody(Vector3 position, float radius, float height)
        {
            if (position.y < -10 || position.y + height >= World.Height) return false;
            for (int y = Mathf.FloorToInt(position.y + .025f); y <= Mathf.FloorToInt(position.y + height - .025f); y++)
            for (int z = Mathf.FloorToInt(position.z - radius + .015f); z <= Mathf.FloorToInt(position.z + radius - .015f); z++)
            for (int x = Mathf.FloorToInt(position.x - radius + .015f); x <= Mathf.FloorToInt(position.x + radius - .015f); x++)
                if (game.World.Solid(new Cell(x, y, z))) return false;
            return true;
        }
        public bool LineClear(Vector3 start, Vector3 end)
        {
            float distance = Vector3.Distance(start, end);
            int steps = Mathf.CeilToInt(distance * 5);
            for (int i = 1; i < steps; i++) if (game.World.Solid(CellAt(Vector3.Lerp(start, end, i / (float)steps)))) return false;
            return true;
        }
        public void ShootArrow(Vector3 origin, Vector3 direction, float charge = 1)
            => Projectile(origin, direction.normalized * Mathf.Lerp(12, 40, Mathf.Clamp01(charge)), Mathf.Lerp(2, 10, Mathf.Clamp01(charge)), true, false, null);
        public void Projectile(Vector3 origin, Vector3 velocity, float damage, bool player, bool fire, MobActor owner)
        {
            if (projectiles.Count >= 64) return;
            var obj = new GameObject(fire ? "Fireball" : "Arrow");
            obj.transform.SetParent(transform, false); obj.transform.position = origin;
            var projectile = obj.AddComponent<MobProjectile>();
            projectile.Init(this, velocity, damage, player, fire, owner);
            projectiles.Add(projectile);
        }
        public MobSnapshot[] Capture()
        {
            var result = new List<MobSnapshot>();
            foreach (var actor in actors) if (actor != null && !actor.Dead) result.Add(actor.Capture());
            return result.ToArray();
        }
        public void Restore(MobSnapshot[] snapshots)
        {
            if (snapshots == null) return;
            foreach (var snapshot in snapshots)
            {
                if (snapshot == null || snapshot.Kind < 0 || snapshot.Kind > (int)MobKind.EndCrystal || snapshot.Health <= 0 ||
                    float.IsNaN(snapshot.X) || float.IsNaN(snapshot.Y) || float.IsNaN(snapshot.Z) || float.IsNaN(snapshot.Health) ||
                    float.IsInfinity(snapshot.X) || float.IsInfinity(snapshot.Y) || float.IsInfinity(snapshot.Z) ||
                    Mathf.Abs(snapshot.X) > 1000000 || Mathf.Abs(snapshot.Z) > 1000000 || snapshot.Y < -8 || snapshot.Y > World.Height) continue;
                var kind = (MobKind)snapshot.Kind;
                if (game.EndDragonDefeated && (kind == MobKind.EndDragon || kind == MobKind.EndCrystal)) continue;
                var actor = Spawn(kind, new Vector3(snapshot.X, snapshot.Y, snapshot.Z));
                if (actor != null) actor.Restore(snapshot);
            }
        }
        public string[] CaptureMarkers() { var result = new string[consumedMarkers.Count]; consumedMarkers.CopyTo(result); return result; }
        public void RestoreMarkers(string[] markers) { if (markers != null) foreach (var marker in markers) if (!string.IsNullOrEmpty(marker)) consumedMarkers.Add(marker); }
        public static Cell CellAt(Vector3 p) => new Cell(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
        public static Vector3 Position(Cell p) => new Vector3(p.X + .5f, p.Y + .025f, p.Z + .5f);
    }
}
