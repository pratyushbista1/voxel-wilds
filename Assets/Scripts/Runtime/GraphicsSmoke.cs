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
    public sealed class GraphicsSmoke : MonoBehaviour
    {
        private GameSession game;
        private string artifactDirectory;
        private string stage = "initialization";
        private float started;
        private bool finished;
        private readonly List<string> checks = new List<string>();
        private readonly Dictionary<string, float> luminance = new Dictionary<string, float>();

        private void Start()
        {
            started = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            StartCoroutine(Guarded(Exercise()));
        }

        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > 120)
                Fail("120-second watchdog expired during " + stage);
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
            Require(game != null && game.Player != null && game.Renderer != null, "Native scene creates its graphics components");
            ValidateDirectories();
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Visual checks have a real graphics device");
            UnityEngine.Random.InitState(1453);
            game.Settings.Fullscreen = false;
            game.Settings.Fps = 60;
            game.Settings.VSync = false;
            game.Settings.Bobbing = false;
            GraphicsOptions.SetPreset(game.Settings, 2);
            game.ApplySettings();
            game.NewWorld("Graphics check", 1453, true);
            game.SetPaused(true);
            game.Player.enabled = false;
            game.Mobs.enabled = false;
            game.Hud.enabled = false;
            foreach (var renderer in game.Player.Eye.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            Require(game.World.Seed == 1453 && game.World.Dimension == Dimension.Overworld, "Visual fixture uses a fixed Overworld seed");

            stage = "material and water fixture";
            BuildFixture();
            Vector3 viewpoint = new Vector3(19, 38, -1);
            game.Player.Teleport(viewpoint);
            FacePoint(new Vector3(4, 34.3f, 17));
            game.Renderer.EnsureImmediate(viewpoint);
            for (int frame = 0; frame < 90; frame++)
            {
                game.Renderer.Tick(viewpoint, game.Settings.ViewDistance);
                yield return null;
            }
            Require(game.Renderer.ChunkCount >= 25, "Visual fixture has surrounding terrain chunks");
            foreach (string name in new[] { "VoxelWilds/Terrain", "VoxelWilds/Fluid", "VoxelWilds/Sky", "VoxelWilds/Cinematic" })
            {
                Shader shader = Shader.Find(name);
                Require(shader != null && shader.isSupported, name + " compiles for the native graphics device");
            }
            Require(game.Renderer.Atlas != null && game.Renderer.Atlas.width == 256 && game.Renderer.Atlas.height == 256,
                "The renderer owns the complete 256-pixel block texture atlas");
            Require(game.Renderer.Atlas.mipmapCount >= 4, "The block atlas includes isolated mip levels for distant terrain");
            Require(game.Renderer.TerrainMaterial.FindPass("ShadowCaster") >= 0, "Terrain includes its own alpha-tested shadow-caster pass");
            File.WriteAllBytes(Path.Combine(artifactDirectory, "00-block-atlas.png"), game.Renderer.Atlas.EncodeToPNG());

            stage = "High preset at midday";
            ApplyPreset(2);
            SetTime(.5f);
            Require(QualitySettings.antiAliasing == 4, "High preset applies 4x MSAA");
            Require(QualitySettings.shadows == ShadowQuality.All, "High preset enables soft shadows");
            Require(QualitySettings.shadowResolution == ShadowResolution.High, "High preset selects high-resolution shadow maps");
            Require(Mathf.Abs(QualitySettings.shadowDistance - 96) < .1f, "High preset applies a 96-block shadow distance");
            Require(Mathf.Abs(Shader.GetGlobalFloat("_VoxelAOStrength") - .75f) < .01f, "High preset sends ambient occlusion strength to terrain shaders");
            Require(Shader.GetGlobalFloat("_VoxelWaterMotion") > .5f, "High preset enables animated water in the fluid shader");
            Require(game.Player.Eye.allowMSAA, "Main camera allows the selected multisample antialiasing");
            Require(game.Settings.Cinematic && game.Player.Eye.allowHDR, "High preset enables the cinematic HDR rendering path");
            Require(Mathf.Abs(game.Settings.Bloom - .18f) < .001f && Mathf.Abs(game.Settings.Exposure) < .001f,
                "High preset uses restrained bloom at neutral exposure");
            CinematicEffects effects = game.Player.Eye.GetComponent<CinematicEffects>();
            Require(effects != null && effects.IsSupported && effects.EffectsEnabled,
                "Main camera owns a supported and active cinematic image effect");
            Require(Mathf.Abs(effects.BloomIntensity - .18f) < .001f && Mathf.Abs(effects.Exposure) < .001f,
                "High preset passes bloom and exposure values into the camera effect");
            yield return Capture("01-high-midday.png");

            stage = "Low preset at the same view";
            ApplyPreset(0);
            SetTime(.5f);
            Require(QualitySettings.antiAliasing == 0, "Low preset disables MSAA");
            Require(QualitySettings.shadows == ShadowQuality.Disable, "Low preset disables shadow rendering");
            Require(Shader.GetGlobalFloat("_VoxelWaterMotion") < .5f, "Low preset disables water animation");
            Require(Mathf.Abs(Shader.GetGlobalFloat("_VoxelAOStrength") - .35f) < .01f, "Low preset reduces terrain ambient occlusion");
            Require(!game.Settings.Cinematic && !game.Player.Eye.allowHDR && game.Settings.Bloom == 0,
                "Low preset disables the cinematic HDR path and bloom");
            Require(!effects.EffectsEnabled && effects.BloomIntensity == 0,
                "Low preset bypasses the cinematic image effect without recreating the camera");
            yield return Capture("02-low-midday.png");

            stage = "frame pacing and custom settings";
            game.Settings.VSync = true;
            game.Settings.Fps = 0;
            game.ApplySettings();
            Require(QualitySettings.vSyncCount == 1, "VSync control enables display synchronization");
            game.Settings.VSync = false;
            game.ApplySettings();
            Require(QualitySettings.vSyncCount == 0 && Application.targetFrameRate == -1, "Unlimited FPS removes both VSync and the application frame cap when VSync is off");
            game.Settings.Fps = 120;
            game.ApplySettings();
            Require(Application.targetFrameRate == 120, "Custom FPS cap reaches Unity frame pacing");
            GameSettings recovered = JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(game.Settings));
            Require(recovered.Fps == 120 && !recovered.VSync && recovered.AntiAliasing == 0 && !recovered.AnimatedWater && !recovered.Cinematic && recovered.Bloom == 0,
                "Advanced graphics settings survive JSON save serialization");

            stage = "High preset sunset and night";
            ApplyPreset(2);
            SetTime(.73f);
            yield return Capture("03-high-sunset.png");
            SetTime(.03f);
            yield return Capture("04-high-night.png");
            Require(luminance["01-high-midday.png"] > luminance["04-high-night.png"] + 5,
                "The same terrain is visibly darker at night than at midday");
            SetTime(.27f);
            yield return Capture("05-high-sunrise.png");
            SetTime(.5f);
            stage = "live exposure control";
            game.Settings.Exposure = .75f;
            game.ApplySettings();
            Require(Mathf.Abs(effects.Exposure - .75f) < .001f, "Changing exposure updates the live camera effect");
            yield return Capture("06-high-exposure-plus075.png");
            Require(luminance["06-high-exposure-plus075.png"] > luminance["01-high-midday.png"] + 3,
                "Increasing exposure visibly brightens the rendered scene");
            game.Settings.Exposure = 0;
            game.ApplySettings();
            stage = "rendered shadow comparison";
            ApplyPreset(2);
            int previousShadows = game.Settings.Shadows;
            bool previousWaterMotion = game.Settings.AnimatedWater;
            bool previousClouds = game.Settings.Clouds;
            game.Settings.AnimatedWater = false;
            game.Settings.Clouds = false;
            game.Settings.Shadows = 2;
            game.ApplySettings();
            SetTime(.65f);
            yield return Capture("07-high-shadows-on.png");
            game.Settings.Shadows = 0;
            game.ApplySettings();
            SetTime(.65f);
            yield return Capture("08-high-shadows-off.png");
            float shadowDifference = luminance["08-high-shadows-off.png"] - luminance["07-high-shadows-on.png"];
            game.Settings.Shadows = previousShadows;
            game.Settings.AnimatedWater = previousWaterMotion;
            game.Settings.Clouds = previousClouds;
            game.ApplySettings();
            SetTime(.5f);
            Debug.Log("VOXEL_VISUAL_SHADOW_DELTA " + shadowDifference.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            Require(shadowDifference > .1f,
                "Enabling shadows visibly darkens the same terrain compared with shadows disabled");
            Require(game.SaveWorld() && game.LastSaveError == null, "Graphics controls do not prevent normal world saving");
        }

        private void ApplyPreset(int preset)
        {
            GraphicsOptions.SetPreset(game.Settings, preset);
            game.Settings.Fps = 60;
            game.Settings.VSync = false;
            game.Settings.Fullscreen = false;
            game.ApplySettings();
        }

        private void BuildFixture()
        {
            for (int z = -3; z <= 32; z++)
            for (int x = -9; x <= 24; x++)
            {
                game.World.Set(new Cell(x, 31, z), Block.Dirt);
                game.World.Set(new Cell(x, 32, z), Block.Grass);
                for (int y = 33; y <= 43; y++) game.World.Set(new Cell(x, y, z), Block.Air);
            }
            for (int z = 8; z <= 19; z++)
            for (int x = 5; x <= 13; x++)
            {
                game.World.Set(new Cell(x, 29, z), Block.Sand);
                bool edge = z == 8 || z == 19 || x == 5 || x == 13;
                for (int y = 30; y <= 32; y++)
                    game.World.Set(new Cell(x, y, z), edge ? Block.Cobble : y == 32 ? Block.Air : Block.Water);
            }
            for (int x = -5; x <= 13; x++)
            for (int y = 33; y <= 37; y++)
            {
                Block block = x < 1 ? Block.Planks : x < 7 ? Block.Bricks : Block.Stone;
                if (y == 33 || y == 37 || x == -5 || x == 1 || x == 7 || x == 13) block = Block.Log;
                if (y >= 34 && y <= 35 && (x == -2 || x == -1 || x == 4 || x == 5)) block = Block.Glass;
                game.World.Set(new Cell(x, y, 25), block);
            }
            for (int z = 0; z < 25; z++)
                for (int x = 1; x <= 3; x++) game.World.Set(new Cell(x, 32, z), Block.Gravel);
            Tree(-5, 13);
            Tree(18, 23);
            game.World.Set(new Cell(-1, 33, 20), Block.Chest);
            game.World.Set(new Cell(-2, 33, 20), Block.Workbench);
            game.World.Set(new Cell(-3, 33, 20), Block.Furnace);
            game.World.Set(new Cell(3, 33, 18), Block.Lantern);
            game.World.Set(new Cell(-2, 33, 11), Block.Campfire);
            for (int z = 20; z <= 22; z++)
            for (int x = 15; x <= 17; x++)
            {
                game.World.Set(new Cell(x, 32, z), Block.Farmland);
                game.World.Set(new Cell(x, 33, z), Block.Crop);
            }
            Require(game.World.GetBlock(new Cell(7, 31, 12)) == Block.Water, "Visual fixture includes a bounded transparent-water pool");
            Require(game.World.GetBlock(new Cell(4, 35, 25)) == Block.Glass, "Visual fixture includes windows, wood, brick and stone surfaces");
        }

        private void Tree(int x, int z)
        {
            for (int y = 33; y <= 37; y++) game.World.Set(new Cell(x, y, z), Block.Log);
            for (int y = 36; y <= 39; y++)
            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            {
                if (y == 39 && (Math.Abs(dx) > 1 || Math.Abs(dz) > 1)) continue;
                Cell cell = new Cell(x + dx, y, z + dz);
                if (game.World.GetBlock(cell) != Block.Log) game.World.Set(cell, Block.Leaves);
            }
        }

        private void SetTime(float value)
        {
            var state = (SessionSave)typeof(GameSession).GetField("save", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            state.Day = value;
            typeof(GameSession).GetMethod("UpdateSky", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
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
            int width = 1280, height = 720;
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
                Color32[] pixels = image.GetPixels32();
                var colors = new HashSet<int>();
                int magenta = 0, samples = 0;
                float brightness = 0;
                int terrainSamples = 0, visibleTerrain = 0;
                for (int y = height / 8; y < height * 7 / 8; y += 13)
                for (int x = width / 8; x < width * 7 / 8; x += 13)
                {
                    Color32 p = pixels[y * width + x];
                    colors.Add(((p.r >> 3) << 10) | ((p.g >> 3) << 5) | (p.b >> 3));
                    if (p.r > 180 && p.b > 180 && p.g < 60) magenta++;
                    brightness += .2126f * p.r + .7152f * p.g + .0722f * p.b;
                    samples++;
                    if (y < height / 3)
                    {
                        terrainSamples++;
                        if (p.r + p.g + p.b > 30) visibleTerrain++;
                    }
                }
                File.WriteAllBytes(Path.Combine(artifactDirectory, name), image.EncodeToPNG());
                Require(colors.Count > 12, name + " contains detailed rendered terrain, not a blank frame");
                Require(magenta < samples / 100, name + " has no substantial missing-shader magenta");
                if (name.Contains("midday")) Require(visibleTerrain > terrainSamples * .8f, name + " keeps foreground terrain visibly lit");
                luminance[name] = brightness / samples;
                Debug.Log("VOXEL_VISUAL_IMAGE " + name + " luminance=" + luminance[name].ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Destroy(image);
            }
        }

        private void ValidateDirectories()
        {
            string saveArgument = GameSession.Argument("-voxel-saves");
            string artifactArgument = GameSession.Argument("-voxel-artifacts");
            if (string.IsNullOrWhiteSpace(saveArgument) || string.IsNullOrWhiteSpace(artifactArgument))
                throw new InvalidOperationException("Visual checks require fresh -voxel-saves and -voxel-artifacts directories.");
            string project = FindProjectRoot();
            string saves = Path.GetFullPath(saveArgument);
            string cacheRoot = Path.Combine(project, ".cache") + Path.DirectorySeparatorChar;
            if (!saves.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Visual check saves must stay under Game/.cache.");
            if (!string.Equals(saves.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(game.SaveDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The game did not use the isolated visual-check save directory.");
            if (Directory.EnumerateFiles(saves, "*.vws", SearchOption.AllDirectories).Any())
                throw new InvalidOperationException("Visual checks cannot run in a directory containing existing worlds.");
            artifactDirectory = Path.GetFullPath(artifactArgument);
            string artifactRoot = Path.Combine(project, "artifacts") + Path.DirectorySeparatorChar;
            if (!artifactDirectory.StartsWith(artifactRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Visual-check screenshots must stay under Game/artifacts.");
            Directory.CreateDirectory(artifactDirectory);
            Require(true, "Visual saves and screenshots are isolated from player data");
        }

        private static string FindProjectRoot()
        {
            foreach (string start in new[] { Application.dataPath, Directory.GetCurrentDirectory() })
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                if (Directory.Exists(Path.Combine(directory.FullName, "Assets", "Scripts")) && Directory.Exists(Path.Combine(directory.FullName, "ProjectSettings")))
                    return directory.FullName;
            throw new DirectoryNotFoundException("Run the visual-check executable inside its Game project.");
        }

        private void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            checks.Add(description);
            Debug.Log("VOXEL_VISUAL_CHECK " + description);
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!finished && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
                Fail("Runtime error during " + stage + ": " + condition + "\n" + stackTrace);
        }

        private void Finish()
        {
            string summary = "VOXEL_VISUAL_PASS " + checks.Count + " checks in " + (Time.realtimeSinceStartup - started).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " seconds";
            try { File.WriteAllText(Path.Combine(artifactDirectory, "result.txt"), summary + Environment.NewLine + string.Join(Environment.NewLine, checks)); }
            catch (Exception error) { Fail("Could not write visual-check report: " + error); return; }
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
            string report = "VOXEL_VISUAL_FAIL " + message;
            try { if (artifactDirectory != null) File.WriteAllText(Path.Combine(artifactDirectory, "result.txt"), report + Environment.NewLine + string.Join(Environment.NewLine, checks)); }
            catch (Exception error) { Debug.LogWarning("Could not write visual-check report: " + error.Message); }
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
