using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class BlockBreakParticles : MonoBehaviour
    {
        public const int Capacity = 128;
        private struct Chip
        {
            public Vector3 Position, Velocity;
            public Vector2 UvMin, UvMax;
            public float Life, Duration, Size;
            public byte Shade;
        }

        private readonly Chip[] chips = new Chip[Capacity];
        private readonly List<Vector3> vertices = new List<Vector3>(Capacity * 4);
        private readonly List<Vector2> uvs = new List<Vector2>(Capacity * 4);
        private readonly List<Color32> colors = new List<Color32>(Capacity * 4);
        private readonly List<int> triangles = new List<int>(Capacity * 6);
        private GameSession game;
        private Mesh mesh;
        private Material material;
        private MeshRenderer view;
        private uint random = 835471;
        private float nextHit;
        private int cursor;

        public int ActiveCount { get; private set; }

        public void Init(GameSession value)
        {
            game = value;
            if (!mesh)
            {
                transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                transform.localScale = Vector3.one;
                mesh = new Mesh { name = "Mining chips" };
                mesh.MarkDynamic();
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                material = new Material(Shader.Find("VoxelWilds/BlockBreakOverlay")) { name = "Mining chip atlas" };
                view = gameObject.AddComponent<MeshRenderer>();
                view.sharedMaterial = material;
                view.shadowCastingMode = ShadowCastingMode.Off;
                view.receiveShadows = false;
            }
            if (game && game.Renderer) material.mainTexture = game.Renderer.Atlas;
            Clear();
        }

        public void Hit(Cell cell, Voxel voxel, Vector3 hitpoint)
        {
            if (!Ready(voxel.Id) || Time.time < nextHit) return;
            nextHit = Time.time + .1f;
            Bounds bounds = BlockBreakOverlay.VisualBounds(cell, voxel);
            Vector3 point = bounds.ClosestPoint(hitpoint), direction = Vector3.up;
            int face = 2;
            float closest = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                float low = Mathf.Abs(point[axis] - bounds.min[axis]), high = Mathf.Abs(point[axis] - bounds.max[axis]);
                if (low < closest) { closest = low; direction = Vector3.zero; direction[axis] = -1; face = axis * 2 + 1; }
                if (high < closest) { closest = high; direction = Vector3.zero; direction[axis] = 1; face = axis * 2; }
            }
            for (int i = 0; i < 3; i++)
            {
                Vector3 scatter = new Vector3(Range(-.1f, .1f), Range(-.1f, .1f), Range(-.1f, .1f));
                scatter -= direction * Vector3.Dot(scatter, direction);
                Add(voxel.Id, face, point + direction * .025f + scatter,
                    direction * Range(.3f, .8f) + new Vector3(Range(-.3f, .3f), Range(.5f, 1.3f), Range(-.3f, .3f)), Range(.035f, .065f));
            }
        }

        public void Burst(Cell cell, Voxel voxel)
        {
            if (!Ready(voxel.Id)) return;
            Bounds bounds = BlockBreakOverlay.VisualBounds(cell, voxel);
            for (int z = 0; z < 4; z++)
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                Vector3 local = new Vector3((x + .5f) / 4, (y + .5f) / 4, (z + .5f) / 4);
                Vector3 position = bounds.min + Vector3.Scale(local, bounds.size);
                Vector3 velocity = (local - Vector3.one * .5f) * Range(2, 3.2f) + Vector3.up * .6f;
                Add(voxel.Id, (int)Range(0, 5.999f), position, velocity, Range(.045f, .09f));
            }
        }

        private bool Ready(Block block) => mesh && game && game.World != null && game.Renderer && game.Renderer.Atlas
            && block != Block.Air && !Blocks.IsFluid(block) && block != Block.PortalX && block != Block.PortalZ && block != Block.EndPortal;

        private void Add(Block block, int face, Vector3 position, Vector3 velocity, float size)
        {
            int index = cursor++ % Capacity;
            if (chips[index].Life <= 0) ActiveCount++;
            float u = Mathf.Floor(Range(0, 12.999f)) / 16, v = Mathf.Floor(Range(0, 12.999f)) / 16;
            float duration = Range(.35f, .8f);
            chips[index] = new Chip {
                Position = position, Velocity = velocity, Size = size, Life = duration, Duration = duration,
                UvMin = BlockTextureAtlas.Uv(block, face, u, v), UvMax = BlockTextureAtlas.Uv(block, face, u + .1875f, v + .1875f),
                Shade = (byte)Range(155, 231)
            };
        }

        private void LateUpdate()
        {
            if (!mesh || !game || game.Player == null || game.Player.Eye == null) return;
            if (game.World == null) { Clear(); return; }
            if (ActiveCount == 0) return;
            float dt = game.Paused ? 0 : Mathf.Min(Time.deltaTime, .05f);
            Vector3 right = game.Player.Eye.transform.right, up = game.Player.Eye.transform.up;
            vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear();
            for (int i = 0; i < chips.Length; i++)
            {
                Chip chip = chips[i];
                if (chip.Life <= 0) continue;
                chip.Life -= dt;
                if (chip.Life <= 0) { chips[i] = chip; ActiveCount--; continue; }
                chip.Velocity += Vector3.down * (8 * dt);
                Vector3 next = chip.Position + chip.Velocity * dt;
                Cell cell = new Cell(Mathf.FloorToInt(next.x), Mathf.FloorToInt(next.y), Mathf.FloorToInt(next.z));
                Voxel block = game.World.Get(cell);
                if (Blocks.IsSolid(block.Id) && BlockShape.Bounds(cell, block).Contains(next))
                {
                    Bounds bounds = BlockShape.Bounds(cell, block);
                    if (chip.Position.y >= bounds.max.y && chip.Velocity.y < 0)
                    {
                        next.y = bounds.max.y + .003f;
                        chip.Velocity = new Vector3(chip.Velocity.x * .55f, 0, chip.Velocity.z * .55f);
                    }
                    else { next = chip.Position; chip.Velocity *= .2f; }
                }
                chip.Position = next;
                chips[i] = chip;
                float size = chip.Size * Mathf.Min(1, chip.Life / .12f);
                Vector3 r = right * size, u = up * size;
                int first = vertices.Count;
                vertices.Add(next - r - u); vertices.Add(next - r + u); vertices.Add(next + r + u); vertices.Add(next + r - u);
                uvs.Add(chip.UvMin); uvs.Add(new Vector2(chip.UvMin.x, chip.UvMax.y)); uvs.Add(chip.UvMax); uvs.Add(new Vector2(chip.UvMax.x, chip.UvMin.y));
                var color = new Color32(chip.Shade, chip.Shade, chip.Shade, 255);
                for (int corner = 0; corner < 4; corner++) colors.Add(color);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }
            mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            view.enabled = ActiveCount > 0;
        }

        public void Clear()
        {
            System.Array.Clear(chips, 0, chips.Length);
            ActiveCount = 0;
            cursor = 0;
            nextHit = 0;
            if (view) view.enabled = false;
            if (mesh) mesh.Clear();
        }

        private float Range(float minimum, float maximum)
        {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            return minimum + (maximum - minimum) * (random & 0xffffff) / 16777216f;
        }

        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }
    }
}
