using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class FirstPersonView : MonoBehaviour
    {
        public enum Action { Mine, Attack, Use }

        public int PresentedItem { get; private set; }
        public float SwingProgress { get; private set; } = 1;
        public float EquipProgress { get; private set; } = 1;
        public int CompletedSwings { get; private set; }
        public bool IsSwinging => SwingProgress < 1;
        public bool HandVisible => arm != null && arm.gameObject.activeInHierarchy;
        public Vector3 HandLocalPosition => pivot != null ? pivot.localPosition : Vector3.zero;
        public Quaternion HandLocalRotation => pivot != null ? pivot.localRotation : Quaternion.identity;
        public Transform Arm => arm;
        public Transform HeldVisual => itemRoot;

        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<Material> itemMaterials = new List<Material>();
        private PlayerController player;
        private WorldRenderer worldRenderer;
        private Transform pivot, arm, grip, itemRoot;
        private Material skin, sleeve, cuff, nail;
        private float gait, walkWeight, lowering, swingDuration = .3f, eatTime;
        private Vector2 sway;
        private Action action;

        public void Init(PlayerController owner, WorldRenderer renderer)
        {
            player = owner;
            worldRenderer = renderer;
            pivot = new GameObject("Right hand pivot").transform;
            pivot.SetParent(transform, false);
            arm = new GameObject("Player right arm").transform;
            arm.SetParent(pivot, false);
            skin = MakeMaterial("Player skin", new Color(.68f, .45f, .31f));
            sleeve = MakeMaterial("Player sleeve", new Color(.10f, .34f, .43f));
            cuff = MakeMaterial("Sleeve seam", new Color(.07f, .24f, .31f));
            nail = MakeMaterial("Hand knuckles", new Color(.77f, .55f, .40f));
            Cube(arm, "Forearm", new Vector3(0, -.01f, -.14f), new Vector3(.15f, .16f, .29f), skin);
            Cube(arm, "Sleeve", new Vector3(0, -.014f, -.36f), new Vector3(.17f, .18f, .20f), sleeve);
            Cube(arm, "Cuff", new Vector3(0, -.014f, -.255f), new Vector3(.173f, .183f, .025f), cuff);
            Cube(arm, "Hand", new Vector3(0, 0, .04f), new Vector3(.16f, .17f, .16f), skin);
            Cube(arm, "Knuckle edge", new Vector3(0, .078f, .083f), new Vector3(.128f, .013f, .048f), nail);
            grip = new GameObject("Item grip").transform;
            grip.SetParent(pivot, false);
            grip.localPosition = new Vector3(0, .015f, .06f);
            grip.localRotation = Quaternion.Euler(25, 15, -10);
            Advance(0, 0, false, Vector2.zero, false, 0, false);
        }

        public void TriggerSwing(Action nextAction)
        {
            if (IsSwinging) return;
            action = nextAction;
            swingDuration = action == Action.Mine ? .29f : action == Action.Use ? .24f : .34f;
            SwingProgress = 0;
        }

        public void ResetMotion()
        {
            gait = walkWeight = lowering = eatTime = 0;
            sway = Vector2.zero;
            SwingProgress = 1;
            Advance(0, 0, false, Vector2.zero, false, 0, false);
        }

        public void Advance(float deltaTime, float walkedDistance, bool moving, Vector2 lookDelta, bool eating, float bowCharge, bool blocking)
        {
            if (pivot == null || player == null) return;
            float dt = Mathf.Clamp(deltaTime, 0, .1f);
            float smooth = 1 - Mathf.Exp(-12 * dt);
            int selected = player.Inventory.Held?.Id ?? 0;
            float targetLowering = selected != PresentedItem ? 1 : 0;
            lowering = Mathf.MoveTowards(lowering, targetLowering, dt * 8);
            if (lowering >= 1 && selected != PresentedItem) Present(selected);
            EquipProgress = 1 - lowering;
            walkWeight = Mathf.Lerp(walkWeight, moving ? 1 : 0, smooth);
            if (moving) gait += Mathf.Clamp(walkedDistance, 0, .65f) * 5.6f;
            sway = Vector2.Lerp(sway, Vector2.ClampMagnitude(lookDelta, 10) * .0028f, smooth);
            if (IsSwinging)
            {
                SwingProgress = Mathf.Min(1, SwingProgress + dt / swingDuration);
                if (!IsSwinging) CompletedSwings++;
            }
            float t = SwingProgress;
            float strike = Mathf.Sin(t * Mathf.PI);
            float arc = Mathf.Sin(Mathf.Sqrt(t) * Mathf.PI);
            float bobX = Mathf.Sin(gait) * .017f * walkWeight;
            float bobY = (Mathf.Abs(Mathf.Cos(gait)) - .5f) * .018f * walkWeight;
            Vector3 position = new Vector3(.33f + bobX - sway.x, -.27f + bobY - sway.y - lowering * .46f, .60f - lowering * .10f);
            Vector3 rotation = new Vector3(-25 + sway.y * 170, -12 + sway.x * 190, -5 - bobX * 80);
            Vector3 gripRotation = new Vector3(25, 15, -10);
            if (action == Action.Use)
            {
                position += new Vector3(-strike * .035f, strike * .035f, strike * .095f);
                rotation += new Vector3(-strike * 15, strike * 8, strike * 6);
            }
            else
            {
                float strength = action == Action.Mine ? .78f : 1;
                position += new Vector3(-arc * .17f, -strike * .055f, strike * .06f) * strength;
                rotation += new Vector3(strike * 10, -arc * 18, -arc * 20) * strength;
                gripRotation += new Vector3(strike * 40, -arc * 6, -arc * 12) * strength;
            }
            eatTime = eating ? eatTime + dt : 0;
            if (eating)
            {
                float bite = Mathf.Sin(eatTime * 21);
                position += new Vector3(-.13f, .12f + bite * .014f, -.07f);
                rotation += new Vector3(-20, 0, 38 + bite * 4);
            }
            if (bowCharge > 0)
            {
                position += new Vector3(-.14f, .04f, -.10f * bowCharge);
                rotation += new Vector3(5, -12, 12);
            }
            if (blocking)
            {
                position += new Vector3(-.19f, .10f, .03f);
                rotation += new Vector3(-5, 28, 13);
            }
            pivot.localPosition = position;
            pivot.localRotation = Quaternion.Euler(rotation);
            grip.localRotation = Quaternion.Euler(gripRotation);
        }

        private void Present(int item)
        {
            PresentedItem = item;
            if (itemRoot != null)
            {
                itemRoot.gameObject.SetActive(false);
                Destroy(itemRoot.gameObject);
                itemRoot = null;
            }
            foreach (var material in itemMaterials) Destroy(material);
            itemMaterials.Clear();
            if (item == 0) return;
            itemRoot = new GameObject("Held " + Items.Name(item)).transform;
            itemRoot.SetParent(grip, false);
            string model = item == Items.IronSword || item == Items.CrystalSword ? "sword" : Items.MiningTier(item) > 0 ? "pickaxe" : null;
            var prefab = model == null ? null : Resources.Load<GameObject>("Models/" + model);
            if (prefab != null)
            {
                FitTool(prefab, item);
                return;
            }
            Block block = Items.PlaceBlock(item);
            if (block != Block.Air && item != Items.Seeds && item != Items.WaterBucket && item != Items.LavaBucket)
            {
                var held = new GameObject("Textured block");
                held.transform.SetParent(itemRoot, false);
                held.transform.localPosition = new Vector3(0, .10f, .035f);
                held.transform.localScale = Vector3.one * .23f;
                held.transform.localRotation = Quaternion.Euler(10, 35, 0);
                held.AddComponent<MeshFilter>().sharedMesh = worldRenderer.BlockPreview(block);
                var renderer = held.AddComponent<MeshRenderer>();
                var material = ItemMaterial("Held block atlas", Color.white);
                material.mainTexture = worldRenderer.Atlas;
                renderer.sharedMaterial = material;
                ConfigureRenderer(renderer);
                return;
            }
            BuildSmallItem(item);
        }

        private void FitTool(GameObject prefab, int item)
        {
            var fitted = new GameObject("Fitted tool").transform;
            fitted.SetParent(itemRoot, false);
            var model = Instantiate(prefab, fitted, false);
            bool found = false;
            Bounds bounds = new Bounds();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                Bounds local = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    p = fitted.InverseTransformPoint(filter.transform.TransformPoint(p));
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            float height = item == Items.IronSword || item == Items.CrystalSword ? .65f : .57f;
            float scale = height / Mathf.Max(.001f, bounds.size.y);
            fitted.localScale = Vector3.one * scale;
            fitted.localPosition = -new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .20f, bounds.center.z) * scale;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var source = renderer.sharedMaterials;
                var converted = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    Color color = source[i] != null && source[i].HasProperty("_Color") ? source[i].color : Color.white;
                    string name = source[i] != null ? source[i].name : "Tool material";
                    if (name.ToLowerInvariant().Contains("iron"))
                    {
                        if (item == Items.WoodenPickaxe) color = new Color(.47f, .29f, .13f);
                        if (item == Items.StonePickaxe) color = new Color(.48f, .49f, .50f);
                        if (item == Items.CrystalPickaxe || item == Items.CrystalSword) color = new Color(.27f, .79f, .79f);
                    }
                    converted[i] = ItemMaterial(name, color);
                    if (source[i] != null && source[i].HasProperty("_MainTex")) converted[i].mainTexture = source[i].mainTexture;
                }
                renderer.sharedMaterials = converted;
                ConfigureRenderer(renderer);
            }
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) { collider.enabled = false; Destroy(collider); }
        }

        private void BuildSmallItem(int item)
        {
            var wood = ItemMaterial("Item wood", new Color(.42f, .26f, .12f));
            var metal = ItemMaterial("Item iron", new Color(.67f, .73f, .76f));
            if (item == Items.IronAxe || item == Items.IronShovel || item == Items.IronHoe || item == Items.Stick || item == Items.BlazeRod)
            {
                Cube(itemRoot, "Handle", new Vector3(0, .15f, 0), new Vector3(.045f, .40f, .045f), wood);
                if (item == Items.IronAxe) Cube(itemRoot, "Axe head", new Vector3(-.07f, .34f, 0), new Vector3(.19f, .16f, .052f), metal);
                if (item == Items.IronShovel) Cube(itemRoot, "Shovel head", new Vector3(0, .38f, 0), new Vector3(.12f, .17f, .04f), metal);
                if (item == Items.IronHoe) Cube(itemRoot, "Hoe head", new Vector3(-.055f, .34f, 0), new Vector3(.17f, .045f, .06f), metal);
                return;
            }
            if (item == Items.Shield)
            {
                Cube(itemRoot, "Shield rim", new Vector3(0, .12f, .025f), new Vector3(.32f, .43f, .055f), metal);
                Cube(itemRoot, "Shield boards", new Vector3(0, .12f, .058f), new Vector3(.27f, .38f, .025f), wood);
                return;
            }
            if (item == Items.Bow)
            {
                for (int i = 0; i < 5; i++)
                {
                    float y = (i - 2) * .086f;
                    var piece = Cube(itemRoot, "Bow stave", new Vector3(.055f * (1 - Mathf.Abs(i - 2) * .5f), y + .11f, 0), new Vector3(.035f, .105f, .035f), wood);
                    piece.localRotation = Quaternion.Euler(0, 0, (i - 2) * 12);
                }
                Cube(itemRoot, "Bow string", new Vector3(-.012f, .11f, 0), new Vector3(.008f, .42f, .008f), metal);
                return;
            }
            bool bucket = item == Items.EmptyBucket || item == Items.WaterBucket || item == Items.LavaBucket;
            Color color = Items.IsFood(item) ? new Color(.69f, .29f, .18f) : item == Items.Crystal || item == Items.EnderPearl || item == Items.EyeEnder ? new Color(.23f, .70f, .58f) : new Color(.68f, .70f, .71f);
            if (item == Items.Bread) color = new Color(.70f, .45f, .20f);
            if (item == Items.Berries || item == Items.Apple) color = new Color(.68f, .08f, .09f);
            var material = ItemMaterial(Items.Name(item), color);
            Cube(itemRoot, "Held item", new Vector3(0, .085f, .015f), bucket ? new Vector3(.19f, .19f, .16f) : new Vector3(.19f, .15f, .047f), bucket ? metal : material);
            if (item == Items.WaterBucket || item == Items.LavaBucket)
                Cube(itemRoot, "Bucket contents", new Vector3(0, .185f, .015f), new Vector3(.155f, .01f, .13f), ItemMaterial("Liquid", item == Items.WaterBucket ? new Color(.1f, .35f, .85f) : new Color(1, .25f, .02f)));
        }

        private Material MakeMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("VoxelWilds/FirstPerson")) { name = name, color = color, hideFlags = HideFlags.DontSave };
            ownedMaterials.Add(material);
            return material;
        }

        private Material ItemMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("VoxelWilds/FirstPerson")) { name = name, color = color, hideFlags = HideFlags.DontSave };
            itemMaterials.Add(material);
            return material;
        }

        private static Transform Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            var collider = cube.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            ConfigureRenderer(renderer);
            return cube.transform;
        }

        private static void ConfigureRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void OnDestroy()
        {
            foreach (var material in ownedMaterials) Destroy(material);
            foreach (var material in itemMaterials) Destroy(material);
        }
    }
}
