using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class SimulationSmoke : MonoBehaviour
    {
        private GameSession game;
        private float started;
        private bool finished;
        private string artifactDirectory;
        private string stage = "initialization";
        private readonly List<string> checks = new List<string>();

        private void Start()
        {
            started = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            StartCoroutine(Guarded(Exercise()));
        }

        private void Update()
        {
            if (finished) return;
            if (Time.realtimeSinceStartup - started > 90) { Fail("90-second watchdog expired during " + stage); return; }
            if (game != null && game.World != null && game.Paused && !game.Player.Dead) game.SetPaused(false);
        }

        private IEnumerator Guarded(IEnumerator scenario)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(scenario);
            while (stack.Count > 0 && !finished)
            {
                object next = null;
                bool moved = false;
                Exception error = null;
                try
                {
                    moved = stack.Peek().MoveNext();
                    if (moved) next = stack.Peek().Current;
                }
                catch (Exception caught) { error = caught; }
                if (error != null) { Fail(stage + ": " + error); yield break; }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
            if (!finished) Finish();
        }

        private IEnumerator Exercise()
        {
            game = GameSession.Instance;
            Require(game != null && game.Player != null && game.Renderer != null && game.Hud != null, "Scene bootstrap creates the session, player, renderer and HUD");
            ValidateDirectories();
            Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "Graphical smoke run has a graphics device");
            UnityEngine.Random.InitState(1453);
            game.Settings.ViewDistance = 2;
            game.Settings.Fps = 60;
            game.Settings.Shadows = 0;
            game.Settings.Fullscreen = false;
            game.ApplySettings();
            stage = "empty-handed survival start";
            game.NewWorld("Fresh survival check", 1453, false);
            game.SetPaused(false);
            Require(!game.Player.IsCreative && game.Player.Inventory.Slots.All(s => s == null || s.Empty), "New Survival worlds start empty-handed without free food, logs or tools");
            Require(game.Player.Inventory.Armor.All(s => s == null || s.Empty) && (game.Player.Inventory.Offhand == null || game.Player.Inventory.Offhand.Empty), "New players have no pre-equipped armor or offhand items");
            Cell firstLog = PlayerController.ToCell(game.Player.transform.position) + new Cell(0, 1, 1);
            game.World.Set(firstLog, Block.Log);
            game.BreakBlock(firstLog);
            Require(game.World.GetBlock(firstLog) == Block.Air && DroppedCount((int)Block.Log) == 1, "An empty hand can harvest the first log into a real drop");
            Require(UnityEngine.Object.FindObjectsByType<DroppedItem>(FindObjectsSortMode.None).Any(d => d.GetComponentInChildren<MeshFilter>() != null), "Dropped blocks use textured block meshes");
            yield return Until(() => game.Player.Inventory.Count((int)Block.Log) == 1, 4, "First log can be collected without a starter tool");
            var starterGrid = new ItemStack[4]; starterGrid[0] = new ItemStack((int)Block.Log);
            Require(game.Player.Inventory.Remove((int)Block.Log, 1) && Crafting.TryCraftInto(starterGrid, 2, game.Player.Inventory), "First collected log crafts through the personal grid");
            Require(game.Player.Inventory.Count((int)Block.Planks) == 4 && game.Player.Inventory.Count(Items.Berries) == 0, "First-day progression yields four planks and no unsolicited food");
            game.DropLoot(game.Player.transform.position + Vector3.forward * 5, Items.Apple, 1);
            Require(UnityEngine.Object.FindObjectsByType<DroppedItem>(FindObjectsSortMode.None).Any(d => d.Stack.Id == Items.Apple && d.GetComponentInChildren<SpriteRenderer>()?.sprite != null), "Dropped food uses its recognizable item sprite");
            game.ReturnToTitle();
            stage = "world creation and rendering";
            game.NewWorld("Smoke world", 1453, true);
            game.SetPaused(false);
            Require(game.World != null && game.World.Dimension == Dimension.Overworld && game.World.Seed == 1453, "NewWorld creates the requested Overworld seed");
            Require(game.Player.IsCreative && game.WorldName == "Smoke world", "NewWorld applies its name and Creative mode");
            Require(game.Player.Inventory.Slots.All(s => s == null || s.Empty), "New Creative hotbars are empty until the player chooses items");
            yield return Frames(3);
            Require(game.Renderer.ChunkCount >= 9, "Initial chunk meshes are built");
            var meshes = game.Renderer.GetComponentsInChildren<MeshFilter>();
            Require(meshes.Any(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 0), "Generated terrain has mesh vertices");
            Require(game.Renderer.GetComponentsInChildren<MeshCollider>().Any(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 0), "Generated terrain has native collision meshes");
            foreach (string name in new[] { "VoxelWilds/Terrain", "VoxelWilds/Fluid" })
            {
                Shader shader = Shader.Find(name);
                Require(shader != null && shader.isSupported, name + " is present and supported");
            }
            yield return Capture("01-overworld.png");

            stage = "native player collision";
            FlatPlatform(3, 15, 3, 15, 32);
            for (int z = 6; z <= 10; z++) game.World.Set(new Cell(11, 33, z), Block.Stone);
            game.Renderer.EnsureImmediate(new Vector3(8.5f, 33.1f, 8.5f));
            game.Player.Teleport(new Vector3(8.5f, 33.08f, 8.5f));
            CharacterController controller = game.Player.GetComponent<CharacterController>();
            Require(controller != null && controller.enabled && controller.stepOffset < .1f, "Player auto-step cannot climb a whole block");
            controller.Move(new Vector3(4, -.1f, 0));
            Require(game.Player.transform.position.x > 9.5f && game.Player.transform.position.x < 10.8f, "Native CharacterController moves forward and is stopped by a block wall");
            Require(game.Player.transform.position.y < 33.2f, "Colliding with a block does not auto-jump");
            controller.Move(Vector3.down * 3);
            Require(game.Player.transform.position.y >= 32.95f, "Player cannot pass through the terrain floor");
            game.Player.Teleport(new Vector3(8.5f, 33.08f, 8.5f));

            stage = "buckets and live fluid updates";
            game.Player.IsCreative = false;
            game.SetDifficulty(2);
            game.Player.Inventory = new Inventory();
            game.Player.Inventory.Slots[0] = new ItemStack(Items.WaterBucket);
            Cell water = new Cell(5, 33, 11);
            Require(game.Use(true, water.Down, water), "Water bucket uses a terrain face");
            Require(game.World.Get(water).Equals(new Voxel(Block.Water)), "Bucket places a water source");
            Require(game.Player.Inventory.Held?.Id == Items.EmptyBucket, "Placing water returns an empty bucket");
            yield return Until(() => game.World.GetBlock(water + new Cell(1, 0, 0)) == Block.Water, 4, "Live session advances fluid flow");
            Require(game.World.Get(water + new Cell(1, 0, 0)).Level > 0, "Spread water is flowing, not another source");
            Require(game.Use(true, water + new Cell(1, 0, 0), water), "Bucket interaction handles flowing water");
            Require(game.Player.Inventory.Held?.Id == Items.EmptyBucket, "Empty bucket cannot collect flowing water");
            Require(game.Use(true, water, water.Up), "Bucket collects the source");
            Require(game.Player.Inventory.Count(Items.WaterBucket) == 1 && game.World.GetBlock(water) == Block.Air, "Collecting source exchanges world water for exactly one filled bucket");
            yield return Until(() => game.World.GetBlock(water + new Cell(1, 0, 0)) == Block.Air, 5, "Removing the source drains its live flow");

            stage = "health, drops and automatic respawn";
            game.Player.Inventory = new Inventory();
            game.Player.Inventory.Slots[0] = new ItemStack(Items.GoldIngot, 7);
            game.Player.Inventory.Slots[1] = new ItemStack(Items.IronSword, 1, 87);
            yield return RealSeconds(.6f);
            float health = game.Player.Health;
            game.Player.Damage(4, game.Player.transform.position);
            Require(game.Player.Health <= health - 3.99f, "Survival damage decreases actual player health");
            yield return RealSeconds(.6f);
            game.Player.Damage(100, game.Player.transform.position);
            Require(game.Player.Dead && game.DeathRemaining > 2.8f, "Lethal damage starts the three-second death countdown");
            Require(game.Player.Inventory.Slots.All(s => s == null || s.Empty), "Death removes carried inventory");
            Require(DroppedCount(Items.GoldIngot) == 7, "Death creates real dropped gold items");
            Require(UnityEngine.Object.FindObjectsByType<DroppedItem>(FindObjectsSortMode.None).Any(d => d.Stack != null && d.Stack.Id == Items.IronSword && d.Stack.Durability == 87), "Death drops retain tool durability");
            yield return RealSeconds(1);
            Require(game.Player.Dead, "Respawn does not happen before its countdown");
            yield return Until(() => !game.Player.Dead, 5, "Death respawns automatically without a button");
            Require(Mathf.Abs(game.Player.Health - 20) < .01f && game.DeathRemaining == 0, "Automatic respawn restores health and clears the countdown");
            Require(Vector3.Distance(game.Player.transform.position, new Vector3(8.5f, 33.1f, 8.5f)) < 2, "Missing bed defaults to world spawn");

            stage = "projectile combat and animal loot";
            game.Player.IsCreative = true;
            game.Player.Teleport(new Vector3(8.5f, 33.08f, 8.5f));
            MobActor cow = game.Mobs.Spawn(MobKind.Cow, new Vector3(8.5f, 33.03f, 12.5f));
            Require(cow != null, "A cow can spawn on generated collision terrain");
            int beefBeforeShot = DroppedCount(Items.RawBeef);
            Vector3 arrowOrigin = new Vector3(8.5f, 34, 9.2f);
            game.Mobs.ShootArrow(arrowOrigin, (cow.HitBounds.center - arrowOrigin).normalized, 1);
            Require(UnityEngine.Object.FindObjectsByType<MobProjectile>(FindObjectsSortMode.None).Length > 0, "Shooting creates a live arrow projectile");
            yield return Until(() => cow == null || cow.Dead, 3, "Arrow collision damages and kills the cow");
            Require(DroppedCount(Items.RawBeef) > beefBeforeShot, "Killing an animal creates new meat drops in the world");
            MobActor enderman = game.Mobs.Spawn(MobKind.Enderman, new Vector3(12.5f, 33.03f, 12.5f));
            Require(enderman != null && enderman.Health == 40, "Enderman has its native actor and health");
            Require(enderman.Hurt(1, game.Player.transform.position) && enderman.Anger > 0, "Attacking an Enderman makes it hostile");
            enderman.Hurt(100, game.Player.transform.position, false, true);

            stage = "native mob spawner";
            FlatPlatform(16, 24, 16, 24, 32);
            for (int z = 16; z <= 24; z++)
            for (int x = 16; x <= 24; x++)
            {
                game.World.Set(new Cell(x, 36, z), Block.Stone);
                if (x == 16 || x == 24 || z == 16 || z == 24)
                    for (int y = 33; y <= 35; y++) game.World.Set(new Cell(x, y, z), Block.Stone);
            }
            Cell spawner = new Cell(20, 33, 20);
            var actorsBeforeSpawner = new HashSet<MobActor>(game.Mobs.Actors);
            game.World.Set(spawner, Block.Spawner);
            game.Renderer.EnsureImmediate(new Vector3(18.5f, 33.08f, 18.5f));
            game.Player.Teleport(new Vector3(18.5f, 33.08f, 18.5f));
            yield return Until(() => game.Mobs.Actors.Any(a => a != null && !actorsBeforeSpawner.Contains(a) && !a.Dead && a.Kind == MobKind.Zombie && Vector3.Distance(a.transform.position, MobDirector.Position(spawner)) < 8), 8, "A dark nearby spawner creates a new zombie through the session update");
            game.World.Set(spawner, Block.Air);
            var registry = Field<IDictionary>(game.Mobs, "spawners");
            Require(!registry.Contains(spawner), "Breaking a spawner unregisters it immediately");
            game.Mobs.Clear();
            game.Mobs.Init(game);

            stage = "beds and sleeping";
            game.Player.Teleport(new Vector3(8.5f, 33.08f, 6.5f));
            game.Renderer.EnsureImmediate(game.Player.transform.position);
            game.Player.IsCreative = false;
            game.Player.Inventory = new Inventory();
            game.Player.Inventory.Slots[0] = new ItemStack((int)Block.Bed);
            Cell bed = new Cell(5, 33, 5);
            Require(game.Use(true, bed.Down, bed), "A bed can be placed on supported ground");
            Require(game.World.GetBlock(bed) == Block.Bed && game.World.GetBlock(bed + new Cell(0, 0, 1)) == Block.BedHead, "Bed placement creates both halves");
            SessionState.Day = .9f;
            Require(game.Use(true, bed, bed.Up) && game.Sleeping, "Using a bed at night enters sleep");
            yield return Until(() => !game.Sleeping, 6, "Sleeping completes through the session update");
            Require(game.TimeOfDay >= .27f && game.TimeOfDay < .29f, "Sleeping advances night to dawn");
            Require(SessionState.HasBed && SessionState.BedX == bed.X && SessionState.BedZ == bed.Z, "Sleeping records the bed respawn position");
            game.Player.Teleport(new Vector3(12.5f, 33.08f, 12.5f));
            yield return RealSeconds(1.1f);
            game.Player.Damage(100, game.Player.transform.position);
            Require(game.Player.Dead, "Bed respawn is tested after actual lethal damage");
            yield return Until(() => !game.Player.Dead, 5, "Bed respawn is automatic");
            Require(Vector2.Distance(new Vector2(game.Player.transform.position.x, game.Player.transform.position.z), new Vector2(5.5f, 5.5f)) < 3, "Death returns the player to the bed area");
            game.BreakBlock(bed);
            Require(game.World.GetBlock(bed) == Block.Air && game.World.GetBlock(bed + new Cell(0, 0, 1)) == Block.Air, "Breaking a bed removes both halves");
            game.Player.Teleport(new Vector3(12.5f, 33.08f, 12.5f));
            yield return RealSeconds(1.1f);
            game.Player.Damage(100, game.Player.transform.position);
            Require(game.Player.Dead, "Missing-bed fallback is tested after actual lethal damage");
            yield return Until(() => !game.Player.Dead, 5, "Missing-bed death still respawns automatically");
            Require(!SessionState.HasBed && Vector3.Distance(game.Player.transform.position, new Vector3(8.5f, 33.1f, 8.5f)) < 2, "Destroyed bed falls back safely to world spawn");

            stage = "save and load round-trip";
            game.Player.IsCreative = true;
            game.Player.Inventory = new Inventory();
            game.Player.Inventory.Selected = 3;
            game.Player.Inventory.Slots[3] = new ItemStack(Items.IronSword, 1, 87);
            game.Player.Inventory.Slots[12] = new ItemStack(Items.Crystal, 11);
            Cell overworldEdit = new Cell(12, 35, 12);
            game.World.Set(overworldEdit, Block.GoldBlock);
            game.Hud.OpenCrafting();
            ItemStack[] openGrid = Field<ItemStack[]>(game.Hud, "grid");
            openGrid[0] = new ItemStack((int)Block.Planks, 3);
            openGrid[8] = new ItemStack(Items.IronIngot, 5);
            var openCursor = new ItemStack((int)Block.Wool, 4);
            SetField(game.Hud, "cursor", openCursor);
            Require(game.Player.Inventory.Count((int)Block.Planks) == 0 && game.Player.Inventory.Count(Items.IronIngot) == 0 && game.Player.Inventory.Count((int)Block.Wool) == 0, "Transient crafting and cursor items are outside the carried inventory");
            Require(game.SaveWorld() && game.LastSaveError == null, "Session saves successfully");
            Require(game.Hud.IsOpen && game.Hud.CaptureTransient().Length == 3, "Saving while crafting leaves the live grid and cursor intact");
            Require(SessionState.TransientItems != null && SessionState.TransientItems.Length == 3 && SessionState.TransientItems.All(s => !ReferenceEquals(s, openCursor) && !ReferenceEquals(s, openGrid[0]) && !ReferenceEquals(s, openGrid[8])), "Transient save entries are independent clones");
            string saveFile = Field<string>(game, "savePath");
            Require(File.Exists(saveFile) && new FileInfo(saveFile).Length > 100, "Native save file exists and contains data");
            World previousWorld = game.World;
            game.World.Set(overworldEdit, Block.Air);
            game.Player.Inventory.Slots[12] = null;
            openGrid[0].Count = 1;
            openCursor.Count = 1;
            Require(SessionState.TransientItems.First(s => s.Id == (int)Block.Planks).Count == 3 && SessionState.TransientItems.First(s => s.Id == (int)Block.Wool).Count == 4, "Changing live crafting items cannot mutate an already captured save snapshot");
            game.LoadWorld(saveFile);
            game.SetPaused(false);
            Require(!ReferenceEquals(previousWorld, game.World), "LoadWorld reconstructs a fresh world instance");
            Require(game.World.GetBlock(overworldEdit) == Block.GoldBlock, "Block edits survive disk save and reload");
            Require(game.Player.Inventory.Selected == 3 && game.Player.Inventory.Held?.Id == Items.IronSword && game.Player.Inventory.Held.Durability == 87, "Selected tool and durability survive disk reload");
            Require(game.Player.Inventory.Count(Items.Crystal) == 11, "Inventory quantities survive disk reload");
            Require(game.Player.Inventory.Count((int)Block.Planks) == 3 && game.Player.Inventory.Count(Items.IronIngot) == 5 && game.Player.Inventory.Count((int)Block.Wool) == 4, "Disk reload restores every crafting and cursor item exactly once");
            Require(!game.Hud.IsOpen && Field<ItemStack>(game.Hud, "cursor") == null && openGrid.All(s => s == null) && game.Hud.CaptureTransient().Length == 0, "Reload clears the original cursor and crafting grid");
            Require(game.SaveWorld(), "Recovered transient items can be saved back into the normal inventory");
            previousWorld = game.World;
            game.LoadWorld(saveFile);
            Require(!ReferenceEquals(previousWorld, game.World), "Second transient-item reload actually reconstructs the world");
            Require(game.Player.Inventory.Count((int)Block.Planks) == 3 && game.Player.Inventory.Count(Items.IronIngot) == 5 && game.Player.Inventory.Count((int)Block.Wool) == 4, "A second save and reload neither loses nor duplicates restored transient items");

            stage = "Nether portal frames and activation";
            var sourcePortal = new PortalFrame(new Cell(20, 60, 20), 5, 7, false);
            for (int y = 0; y <= sourcePortal.Height + 1; y++)
            for (int x = 0; x <= sourcePortal.Width + 1; x++)
                game.World.Set(sourcePortal.At(x, y), x == 0 || x == sourcePortal.Width + 1 || y == 0 || y == sourcePortal.Height + 1 ? Block.Obsidian : Block.Air);
            game.Player.IsCreative = false;
            game.Player.Inventory.Slots[0] = new ItemStack(Items.FlintSteel);
            game.Player.Inventory.Selected = 0;
            int ignitionDurability = game.Player.Inventory.Held.Durability;
            Require(game.Use(true, sourcePortal.At(1, 0), sourcePortal.At(1, 1)), "Flint and steel ignites a larger Z-axis frame from its inner face");
            Require(PortalRules.IsLit(game.World, sourcePortal), "The larger rectangle fills all thirty-five interior cells");
            Require(game.Player.Inventory.Held.Durability == ignitionDurability - 1, "Successful portal ignition wears flint and steel once");
            game.Use(true, sourcePortal.At(1, 0), sourcePortal.At(1, 1));
            Require(game.Player.Inventory.Held.Durability == ignitionDurability - 1, "An already lit portal does not consume tool durability");
            game.BreakBlock(sourcePortal.At(0, 0));
            Require(PortalRules.IsLit(game.World, sourcePortal), "Removing an optional corner does not destroy an active portal");
            game.BreakBlock(sourcePortal.At(0, 4));
            Require(sourcePortal.Interior.All(p => game.World.GetBlock(p) == Block.Air), "Breaking a required side removes the entire tall portal");
            game.World.Set(sourcePortal.At(0, 4), Block.Obsidian);
            Require(game.Use(true, sourcePortal.At(1, 0), sourcePortal.At(1, 1)), "Repairing a frame allows it to be lit again without its corner");
            game.Player.Inventory.Selected=2;
            game.Player.Inventory.Slots[2]=new ItemStack((int)Block.Stone);
            Require(game.Use(true,sourcePortal.At(0,4),sourcePortal.At(1,4)), "A placed solid block can replace an active portal surface");
            yield return Frames(2);
            Require(sourcePortal.Interior.All(p => !PortalRules.IsPortal(game.World.GetBlock(p))), "Placing an obstruction extinguishes the portal through normal world updates");
            game.World.Set(sourcePortal.At(1, 4), Block.Air);
            game.Player.Inventory.Selected=0;
            Require(game.Use(true, sourcePortal.At(1, 0), sourcePortal.At(1, 1)), "Removing an interior obstruction allows relighting");
            game.Player.Inventory.Slots[2]=new ItemStack(Items.WaterBucket);
            game.Player.Inventory.Selected=2;
            Require(game.Use(true,sourcePortal.At(1,0),sourcePortal.At(1,1)), "A water bucket can replace an active portal surface");
            Require(game.SaveWorld()&&sourcePortal.Interior.All(p=>!PortalRules.IsPortal(game.World.GetBlock(p))), "Saving immediately after a fluid obstruction flushes invalid portal cells");
            game.World.Set(sourcePortal.At(1,1),Block.Air);
            game.Player.Inventory.Selected=0;
            Require(game.Use(true,sourcePortal.At(1,0),sourcePortal.At(1,1)), "Removing the water source allows the portal to be relit");
            game.Player.IsCreative = true;
            game.Player.Inventory.Selected = 3;
            game.Player.Teleport(new Vector3(20.5f, 61.08f, 23.5f));
            int sourcePortalCells = game.World.Edits.Count(e => PortalRules.IsPortal(e.Value.Id));

            stage = "Nether dimension persistence";
            game.Player.IsCreative=false;
            var portalCheck=typeof(GameSession).GetMethod("CheckPortal",BindingFlags.Instance|BindingFlags.NonPublic);
            var cooldownField=typeof(GameSession).GetField("portalCooldown",BindingFlags.Instance|BindingFlags.NonPublic);
            cooldownField.SetValue(game,0f);
            portalCheck.Invoke(game,new object[]{3.9f});
            Require(game.World.Dimension==Dimension.Overworld,"Survival portal contact waits until four seconds instead of transferring immediately");
            float transferStarted=Time.realtimeSinceStartup;
            portalCheck.Invoke(game,new object[]{.11f});
            Require(Time.realtimeSinceStartup-transferStarted<10,"First-time portal arrival search completes within ten seconds");
            Require(game.World.Dimension == Dimension.Nether, "Transfer loads the Nether");
            Require(game.Player.transform.position.y < 80 && game.Player.transform.position.y > 0, "Nether landing is below its bedrock ceiling");
            Require(PortalRules.TryFind(game.World, PlayerController.ToCell(game.Player.transform.position), out var arrivalPortal) && PortalRules.IsLit(game.World, arrivalPortal), "Nether arrival places the player inside a valid supported return portal");
            Require(!arrivalPortal.AlongX, "Generated destination portal preserves the source portal axis");
            portalCheck.Invoke(game,new object[]{10f});
            Require(game.World.Dimension==Dimension.Nether,"Arrival cooldown prevents immediately bouncing back while still in the portal");
            Cell netherEdit = new Cell(7, 70, 7);
            game.World.Set(netherEdit, Block.GoldBlock);
            game.Player.Inventory.Slots[0] = new ItemStack(Items.WaterBucket);
            game.Player.Inventory.Selected = 0;
            game.Player.IsCreative = false;
            Cell netherWater = PlayerController.ToCell(game.Player.transform.position) + new Cell(2, 0, 0);
            Require(game.Use(true, netherWater.Down, netherWater), "Water bucket use is handled in the Nether");
            Require(game.Player.Inventory.Held?.Id == Items.EmptyBucket && game.World.GetBlock(netherWater) != Block.Water, "Nether water evaporates and returns the bucket");
            game.Player.IsCreative = true;
            yield return Frames(2);
            yield return Capture("02-nether.png");
            game.Transfer(Dimension.Overworld);
            Require(game.World.GetBlock(overworldEdit) == Block.GoldBlock, "Returning from the Nether retains Overworld edits");
            Require(PortalRules.IsLit(game.World, sourcePortal) && game.World.Edits.Count(e => PortalRules.IsPortal(e.Value.Id)) == sourcePortalCells, "Return travel reuses the existing portal instead of creating another frame");
            Require(Mathf.Abs(game.Player.transform.position.x - 20.5f) < .01f && Mathf.Abs(game.Player.transform.position.z - 23.5f) < .01f, "Return travel lands in the original large portal");

            stage = "End crystals and dragon combat";
            game.Transfer(Dimension.End);
            Require(game.World.Dimension == Dimension.End, "Transfer loads the End");
            LoadEndPillars();
            yield return Until(() => game.Mobs.DragonHealth > 0 && game.Mobs.Actors.Count(a => a != null && !a.Dead && a.Kind == MobKind.EndCrystal) == 8, 5, "End markers spawn one dragon and all eight crystal actors");
            MobActor dragon = game.Mobs.Actors.First(a => a.Kind == MobKind.EndDragon && !a.Dead);
            MobActor crystal = game.Mobs.Actors.First(a => a.Kind == MobKind.EndCrystal && !a.Dead);
            Require(Mathf.Abs(dragon.Health - 200) < .01f, "The End dragon starts with 200 health");
            dragon.transform.position = crystal.transform.position + new Vector3(0, 5, 10);
            Require(dragon.Hurt(10, game.Player.transform.position), "Dragon accepts combat damage");
            float damagedHealth = dragon.Health;
            yield return Until(() => dragon.Health > damagedHealth + .15f, 3, "Nearby End crystals heal the dragon during live updates");
            FacePoint(dragon.transform.position + Vector3.up * 2);
            yield return Capture("03-end-dragon.png");
            dragon.transform.position = new Vector3(.5f, 75, .5f);
            foreach (MobActor actor in game.Mobs.Actors.Where(a => a != null && !a.Dead && a.Kind == MobKind.EndCrystal).ToArray())
                Require(actor.Hurt(10, game.Player.transform.position), "Crystal can be destroyed at " + actor.transform.position);
            Require(game.Mobs.Actors.All(a => a == null || a.Dead || a.Kind != MobKind.EndCrystal), "All eight crystals are destroyed");
            float withoutCrystals = dragon.Health;
            yield return RealSeconds(.6f);
            Require(dragon.Health <= withoutCrystals + .01f, "The dragon stops regenerating after its crystals are destroyed");
            Require(dragon.Hurt(500, game.Player.transform.position), "Dragon can take a lethal final hit");
            Require(game.EndDragonDefeated && game.Mobs.DragonHealth == 0, "Dragon death records victory and unlocks the exit");
            Cell endEdit = new Cell(9, 50, 9);
            game.World.Set(endEdit, Block.GoldBlock);
            Require(game.SaveWorld(), "Defeated End dimension saves");
            game.Transfer(Dimension.Overworld);
            game.Transfer(Dimension.Nether);
            Require(game.World.GetBlock(netherEdit) == Block.GoldBlock, "Nether block edits survive round-trip transfers");
            game.Transfer(Dimension.End);
            Require(game.World.GetBlock(endEdit) == Block.GoldBlock && game.EndDragonDefeated, "End edits and victory survive round-trip transfers");
            LoadEndPillars();
            yield return Frames(3);
            Require(game.Mobs.DragonHealth == 0 && game.Mobs.Actors.All(a => a.Kind != MobKind.EndCrystal), "Defeated dragon and crystals do not respawn on return");
            Require(game.SaveWorld(), "Final smoke state saves successfully");
            previousWorld = game.World;
            game.World.Set(endEdit, Block.Air);
            game.LoadWorld(saveFile);
            Require(!ReferenceEquals(previousWorld, game.World), "Final disk reload actually reconstructs the saved End");
            Require(game.World.Dimension == Dimension.End && game.EndDragonDefeated && game.World.GetBlock(endEdit) == Block.GoldBlock, "Disk reload preserves dimension, End edits and defeated dragon");
            game.Transfer(Dimension.Overworld);
            Require(game.World.GetBlock(overworldEdit) == Block.GoldBlock && game.Player.Inventory.Count(Items.Crystal) == 11, "Cross-dimension disk reload retains original edits and inventory");
            game.SetPaused(true);
            Require(game.Paused && !game.Playing, "Pause halts the live session");
            game.SetPaused(false);
            Require(game.Playing, "Unpause resumes the live session");
            yield return Capture("04-final-overworld.png");
        }

        private SessionSave SessionState => Field<SessionSave>(game, "save");
        private static T Field<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            return (T)field.GetValue(target);
        }
        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }
        private static int DroppedCount(int id) => UnityEngine.Object.FindObjectsByType<DroppedItem>(FindObjectsSortMode.None).Where(d => d.Stack != null && d.Stack.Id == id).Sum(d => d.Stack.Count);
        private void LoadEndPillars()
        {
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                int x = (int)Math.Round(Math.Cos(angle) * 48), z = (int)Math.Round(Math.Sin(angle) * 48);
                game.World.EnsureChunk(World.FloorDiv(x, 16), World.FloorDiv(z, 16));
            }
            Require(game.World.Markers.Count(m => m.Kind == "crystal") == 8, "All crystal marker chunks are loaded for the actor assertions");
        }
        private void FlatPlatform(int minX, int maxX, int minZ, int maxZ, int floor)
        {
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                game.World.Set(new Cell(x, floor, z), Block.Stone);
                for (int y = floor + 1; y <= floor + 5; y++) game.World.Set(new Cell(x, y, z), Block.Air);
            }
        }
        private void FacePoint(Vector3 position)
        {
            Vector3 direction = (position - game.Player.Eye.transform.position).normalized;
            game.Player.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0);
            game.Player.Pitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1, 1)) * Mathf.Rad2Deg;
            game.Player.Eye.transform.localRotation = Quaternion.Euler(game.Player.Pitch, 0, 0);
        }
        private IEnumerator Until(Func<bool> condition, float timeout, string description)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) throw new TimeoutException(description);
                yield return null;
            }
            Require(true, description);
        }
        private static IEnumerator RealSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        private IEnumerator Capture(string name)
        {
            yield return null;
            Require(Screen.width >= 320 && Screen.height >= 240, "Screenshot has a usable graphical viewport");
            int width = Screen.width, height = Screen.height;
            Camera camera = game.Player.Eye;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            Quaternion previousRotation = camera.transform.localRotation;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                if (game.World.Dimension != Dimension.End) camera.transform.localRotation = Quaternion.Euler(18, 0, 0);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                image.Apply(false);
                Color32[] pixels = image.GetPixels32();
                var colors = new HashSet<int>();
                int magenta = 0, samples = 0;
                for (int y = height / 8; y < height * 7 / 8; y += 17)
                for (int x = width / 8; x < width * 7 / 8; x += 17)
                {
                    Color32 p = pixels[y * width + x];
                    colors.Add(((p.r >> 3) << 10) | ((p.g >> 3) << 5) | (p.b >> 3));
                    if (p.r > 180 && p.b > 180 && p.g < 60) magenta++;
                    samples++;
                }
                Require(colors.Count > 12, name + " contains rendered scene detail, not a flat blank frame");
                Require(magenta < samples / 3, name + " is not dominated by missing-shader magenta");
                if (artifactDirectory != null) File.WriteAllBytes(Path.Combine(artifactDirectory, name), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.transform.localRotation = previousRotation;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Destroy(image);
            }
        }
        private void ValidateDirectories()
        {
            string argument = GameSession.Argument("-voxel-saves");
            if (string.IsNullOrWhiteSpace(argument) || argument.StartsWith("-", StringComparison.Ordinal)) throw new InvalidOperationException("Smoke tests require an explicit fresh -voxel-saves directory.");
            string project = FindProjectRoot();
            string path = Path.GetFullPath(argument);
            string cache = Path.Combine(project, ".cache") + Path.DirectorySeparatorChar;
            if (!path.StartsWith(cache, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Smoke saves must be inside Game/.cache, never the user saves directory.");
            if (!string.Equals(path.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(game.SaveDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Game did not accept the isolated save directory.");
            if (Directory.EnumerateFiles(path, "*.vws", SearchOption.AllDirectories).Any()) throw new InvalidOperationException("Use a fresh smoke save directory without existing worlds.");
            string artifact = GameSession.Argument("-voxel-artifacts");
            if (!string.IsNullOrWhiteSpace(artifact))
            {
                artifactDirectory = Path.GetFullPath(artifact);
                string artifactRoot = Path.Combine(project, "artifacts") + Path.DirectorySeparatorChar;
                if (!artifactDirectory.StartsWith(artifactRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Smoke screenshots must stay under Game/artifacts.");
                Directory.CreateDirectory(artifactDirectory);
            }
            Require(true, "Save and screenshot directories are isolated inside the Game project");
        }
        private static string FindProjectRoot()
        {
            foreach (string start in new[] { Application.dataPath, Directory.GetCurrentDirectory() })
                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                    if (Directory.Exists(Path.Combine(directory.FullName, "Assets", "Scripts")) && Directory.Exists(Path.Combine(directory.FullName, "ProjectSettings"))) return directory.FullName;
            throw new DirectoryNotFoundException("Run the smoke executable inside its Game project so output directories can be verified.");
        }
        private void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            checks.Add(description);
            Debug.Log("VOXEL_SMOKE_CHECK " + description);
        }
        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!finished && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)) Fail("Runtime error during " + stage + ": " + condition + "\n" + stackTrace);
        }
        private void Finish()
        {
            if (finished) return;
            float elapsed = Time.realtimeSinceStartup - started;
            string summary = "VOXEL_SMOKE_PASS " + checks.Count + " checks in " + elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " seconds";
            try { if (artifactDirectory != null) File.WriteAllText(Path.Combine(artifactDirectory, "result.txt"), summary + Environment.NewLine + string.Join(Environment.NewLine, checks)); }
            catch (Exception error) { Fail("Could not write final smoke report: " + error); return; }
            finished = true;
            Application.logMessageReceived -= OnLog;
            Debug.Log(summary);
            Exit(0);
        }
        private void Fail(string message)
        {
            if (finished) return;
            finished = true;
            Application.logMessageReceived -= OnLog;
            string report = "VOXEL_SMOKE_FAIL " + message;
            try { if (artifactDirectory != null) File.WriteAllText(Path.Combine(artifactDirectory, "result.txt"), report + Environment.NewLine + string.Join(Environment.NewLine, checks)); }
            catch (Exception error) { Debug.LogWarning("Could not write smoke report: " + error.Message); }
            Debug.LogError(report);
            Exit(1);
        }
        private static void Exit(int code)
        {
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(code);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
