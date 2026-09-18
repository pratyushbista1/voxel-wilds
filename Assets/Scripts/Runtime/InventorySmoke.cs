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
    public sealed class InventorySmoke : MonoBehaviour
    {
        private GameSession game;
        private string artifacts, stage = "startup";
        private float started;
        private bool finished;
        private Event pending;
        private readonly List<string> checks = new List<string>();
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private void Start()
        {
            started = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            StartCoroutine(Guard(Exercise()));
        }

        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > 140) Fail("Timed out during " + stage);
        }

        private IEnumerator Guard(IEnumerator scenario)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(scenario);
            while (stack.Count > 0 && !finished)
            {
                object next = null; bool moved = false; Exception error = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception caught) { error = caught; }
                if (error != null) { Fail(stage + ": " + error); yield break; }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            if (!finished) Finish();
        }

        private IEnumerator Exercise()
        {
            game = GameSession.Instance;
            ValidateDirectories();
            Screen.SetResolution(1280, 720, false);
            GraphicsOptions.SetPreset(game.Settings, 2);
            game.Settings.Fullscreen = false; game.Settings.Fps = 60; game.Settings.VSync = false;
            game.ApplySettings();
            game.NewWorld("Inventory and movement check", 1409, false);
            game.SetPaused(true); game.Player.enabled = false; game.Mobs.enabled = false;
            for (int i = 0; i < 5; i++) yield return null;
            IconChecks();
            game.Hud.OpenInventory();
            SeedDisplay();
            yield return Capture("01-inventory.png");
            CheckPanel();
            Require(Field<ItemStack[]>(game.Hud, "grid").Length == 4, "Personal inventory has a 2 by 2 crafting grid");
            yield return InventoryClicks();
            yield return CraftingChecks();
            yield return OtherScreens();
            yield return MovementChecks();
            game.Hud.Close();
            Require(game.SaveWorld(), "Inventory and movement test world saves successfully");
        }

        private void IconChecks()
        {
            stage = "catalog icons";
            using (var atlas = new ItemIconAtlas())
            {
                int[] ids = Items.CreateCreativeInventory();
                int columns = 12, cell = 48, rows = (ids.Length + columns - 1) / columns;
                var sheet = new Texture2D(columns * cell, rows * cell, TextureFormat.RGBA32, false);
                var pixels = Enumerable.Repeat(new Color32(176, 176, 176, 255), sheet.width * sheet.height).ToArray();
                var signatures = new Dictionary<int, ulong>();
                foreach (int id in ids)
                {
                    Texture2D icon = atlas.Get(id);
                    Require(icon != null && icon.width == 32 && icon.height == 32 && icon.filterMode == FilterMode.Point,
                        "Pixel icon exists for " + Items.Name(id));
                    var art = icon.GetPixels32(); int visible = art.Count(p => p.a > 0);
                    Require(visible > 20 && visible < art.Length, Items.Name(id) + " has a transparent, visible silhouette");
                    ulong hash = 14695981039346656037UL;
                    foreach (Color32 p in art) { hash ^= (uint)(p.r | p.g << 8 | p.b << 16 | p.a << 24); hash *= 1099511628211UL; }
                    signatures[id] = hash;
                    int index = Array.IndexOf(ids, id), left = index % columns * cell + 8, bottom = (rows - 1 - index / columns) * cell + 8;
                    for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                        if (art[y * 32 + x].a > 0) pixels[(bottom + y) * sheet.width + left + x] = art[y * 32 + x];
                    Require(ReferenceEquals(icon, atlas.Get(id)), Items.Name(id) + " reuses its cached texture");
                }
                int[] distinct = { Items.IronPickaxe, Items.IronSword, Items.IronAxe, Items.IronShovel, Items.IronHoe,
                    Items.IronHelmet, Items.IronChestplate, Items.IronLeggings, Items.IronBoots, Items.Apple, Items.Bread,
                    Items.RawBeef, Items.Steak, Items.EmptyBucket, Items.WaterBucket, Items.LavaBucket, Items.Bow, Items.Arrow,
                    Items.IronIngot, Items.GoldIngot, Items.Coal, Items.Crystal, (int)Block.Door, (int)Block.Bed };
                Require(distinct.Select(id => signatures[id]).Distinct().Count() == distinct.Length,
                    "Tools, armor, food, buckets and furniture have distinct artwork");
                sheet.SetPixels32(pixels); sheet.Apply();
                File.WriteAllBytes(Path.Combine(artifacts, "00-item-icons.png"), sheet.EncodeToPNG());
                File.WriteAllLines(Path.Combine(artifacts, "00-item-icons-index.txt"), ids.Select((id, i) => (i / columns + 1) + "," + (i % columns + 1) + ": " + Items.Name(id)));
                Destroy(sheet);
            }
        }

        private IEnumerator InventoryClicks()
        {
            stage = "inventory click and drag";
            ClearItems(); var slots = game.Player.Inventory.Slots;
            slots[9] = new ItemStack((int)Block.Planks, 16);
            yield return Click(Slot(9), 1);
            Require(CursorStack?.Count == 8 && slots[9]?.Count == 8, "Right click picks up half a stack");
            yield return Click(Slot(10), 1);
            Require(CursorStack.Count == 7 && slots[10].Count == 1, "Right click places one item");
            yield return Click(Slot(11));
            Require(CursorStack == null && slots[11].Count == 7, "Left click places the carried stack");
            yield return Click(Slot(11), 0, true);
            Require(slots[11] == null && slots.Take(9).Where(s => s != null).Sum(s => s.Count) == 7,
                "Shift click moves storage items into the hotbar");
            yield return Send(new Event { type = EventType.KeyDown, mousePosition = Slot(9).center, keyCode = KeyCode.Alpha3 });
            Require(slots[9] == null && slots[2]?.Count == 8, "Number key swaps a hovered stack with the selected hotbar slot");

            ClearItems(); SetField(game.Hud, "cursor", new ItemStack((int)Block.Planks, 14));
            yield return Drag(new[] { Slot(12), Slot(13), Slot(14) }, 0);
            Require(slots[12]?.Count == 4 && slots[13]?.Count == 4 && slots[14]?.Count == 4 && CursorStack?.Count == 2,
                "Left drag distributes equal shares and leaves the remainder on the cursor");
            ClearItems(); SetField(game.Hud, "cursor", new ItemStack((int)Block.Planks, 5));
            yield return Drag(new[] { Slot(16), Slot(17), Slot(18) }, 1);
            Require(slots[16]?.Count == 1 && slots[17]?.Count == 1 && slots[18]?.Count == 1 && CursorStack?.Count == 2,
                "Right drag deposits one per visited slot");
            game.Hud.Close();
            Require(game.Player.Inventory.Count((int)Block.Planks) == 5 && game.Hud.CaptureTransient().Length == 0,
                "Closing an inventory returns the remaining cursor items exactly once");
            game.Hud.OpenInventory();
            yield return null;
            ClearItems();
            for (int i = 0; i < 4; i++) game.Player.Inventory.Armor[i] = new ItemStack();
            slots[9] = new ItemStack(Items.IronHelmet, 1, 90);
            yield return Click(Slot(9), 0, true);
            Require(game.Player.Inventory.Armor[0]?.Id == Items.IronHelmet && game.Player.Inventory.Armor[0].Durability == 90 && slots[9] == null,
                "Shift equip recognizes serialized empty armor slots and preserves durability");
            SetField(game.Hud, "cursor", new ItemStack((int)Block.Planks, 2));
            Rect panel = Layout("InventoryPanelRect");
            yield return Click(new Rect(panel.x + 21, panel.y + 21, 54, 54));
            Require(CursorStack?.Count == 2 && game.Player.Inventory.Armor[0]?.Id == Items.IronHelmet,
                "Armor slots reject non-equipment without replacing the worn item");
            SetField(game.Hud, "tooltip", "");
            yield return Send(new Event { type = EventType.MouseMove, mousePosition = Slot(15).center });
            Require(Field<string>(game.Hud, "tooltip") == "", "Empty slots do not display an Air tooltip");
            Array.Clear(game.Player.Inventory.Armor, 0, 4);
        }

        private IEnumerator CraftingChecks()
        {
            stage = "crafting output";
            ClearItems();
            var grid = Field<ItemStack[]>(game.Hud, "grid"); grid[0] = new ItemStack((int)Block.Log, 2);
            yield return Click(Layout("CraftingOutputRect"));
            Require(CursorStack?.Id == (int)Block.Planks && CursorStack.Count == 4 && grid[0]?.Count == 1,
                "Personal crafting takes one recipe and consumes its ingredients");
            yield return Click(Layout("CraftingOutputRect"), 1);
            Require(CursorStack.Count == 8 && grid[0] == null, "Right click takes another whole crafting result");
            game.Hud.Close(); game.Hud.OpenCrafting(); ClearItems();
            grid = Field<ItemStack[]>(game.Hud, "grid");
            Require(grid.Length == 9, "The crafting table has a 3 by 3 grid");
            for (int i = 0; i < 3; i++) grid[i] = new ItemStack(Items.IronIngot);
            grid[4] = new ItemStack(Items.Stick); grid[7] = new ItemStack(Items.Stick);
            SeedDisplay(false);
            yield return Capture("02-crafting-table.png");
            yield return Click(Layout("CraftingOutputRect"), 0, true);
            Require(grid.All(s => s == null) && game.Player.Inventory.Count(Items.IronPickaxe) == 1,
                "Shift crafting moves a tool into storage without losing durability");
            ClearItems(); for (int i = 0; i < 36; i++) game.Player.Inventory.Slots[i] = new ItemStack((int)Block.Stone, 64);
            grid[0] = new ItemStack((int)Block.Log);
            yield return Click(Layout("CraftingOutputRect"), 0, true);
            Require(grid[0]?.Count == 1, "A full inventory does not consume a shift-crafted recipe");
            ClearItems(); SeedDisplay();
            yield return Click(Layout("RecipeToggleRect"));
            yield return Capture("03-recipe-book.png");
            Require(Field<bool>(game.Hud, "recipeBookOpen"), "The recipe button opens the recipe book");
            Rect panel = Layout("InventoryPanelRect");
            Require(panel.x >= 0 && panel.xMax <= Screen.width && panel.y >= 0 && panel.yMax <= Screen.height,
                "Opening the recipe book leaves the crafting panel on screen");
            int logs = game.Player.Inventory.Count((int)Block.Log);
            typeof(GameHud).GetMethod("FillRecipe", Private).Invoke(game.Hud, new object[] { Crafting.Recipes[0], 3 });
            Require(grid[0]?.Id == (int)Block.Log && grid[0].Count == 1 && game.Player.Inventory.Count((int)Block.Log) == logs - 1,
                "Selecting a recipe fills its grid from the inventory without duplicating ingredients");
            Screen.SetResolution(960, 540, false);
            for (int i = 0; i < 5; i++) yield return null;
            yield return Capture("04-small-window.png");
            CheckPanel();
            Screen.SetResolution(1280, 720, false);
            for (int i = 0; i < 5; i++) yield return null;
        }

        private IEnumerator OtherScreens()
        {
            stage = "container screens";
            var chest = new ItemStack[27]; chest[0] = new ItemStack(Items.Apple, 9);
            game.Hud.OpenChest(chest); yield return Capture("05-chest.png"); CheckPanel();
            var furnace = new Furnace { Input = new ItemStack(Items.RawBeef, 3), Fuel = new ItemStack(Items.Coal, 5), Output = new ItemStack(Items.Steak, 2) };
            game.Hud.OpenFurnace(furnace);
            yield return Capture("06-furnace.png"); CheckPanel();
            ClearItems(); SetField(game.Hud, "cursor", new ItemStack(Items.Steak));
            Rect panel = Layout("InventoryPanelRect");
            yield return Send(new Event { type = EventType.MouseDown, mousePosition = new Vector2(panel.x + 128 * 3, panel.y + 43 * 3), button = 0, clickCount = 2 });
            yield return Send(new Event { type = EventType.MouseUp, mousePosition = new Vector2(panel.x + 128 * 3, panel.y + 43 * 3), button = 0 });
            Require(CursorStack?.Count == 3 && furnace.Output == null, "Double-click collection clears furnace output instead of restoring an empty stack");
            game.Player.IsCreative = true; game.Hud.OpenInventory();
            yield return Capture("07-creative.png"); CheckPanel();
            game.Hud.Close(); game.Player.IsCreative = false;
        }

        private IEnumerator MovementChecks()
        {
            stage = "native movement";
            game.Hud.enabled = false;
            for (int z = -3; z <= 24; z++) for (int x = -3; x <= 12; x++)
            {
                game.World.Set(new Cell(x, 40, z), Block.Stone);
                for (int y = 41; y <= 45; y++) game.World.Set(new Cell(x, y, z), Block.Air);
            }
            game.Renderer.EnsureImmediate(new Vector3(3, 41, 3));
            yield return null; Physics.SyncTransforms();
            var step = typeof(PlayerController).GetMethod("StepMovement");
            Require(step != null, "Native movement checks use the normal controller movement path");
            float Move(Vector2 input, bool sprint = false, bool jump = false)
                => (float)step.Invoke(game.Player, new object[] { 1f / 60, input, sprint, false, jump, false, true, false, false });
            game.Player.Hunger = 20; game.Player.IsCreative = true; game.Player.Flying = false;
            game.Player.transform.rotation = Quaternion.identity;
            Vector3 start = new Vector3(3.5f, 41.05f, 1.5f);
            game.Player.Teleport(start);
            for (int i = 0; i < 10; i++) Move(Vector2.zero);
            float z0 = game.Player.transform.position.z;
            for (int i = 0; i < 120; i++) Move(Vector2.up);
            float walking = game.Player.transform.position.z - z0;
            Require(walking > 7.9f && walking < 8.8f, "Grounded walking travels at the expected speed");
            game.Player.Teleport(start); for (int i = 0; i < 10; i++) Move(Vector2.zero);
            z0 = game.Player.transform.position.z;
            for (int i = 0; i < 120; i++) Move(Vector2.up, true);
            float running = game.Player.transform.position.z - z0;
            Require(running > walking * 1.2f && running < walking * 1.4f, "Forward sprint is consistently faster than walking");
            Require(game.Player.Eye.fieldOfView > game.Settings.FieldOfView && game.Player.Eye.fieldOfView < game.Settings.FieldOfView * 1.08f,
                "Sprinting widens the field of view gently");
            float stop = game.Player.transform.position.z;
            for (int i = 0; i < 60; i++) Move(Vector2.zero);
            Require(game.Player.transform.position.z - stop < .45f, "Releasing movement stops without long sliding");
            Require(Mathf.Abs(game.Player.Eye.fieldOfView - game.Settings.FieldOfView) < .1f, "Stopping restores the selected field of view");
            game.Player.Teleport(start); for (int i = 0; i < 10; i++) Move(Vector2.zero);
            float floor = game.Player.transform.position.y, highest = floor;
            Move(Vector2.zero, false, true);
            for (int i = 0; i < 70; i++) { Move(Vector2.zero); highest = Mathf.Max(highest, game.Player.transform.position.y); }
            Require(highest - floor > 1.1f && highest - floor < 1.35f, "A jump clears one block without an exaggerated leap");
            Require(Mathf.Abs(game.Player.transform.position.y - floor) < .08f, "The character lands back on the same floor");
            game.World.Set(new Cell(3, 43, 1), Block.Stone);
            game.Renderer.EnsureImmediate(start); yield return null; Physics.SyncTransforms();
            game.Player.Teleport(start); for (int i = 0; i < 10; i++) Move(Vector2.zero);
            float lowCeilingApex = game.Player.transform.position.y;
            Move(Vector2.zero, false, true);
            for (int i = 0; i < 60; i++) { Move(Vector2.zero); lowCeilingApex = Mathf.Max(lowCeilingApex, game.Player.transform.position.y); }
            Require(lowCeilingApex < floor + .28f && Mathf.Abs(game.Player.transform.position.y - floor) < .08f,
                "Jumping into a low ceiling clears upward momentum and lands normally");
            game.World.Set(new Cell(3, 43, 1), Block.Air);
            game.Renderer.EnsureImmediate(start); yield return null; Physics.SyncTransforms();
            game.Player.IsCreative = false; game.Player.Health = 20; SetField(game.Player, "hitCooldown", 0f);
            game.Player.Teleport(start + Vector3.up * 7);
            for (int i = 0; i < 120; i++) Move(Vector2.zero);
            Require(game.Player.Health < 20 && game.Player.Health >= 15 && Mathf.Abs(game.Player.transform.position.y - floor) < .08f,
                "Landing after a long fall still applies survival fall damage");
            game.Player.IsCreative = true;
            for (int x = 1; x <= 6; x++) game.World.Set(new Cell(x, 41, 5), Block.Stone);
            game.Renderer.EnsureImmediate(start); yield return null; Physics.SyncTransforms();
            game.Player.Teleport(start); for (int i = 0; i < 10; i++) Move(Vector2.zero);
            for (int i = 0; i < 120; i++) Move(Vector2.up, true);
            Require(game.Player.transform.position.z < 4.8f && game.Player.transform.position.y < floor + .1f,
                "Sprinting into a wall neither passes through it nor auto-jumps");
            float blockedDistance = Move(Vector2.up, true);
            Require(blockedDistance < .005f, "Blocked running stops advancing the walking animation");
        }

        private void SeedDisplay(bool tools = true)
        {
            int[] ids = { (int)Block.Grass, (int)Block.Stone, (int)Block.Log, (int)Block.Planks, (int)Block.Workbench,
                (int)Block.Door, (int)Block.Chest, (int)Block.Bed, Items.Apple, Items.Bread, Items.RawBeef, Items.Steak,
                Items.WaterBucket, Items.LavaBucket, Items.Coal, Items.IronIngot, Items.GoldIngot, Items.Crystal, Items.Bow,
                Items.Arrow, Items.IronHelmet, Items.IronChestplate, Items.IronLeggings, Items.IronBoots, Items.EyeEnder, Items.Wheat, Items.Seeds };
            for (int i = 0; i < ids.Length; i++) game.Player.Inventory.Slots[i + 9] = new ItemStack(ids[i], Mathf.Min(16 + i, Items.MaxStack(ids[i])));
            if (tools)
            {
                int[] hotbar = { Items.IronPickaxe, Items.IronSword, Items.IronAxe, Items.IronShovel, Items.IronHoe, (int)Block.Torch, Items.Berries, Items.EmptyBucket, Items.Shield };
                for (int i = 0; i < hotbar.Length; i++) game.Player.Inventory.Slots[i] = new ItemStack(hotbar[i], Mathf.Min(8, Items.MaxStack(hotbar[i])), Items.Durability(hotbar[i]) / 2);
            }
        }

        private void ClearItems()
        {
            Array.Clear(game.Player.Inventory.Slots, 0, 36);
            var grid = Field<ItemStack[]>(game.Hud, "grid"); Array.Clear(grid, 0, grid.Length);
            SetField(game.Hud, "cursor", null);
        }
        private ItemStack CursorStack => Field<ItemStack>(game.Hud, "cursor");
        private Rect Layout(string name) => (Rect)typeof(GameHud).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(game.Hud);
        private Rect Slot(int index) => (Rect)typeof(GameHud).GetMethod("InventorySlotRect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(game.Hud, new object[] { index });
        private void CheckPanel()
        {
            Rect r = Layout("InventoryPanelRect"); float scale = Mathf.Min(Screen.height / 720f, Screen.width / 960f);
            Require(r.width > 400 && r.x * scale >= 0 && r.y * scale >= 0 && r.xMax * scale <= Screen.width + 1 && r.yMax * scale <= Screen.height + 1,
                "The inventory panel fits the current window");
        }
        private IEnumerator Click(Rect rect, int button = 0, bool shift = false)
        {
            yield return Send(new Event { type = EventType.MouseDown, mousePosition = rect.center, button = button, modifiers = shift ? EventModifiers.Shift : EventModifiers.None });
            yield return Send(new Event { type = EventType.MouseUp, mousePosition = rect.center, button = button });
        }
        private IEnumerator Drag(Rect[] slots, int button)
        {
            yield return Send(new Event { type = EventType.MouseDown, mousePosition = slots[0].center, button = button });
            foreach (Rect slot in slots.Skip(1)) yield return Send(new Event { type = EventType.MouseDrag, mousePosition = slot.center, button = button });
            yield return Send(new Event { type = EventType.MouseUp, mousePosition = slots[slots.Length - 1].center, button = button });
        }
        private IEnumerator Send(Event value) { pending = value; while (pending != null && !finished) yield return null; }
        private void OnGUI()
        {
            if (pending == null || game == null || Event.current.type != EventType.Repaint || finished) return;
            var previous = new Event(Event.current); var matrix = GUI.matrix;
            try
            {
                GUI.matrix = Matrix4x4.identity;
                Event.current = new Event(pending);
                typeof(GameHud).GetMethod("InventoryScreen", Private).Invoke(game.Hud, null);
                if (pending.type == EventType.MouseUp) typeof(GameHud).GetMethod("FinishDrag", Private).Invoke(game.Hud, null);
            }
            catch (Exception error) { Fail("GUI event failed: " + error); }
            finally { pending = null; Event.current = previous; GUI.matrix = matrix; }
        }
        private IEnumerator Capture(string name)
        {
            yield return null; yield return new WaitForEndOfFrame();
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            Require(image != null && image.width >= 900 && image.height >= 500, "Captured native UI: " + name);
            var pixels = image.GetPixels32();
            File.WriteAllBytes(Path.Combine(artifacts, name), image.EncodeToPNG());
            Require(pixels.Where((p, i) => i % 53 == 0).Select(p => (p.r >> 4) * 256 + (p.g >> 4) * 16 + (p.b >> 4)).Distinct().Count() > 16,
                name + " contains visible UI and artwork");
            Destroy(image);
        }
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        private static void SetField(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        private void ValidateDirectories()
        {
            string saves = Path.GetFullPath(GameSession.Argument("-voxel-saves") ?? ""), output = GameSession.Argument("-voxel-artifacts");
            DirectoryInfo root = new DirectoryInfo(Application.dataPath);
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Assets", "Scripts"))) root = root.Parent;
            if (root == null || !saves.StartsWith(Path.Combine(root.FullName, ".cache") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || Directory.EnumerateFiles(saves, "*.vws", SearchOption.AllDirectories).Any()) throw new InvalidOperationException("Use a fresh Game/.cache test save directory.");
            artifacts = Path.GetFullPath(output ?? "");
            if (!artifacts.StartsWith(Path.Combine(root.FullName, "artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Screenshots must stay in Game/artifacts.");
            Require(string.Equals(saves, Path.GetFullPath(game.SaveDirectory), StringComparison.OrdinalIgnoreCase), "UI tests use an isolated save directory");
        }
        private void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add(message); Debug.Log("VOXEL_INVENTORY_CHECK " + message);
        }
        private void OnLog(string message, string trace, LogType type)
        {
            if (!finished && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)) Fail(message + "\n" + trace);
        }
        private void Finish()
        {
            finished = true; Application.logMessageReceived -= OnLog;
            string report = "VOXEL_INVENTORY_PASS " + checks.Count + " checks in " + (Time.realtimeSinceStartup - started).ToString("F1") + " seconds";
            File.WriteAllText(Path.Combine(artifacts, "result.txt"), report + Environment.NewLine + string.Join(Environment.NewLine, checks));
            Debug.Log(report); Application.Quit(0);
        }
        private void Fail(string message)
        {
            if (finished) return; finished = true; Application.logMessageReceived -= OnLog;
            string report = "VOXEL_INVENTORY_FAIL " + stage + ": " + message;
            if (artifacts != null) File.WriteAllText(Path.Combine(artifacts, "result.txt"), report + Environment.NewLine + string.Join(Environment.NewLine, checks));
            Debug.LogError(report); Application.Quit(1);
        }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
