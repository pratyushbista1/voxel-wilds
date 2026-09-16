using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class InteractionSmoke : MonoBehaviour
    {
        private GameSession game;
        private string artifactDirectory;
        private string stage = "initialization";
        private float started;
        private bool finished;
        private readonly List<string> checks = new List<string>();
        private Color32[] lastImage;

        private void Start()
        {
            started = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            StartCoroutine(Guarded(Exercise()));
        }

        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > 140)
                Fail("140-second watchdog expired during " + stage);
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
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
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
            Require(game != null && game.Player != null && game.Renderer != null, "Native scene creates its interaction components");
            ValidateDirectories();
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Interaction checks have a real graphics device");
            UnityEngine.Random.InitState(902);
            GraphicsOptions.SetPreset(game.Settings, 2);
            game.Settings.Fullscreen = false;
            game.Settings.Fps = 60;
            game.Settings.VSync = false;
            game.Settings.Clouds = false;
            game.Settings.AnimatedWater = false;
            game.Settings.Bobbing = false;
            game.ApplySettings();
            game.NewWorld("Interaction check", 902, true);
            Freeze();
            BuildFixture();
            game.Player.Teleport(new Vector3(7.5f, 33.08f, 3.5f));
            FacePoint(new Vector3(7.5f, 34.1f, 12));
            var state = Field<SessionSave>(game, "save");
            state.Day = .55f;
            typeof(GameSession).GetMethod("UpdateSky", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            game.Renderer.EnsureImmediate(game.Player.transform.position);
            for (int i = 0; i < 12; i++) { game.Renderer.Tick(game.Player.transform.position, 3); yield return null; }
            yield return FirstPersonChecks();
            yield return DoorChecks();
            yield return MobChecks();
            Require(game.SaveWorld() && game.LastSaveError == null, "Interaction fixtures save without errors");
        }

        private void Freeze()
        {
            game.SetPaused(true);
            game.Player.enabled = false;
            game.Mobs.enabled = false;
            game.Hud.enabled = false;
        }

        private void BuildFixture()
        {
            for (int z = -2; z <= 18; z++)
            for (int x = -2; x <= 18; x++)
            {
                game.World.Set(new Cell(x, 32, z), Block.Grass);
                for (int y = 33; y <= 42; y++) game.World.Set(new Cell(x, y, z), Block.Air);
            }
            for (int x = 4; x <= 10; x++)
            for (int y = 33; y <= 36; y++)
                if (x != 7 || y > 34) game.World.Set(new Cell(x, y, 8), y == 36 ? Block.Log : Block.Planks);
            for (int z = 9; z <= 12; z++)
            {
                for (int y = 33; y <= 35; y++)
                {
                    game.World.Set(new Cell(4, y, z), Block.Planks);
                    game.World.Set(new Cell(10, y, z), Block.Planks);
                }
                for (int x = 4; x <= 10; x++) game.World.Set(new Cell(x, 36, z), Block.Planks);
            }
            for (int x = 4; x <= 10; x++)
            for (int y = 33; y <= 35; y++) game.World.Set(new Cell(x, y, 12), Block.Planks);
            game.World.Set(new Cell(5, 34, 8), Block.Glass);
            game.World.Set(new Cell(9, 34, 8), Block.Glass);
        }

        private IEnumerator FirstPersonChecks()
        {
            stage = "first-person motion";
            FirstPersonView view = game.Player.View;
            Require(view != null && view.Arm != null, "The player owns a modeled first-person arm");
            Shader shader = Shader.Find("VoxelWilds/FirstPerson");
            Require(shader != null && shader.isSupported, "The first-person shader compiles on the native graphics device");
            game.Player.Inventory.Selected = 0;
            game.Player.Inventory.Slots[0] = null;
            for (int i = 0; i < 30; i++) Advance(view);
            view.ResetMotion();
            Transform pivot = view.Arm.parent;
            Require(view.PresentedItem == 0 && view.HandVisible, "An empty hotbar slot still presents the player's bare hand and sleeve");
            Require(view.Arm.GetComponentsInChildren<Renderer>().Length >= 3, "The arm has distinct hand, forearm and sleeve geometry");
            Require(pivot.GetComponentsInChildren<Collider>().All(c => !c.enabled), "The camera-mounted arm cannot collide with the player or world");
            pivot.gameObject.SetActive(false);
            yield return Capture("00-hand-hidden-baseline.png");
            Color32[] withoutHand = lastImage;
            pivot.gameObject.SetActive(true);
            yield return Capture("01-empty-hand.png");
            Color32[] bareHand = lastImage;
            Require(ChangedPixels(withoutHand, bareHand, true) > 700, "The empty hand is visibly rendered in the main camera's lower-right view");

            game.Player.Inventory.Slots[0] = new ItemStack((int)Block.Planks, 4);
            Advance(view);
            Require(view.EquipProgress < 1 && view.PresentedItem == 0, "Equipping starts by lowering the current hand instead of popping the item into view");
            for (int i = 0; i < 30; i++) Advance(view);
            Require(view.PresentedItem == (int)Block.Planks && view.HeldVisual != null && view.EquipProgress > .99f && view.HandVisible,
                "Equipping a block finishes with both the held block and the arm visible");
            yield return Capture("02-held-block.png");
            Require(ChangedPixels(bareHand, lastImage, true) > 300, "A held textured block is visibly distinct from an empty hand");

            game.Player.Inventory.Slots[0] = new ItemStack(Items.IronPickaxe);
            for (int i = 0; i < 30; i++) Advance(view);
            Require(view.PresentedItem == Items.IronPickaxe && view.HeldVisual.GetComponentsInChildren<MeshFilter>().Length > 1,
                "A pickaxe equips the detailed Blender tool rather than a placeholder cube");
            yield return Capture("03-held-pickaxe.png");
            Require(ChangedPixels(bareHand, lastImage, true) > 300, "The held Blender pickaxe occupies visible pixels in the main camera");

            stage = "repeatable swing cycles";
            int completed = view.CompletedSwings;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < 150; i++)
            {
                view.TriggerSwing(FirstPersonView.Action.Mine);
                Advance(view, 1f / 60);
                minX = Mathf.Min(minX, view.HandLocalPosition.x);
                maxX = Mathf.Max(maxX, view.HandLocalPosition.x);
            }
            Require(view.CompletedSwings - completed >= 7, "Sustained mining completes repeated swing cycles instead of freezing in one pose");
            Require(maxX - minX > .08f, "Repeated mining moves through a visible swing arc");
            view.ResetMotion();
            Vector3 restPosition = view.HandLocalPosition;
            Quaternion restRotation = view.HandLocalRotation;
            view.TriggerSwing(FirstPersonView.Action.Attack);
            Advance(view, .07f);
            yield return Capture("08-attack-windup.png");
            Advance(view, .09f);
            yield return Capture("09-attack-strike.png");
            Advance(view, .09f);
            yield return Capture("10-attack-recovery.png");
            for (int i = 0; i < 30; i++) Advance(view);
            Require(!view.IsSwinging && Vector3.Distance(restPosition, view.HandLocalPosition) < .001f
                && Quaternion.Angle(restRotation, view.HandLocalRotation) < .1f, "An attack returns precisely to the neutral hand pose");

            stage = "view bobbing and teleport reset";
            MethodInfo animate = typeof(PlayerController).GetMethod("AnimateHand", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(animate != null, "Bobbing checks exercise the player's normal animation path");
            game.Settings.Bobbing = true;
            float minimumEye = float.MaxValue, maximumEye = float.MinValue;
            for (int i = 0; i < 100; i++)
            {
                animate.Invoke(game.Player, new object[] { .02f, .075f, true, Vector2.zero, false });
                minimumEye = Mathf.Min(minimumEye, game.Player.Eye.transform.localPosition.y);
                maximumEye = Mathf.Max(maximumEye, game.Player.Eye.transform.localPosition.y);
            }
            Require(maximumEye - minimumEye > .003f && maximumEye - minimumEye < .09f, "Enabled view bobbing produces restrained motion while walking");
            game.Settings.Bobbing = false;
            for (int i = 0; i < 100; i++) animate.Invoke(game.Player, new object[] { .02f, .075f, true, Vector2.zero, false });
            Vector3 stableEye = game.Player.Eye.transform.localPosition, stableHand = view.HandLocalPosition;
            for (int i = 0; i < 30; i++) animate.Invoke(game.Player, new object[] { .02f, .075f, true, Vector2.zero, false });
            Require(Vector3.Distance(stableEye, game.Player.Eye.transform.localPosition) < .001f
                && Vector3.Distance(stableHand, view.HandLocalPosition) < .001f, "Disabling view bobbing settles both the camera and the hand even while moving");
            view.TriggerSwing(FirstPersonView.Action.Attack);
            Advance(view, .1f);
            game.Player.Teleport(new Vector3(7.5f, 33.08f, 3.5f));
            Require(!view.IsSwinging && Vector3.Distance(game.Player.Eye.transform.localPosition, new Vector3(0, 1.62f, 0)) < .001f,
                "Teleport and respawn reset unfinished swings and camera bobbing");
            game.Player.Inventory.Slots[0] = null;
            for (int i = 0; i < 30; i++) Advance(view);
            Require(view.PresentedItem == 0 && view.HeldVisual == null && view.HandVisible, "Unequipping removes only the item and keeps the character's hand");
        }

        private static void Advance(FirstPersonView view, float dt = .02f) => view.Advance(dt, 0, false, Vector2.zero, false, 0, false);

        private static int ChangedPixels(Color32[] a, Color32[] b, bool lowerRight)
        {
            int changed = 0;
            for (int y = 0; y < (lowerRight ? 420 : 720); y++)
            for (int x = lowerRight ? 640 : 0; x < 1280; x++)
            {
                int index = y * 1280 + x;
                if (Math.Abs(a[index].r - b[index].r) + Math.Abs(a[index].g - b[index].g) + Math.Abs(a[index].b - b[index].b) > 24) changed++;
            }
            return changed;
        }

        private IEnumerator DoorChecks()
        {
            stage = "door placement and interaction";
            Cell foot = new Cell(7, 33, 8);
            game.Player.Teleport(new Vector3(7.5f, 33.08f, 5.5f));
            game.Player.transform.rotation = Quaternion.identity;
            FacePoint(new Vector3(7.5f, 34, 8.5f));
            game.Player.Inventory.Selected = 0;
            game.Player.Inventory.Slots[0] = new ItemStack((int)Block.Door, 2);
            game.Player.IsCreative = false;
            Require(game.Use(true, foot.Down, foot), "Normal item use places a wooden door");
            Voxel lower = game.World.Get(foot), upper = game.World.Get(foot.Up);
            Require(lower.Id == Block.Door && upper.Id == Block.Door && DoorRules.Paired(lower, upper), "A placed door has a correctly paired lower and upper half");
            Require(!DoorRules.IsUpper(lower) && DoorRules.IsUpper(upper) && !DoorRules.IsOpen(lower), "Placed doors start closed with correct half flags");
            Require(game.Player.Inventory.Held.Count == 1, "Placing both door halves consumes exactly one inventory item");
            game.Player.Inventory.Slots[0] = null;
            game.Renderer.EnsureImmediate(game.Player.transform.position);
            yield return Capture("04-door-closed.png");
            bool alongZ = (DoorRules.Facing(lower) & 1) == 0;
            Vector3 axis = alongZ ? Vector3.forward : Vector3.right;
            Vector3 center = new Vector3(foot.X + .5f, foot.Y + .08f, foot.Z + .5f);
            Vector3 rayStart = center + Vector3.up * 1.4f - axis * 2;
            Vector3 rayEnd = center + Vector3.up * 1.4f + axis * 2;
            Require(!game.Mobs.LineClear(rayStart, rayEnd), "A closed door blocks combat line of sight through its panel");
            Require(game.Player.Trace(rayStart, axis, 4, out Cell selectedDoor, out _) && selectedDoor == foot.Up,
                "Player selection hits the thin upper panel of a closed door");
            game.Player.Teleport(center - axis * 2);
            CharacterController controller = game.Player.GetComponent<CharacterController>();
            Require(controller != null && controller.enabled, "Door collision uses the actual player CharacterController");
            for (int i = 0; i < 45; i++) controller.Move(axis * .08f + Vector3.down * .015f);
            float closedTravel = Vector3.Dot(game.Player.transform.position - center, axis);
            Require(closedTravel < .7f, "The closed door physically blocks player traversal");
            game.Player.Teleport(center - axis * 2);
            Require(game.Use(true, foot.Up, foot.Up), "Right-click interaction accepts the upper door half");
            Require(DoorRules.IsOpen(game.World.Get(foot)) && DoorRules.IsOpen(game.World.Get(foot.Up)), "Using the upper half opens both halves together");
            game.Renderer.EnsureImmediate(game.Player.transform.position);
            float openingUntil = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < openingUntil) yield return null;
            Physics.SyncTransforms();
            Require(game.Mobs.LineClear(rayStart, rayEnd), "An open doorway permits combat line of sight through its clear center");
            Require(!game.Player.Trace(rayStart, axis, 4, out selectedDoor, out _) || selectedDoor != foot && selectedDoor != foot.Up,
                "Selection passes through the clear center of an open doorway");
            Require(game.Mobs.ClearBody(center, .3f, 1.8f), "Mob body checks can pass through an open doorway");
            Bounds panel = BlockShape.Bounds(foot, game.World.Get(foot));
            Require(!game.Mobs.ClearBody(new Vector3(panel.center.x, center.y, panel.center.z), .15f, 1.8f),
                "Mob body checks still respect the thin panel of an open door");
            FacePoint(center + Vector3.up);
            yield return Capture("05-door-open.png");
            for (int i = 0; i < 45; i++) controller.Move(axis * .08f + Vector3.down * .015f);
            Require(Vector3.Dot(game.Player.transform.position - center, axis) > 1.1f, "The open door lets the same CharacterController pass through the doorway");
            game.Player.Teleport(center - axis * 2);
            Require(game.Use(true, foot, foot), "Right-click interaction accepts the lower door half");
            Require(!DoorRules.IsOpen(game.World.Get(foot)) && !DoorRules.IsOpen(game.World.Get(foot.Up)), "Using the lower half closes both halves together");
            Require(game.Use(true, foot, foot) && DoorRules.IsOpen(game.World.Get(foot)), "A closed door can be reopened normally");

            stage = "door persistence";
            byte savedLower = game.World.Get(foot).Level, savedUpper = game.World.Get(foot.Up).Level;
            Require(game.SaveWorld(), "Open door state saves to disk");
            string saveFile = Field<string>(game, "savePath");
            World previous = game.World;
            game.LoadWorld(saveFile);
            Freeze();
            Require(!ReferenceEquals(previous, game.World), "Door persistence check reconstructs a fresh world instance");
            Require(game.World.Get(foot).Id == Block.Door && game.World.Get(foot.Up).Id == Block.Door
                && game.World.Get(foot).Level == savedLower && game.World.Get(foot.Up).Level == savedUpper,
                "Door facing, open state and both half flags survive disk reload");

            stage = "door breaking and drops";
            game.Player.IsCreative = false;
            game.Player.Inventory.Slots[0] = null;
            int count = DroppedCount((int)Block.Door);
            game.BreakBlock(foot.Up);
            Require(game.World.GetBlock(foot) == Block.Air && game.World.GetBlock(foot.Up) == Block.Air, "Breaking the upper half removes the complete door");
            Require(DroppedCount((int)Block.Door) == count + 1, "Breaking the upper half drops exactly one wooden door");
            game.BreakBlock(foot);
            Require(DroppedCount((int)Block.Door) == count + 1, "Breaking the now-empty lower cell does not duplicate the door drop");
            game.Player.Inventory.Slots[0] = new ItemStack((int)Block.Door);
            Require(game.Use(true, foot.Down, foot), "A replacement door can be placed in the cleared doorway");
            game.BreakBlock(foot);
            Require(game.World.GetBlock(foot) == Block.Air && game.World.GetBlock(foot.Up) == Block.Air, "Breaking the lower half also removes the complete door");
            Require(DroppedCount((int)Block.Door) == count + 2, "Breaking the lower half also drops exactly one door");
            game.BreakBlock(foot.Up);
            Require(DroppedCount((int)Block.Door) == count + 2, "The removed upper half cannot generate a duplicate drop");
            game.Player.Inventory.Slots[0] = new ItemStack((int)Block.Door, 2);
            game.World.Set(foot.Up, Block.Stone);
            Require(!game.Use(true, foot.Down, foot), "Door placement is rejected when the upper half is obstructed");
            Require(game.World.GetBlock(foot) == Block.Air && game.Player.Inventory.Held.Count == 2, "Rejected placement leaves the world and door stack unchanged");
            game.World.Set(foot.Up, Block.Air);
            game.Player.IsCreative = true;
            yield return null;
        }

        private IEnumerator MobChecks()
        {
            stage = "mob gait and idle recovery";
            var root = new GameObject("Interaction test zombie");
            root.transform.position = new Vector3(12.5f, 33, 8.5f);
            root.transform.rotation = Quaternion.Euler(0, 145, 0);
            var visual = root.AddComponent<MobVisual>();
            visual.Init(MobKind.Zombie);
            Transform[] legs = root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("leg_", StringComparison.OrdinalIgnoreCase) && !t.name.ToLowerInvariant().Contains("mesh")).ToArray();
            Require(legs.Length == 2, "Blender zombie exposes two animated leg joints");
            Quaternion[] rest = legs.Select(t => t.rotation).ToArray();
            float opposingFrames = 0, activeFrames = 0;
            for (int i = 0; i < 90; i++)
            {
                visual.Animate(1f / 60, MobRules.Definition(MobKind.Zombie).Speed, 0, 0, 0, false);
                Vector3 a = RotationVector(legs[0].rotation * Quaternion.Inverse(rest[0]));
                Vector3 b = RotationVector(legs[1].rotation * Quaternion.Inverse(rest[1]));
                if (a.magnitude > 4 && b.magnitude > 4) { activeFrames++; if (Vector3.Dot(a, b) < 0) opposingFrames++; }
            }
            Require(activeFrames > 30 && opposingFrames > activeFrames * .95f, "Biped legs swing in opposite directions throughout the walking cycle");
            game.Player.Teleport(new Vector3(12.5f, 33.08f, 4.5f));
            FacePoint(root.transform.position + Vector3.up * 1.1f);
            yield return Capture("06-zombie-walking.png");
            for (int i = 0; i < 180; i++) visual.Animate(1f / 60, 0, 0, 0, 0, false);
            Require(Quaternion.Angle(legs[0].rotation, rest[0]) < 1 && Quaternion.Angle(legs[1].rotation, rest[1]) < 1, "Stopping smoothly returns both legs to their neutral stance");
            yield return Capture("07-zombie-idle.png");
            Destroy(root);
        }

        private static Vector3 RotationVector(Quaternion rotation)
        {
            rotation.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180) angle -= 360;
            return axis * angle;
        }

        private void FacePoint(Vector3 position)
        {
            Vector3 direction = (position - game.Player.Eye.transform.position).normalized;
            game.Player.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0);
            game.Player.Pitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1, 1)) * Mathf.Rad2Deg;
            game.Player.Eye.transform.localRotation = Quaternion.Euler(game.Player.Pitch, 0, 0);
        }

        private IEnumerator Capture(string name)
        {
            yield return null;
            Camera camera = game.Player.Eye;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            const int width = 1280, height = 720;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default, Math.Max(1, QualitySettings.antiAliasing));
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                image.Apply(false);
                lastImage = image.GetPixels32();
                var colors = new HashSet<int>();
                int magenta = 0, samples = 0;
                for (int y = height / 8; y < height * 7 / 8; y += 13)
                for (int x = width / 8; x < width * 7 / 8; x += 13)
                {
                    Color32 p = lastImage[y * width + x];
                    colors.Add(((p.r >> 3) << 10) | ((p.g >> 3) << 5) | (p.b >> 3));
                    if (p.r > 180 && p.b > 180 && p.g < 60) magenta++;
                    samples++;
                }
                File.WriteAllBytes(Path.Combine(artifactDirectory, name), image.EncodeToPNG());
                Require(colors.Count > 12, name + " contains a detailed scene, not a blank frame");
                Require(magenta < samples / 100, name + " has no substantial missing-shader magenta");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Destroy(image);
            }
        }

        private static T Field<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            return (T)field.GetValue(target);
        }

        private static int DroppedCount(int id) => UnityEngine.Object.FindObjectsByType<DroppedItem>(FindObjectsSortMode.None)
            .Where(d => d.Stack != null && d.Stack.Id == id).Sum(d => d.Stack.Count);

        private void ValidateDirectories()
        {
            string savesArgument = GameSession.Argument("-voxel-saves"), artifactArgument = GameSession.Argument("-voxel-artifacts");
            if (string.IsNullOrWhiteSpace(savesArgument) || string.IsNullOrWhiteSpace(artifactArgument))
                throw new InvalidOperationException("Interaction checks require fresh -voxel-saves and -voxel-artifacts directories.");
            string project = FindProjectRoot(), saves = Path.GetFullPath(savesArgument);
            if (!saves.StartsWith(Path.Combine(project, ".cache") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Interaction check saves must stay under Game/.cache.");
            if (!string.Equals(saves.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(game.SaveDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The game did not use the isolated interaction-check save directory.");
            if (Directory.EnumerateFiles(saves, "*.vws", SearchOption.AllDirectories).Any())
                throw new InvalidOperationException("Interaction checks cannot run in a directory containing existing worlds.");
            artifactDirectory = Path.GetFullPath(artifactArgument);
            if (!artifactDirectory.StartsWith(Path.Combine(project, "artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Interaction screenshots must stay under Game/artifacts.");
            Directory.CreateDirectory(artifactDirectory);
            Require(true, "Interaction saves and screenshots are isolated from player data");
        }

        private static string FindProjectRoot()
        {
            foreach (string start in new[] { Application.dataPath, Directory.GetCurrentDirectory() })
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                if (Directory.Exists(Path.Combine(directory.FullName, "Assets", "Scripts")) && Directory.Exists(Path.Combine(directory.FullName, "ProjectSettings")))
                    return directory.FullName;
            throw new DirectoryNotFoundException("Run the interaction-check executable inside its Game project.");
        }

        private void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            checks.Add(description);
            Debug.Log("VOXEL_INTERACTION_CHECK " + description);
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!finished && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
                Fail("Runtime error during " + stage + ": " + condition + "\n" + stackTrace);
        }

        private void Finish()
        {
            string summary = "VOXEL_INTERACTION_PASS " + checks.Count + " checks in " + (Time.realtimeSinceStartup - started).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " seconds";
            try { File.WriteAllText(Path.Combine(artifactDirectory, "result.txt"), summary + Environment.NewLine + string.Join(Environment.NewLine, checks)); }
            catch (Exception error) { Fail("Could not write interaction-check report: " + error); return; }
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
            string report = "VOXEL_INTERACTION_FAIL " + message;
            try { if (artifactDirectory != null) File.WriteAllText(Path.Combine(artifactDirectory, "result.txt"), report + Environment.NewLine + string.Join(Environment.NewLine, checks)); }
            catch (Exception error) { Debug.LogWarning("Could not write interaction-check report: " + error.Message); }
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
