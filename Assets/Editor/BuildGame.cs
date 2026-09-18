using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelWilds.Core;
using Object = UnityEngine.Object;

namespace VoxelWilds.Editor
{
    public static class BuildGame
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string Version = "2.0.3";

        [MenuItem("Voxel Wilds/Configure project")]
        public static void Configure()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Project configuration was cancelled.");
            PlayerSettings.companyName = "VoxelWilds";
            PlayerSettings.productName = "Voxel Wilds";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.voxelwilds.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.All;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset"));
            var input = settings.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            IncludeShader("Standard");
            IncludeShader("Unlit/Color");
            IncludeShader("Sprites/Default");
            IncludeShader("VoxelWilds/Terrain");
            IncludeShader("VoxelWilds/Fluid");
            IncludeShader("VoxelWilds/Sky");
            IncludeShader("VoxelWilds/Cinematic");
            IncludeShader("VoxelWilds/FirstPerson");

            Directory.CreateDirectory("Assets/Scenes");
            var scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sessions = Object.FindObjectsByType<GameSession>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (sessions.Length == 0) new GameObject("Voxel Wilds").AddComponent<GameSession>();
            else if (sessions.Length != 1) throw new InvalidOperationException("Main scene must contain exactly one GameSession.");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Configured Voxel Wilds " + Version + " for Windows x64 with the built-in renderer.");
        }

        static void IncludeShader(string name)
        {
            Shader shader = Shader.Find(name);
            if (!shader) throw new InvalidOperationException("Required shader is missing: " + name);
            var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphics.Length == 0) throw new InvalidOperationException("GraphicsSettings asset is missing.");
            var settings = new SerializedObject(graphics[0]);
            var list = settings.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) throw new InvalidOperationException("Cannot locate always-included shaders.");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Voxel Wilds/Build Windows")]
        public static void BuildWindows()
        {
            Configure();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.GetFullPath(Argument("-voxelBuildPath") ?? Path.Combine(root, "release", Version, "Windows"));
            if (!folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Build output must stay inside this Game project.");
            Directory.CreateDirectory(folder);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(folder, "Voxel Wilds.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors > 0)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result + ", " + report.summary.totalErrors + " errors.");
            if (!File.Exists(options.locationPathName) || !File.Exists(Path.Combine(folder, "UnityPlayer.dll")) || !Directory.Exists(Path.Combine(folder, "Voxel Wilds_Data")))
                throw new InvalidOperationException("Build report succeeded but required player files are missing.");
            Debug.Log("VOXEL_BUILD_SUCCEEDED " + options.locationPathName + " (" + report.summary.totalSize + " bytes)");
        }

        [Serializable]
        sealed class CheckReport
        {
            public bool success;
            public int checks;
            public string unityVersion;
            public string completedUtc;
        }

        [MenuItem("Voxel Wilds/Run editor checks")]
        public static void RunChecks()
        {
            Configure();
            int checks = 0;
            void Check(bool result, string message)
            {
                if (!result) throw new InvalidOperationException(message);
                checks++;
                Debug.Log("PASS " + message);
            }
            Check(Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None).Length == 1, "Main scene has one game session");
            Check(EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].path == ScenePath, "Main scene is included in the player");
            foreach (string shaderName in new[] { "VoxelWilds/Terrain", "VoxelWilds/Fluid", "VoxelWilds/Sky", "VoxelWilds/Cinematic", "VoxelWilds/FirstPerson", "Standard" })
            {
                var shader = Shader.Find(shaderName);
                Check(shader != null && !ShaderUtil.ShaderHasError(shader), shaderName + " imports without shader errors");
            }
            foreach (string modelName in new[] { "zombie", "cow", "pig", "sheep", "chicken", "blaze", "piglin", "skeleton", "creeper", "spider", "enderman", "end_dragon", "end_crystal", "villager" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/" + modelName + ".fbx");
                Check(model != null, modelName + " model imports");
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                Check(renderers.Length > 0 && renderers.All(r => r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name == "Standard")), modelName + " has built-in materials");
            }
            var bag = new Inventory();
            Check(bag.Add(Items.Coal, 70) == 0 && bag.Count(Items.Coal) == 70 && bag.Slots[0].Count == 64, "Inventory splits stacks");
            var grid = new[] { new ItemStack((int)Block.Log), null, null, null };
            Check(Crafting.TryCraftInto(grid, 2, bag) && bag.Count((int)Block.Planks) == 4 && grid[0] == null, "Crafting consumes and inserts exactly once");
            var furnace = new Furnace { Input = new ItemStack(Items.RawBeef, 8), Fuel = new ItemStack(Items.LavaBucket) };
            Check(furnace.Tick(80) == 8 && furnace.Output.Id == Items.Steak && furnace.Fuel.Id == Items.EmptyBucket, "Furnace cooks meat and preserves the bucket");
            var world = new World(1729, Dimension.Overworld);
            world.Set(new Cell(-1, 90, -1), Block.Obsidian);
            Check(world.GetBlock(new Cell(-1, 90, -1)) == Block.Obsidian, "World edits work across negative chunk coordinates");
            string destination = Argument("-voxelTestReport") ?? "artifacts/unity-editor-checks.json";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination)));
            File.WriteAllText(destination, JsonUtility.ToJson(new CheckReport { success = true, checks = checks, unityVersion = Application.unityVersion, completedUtc = DateTime.UtcNow.ToString("O") }, true));
            Debug.Log("VOXEL_EDITOR_CHECKS_PASSED " + checks);
        }

        static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return null;
        }
    }
}
