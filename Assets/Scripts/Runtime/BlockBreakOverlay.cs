using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class BlockBreakOverlay : MonoBehaviour
    {
        private static readonly Cell[] Neighbors = { new Cell(1, 0, 0), new Cell(-1, 0, 0), new Cell(0, 1, 0), new Cell(0, -1, 0), new Cell(0, 0, 1), new Cell(0, 0, -1) };
        private static readonly Vector3[] Normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        private static readonly Vector3[][] Corners = {
            new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
            new[] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },
            new[] { new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0), new Vector3(0,1,0) },
            new[] { new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1) },
            new[] { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1) },
            new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) }
        };
        private readonly List<Vector3> vertices = new List<Vector3>(24);
        private readonly List<Vector2> uvs = new List<Vector2>(24);
        private readonly List<Color32> colors = new List<Color32>(24);
        private readonly List<int> triangles = new List<int>(36);
        private readonly Texture2D[] stages = new Texture2D[10];
        private World world;
        private Mesh mesh;
        private Material material;
        private MeshRenderer view;
        private Cell target;
        private Voxel voxel;
        private bool dirty = true;

        public bool IsVisible => view && view.enabled;
        public int Stage { get; private set; } = -1;
        public Bounds SurfaceBounds { get; private set; }
        public int FaceCount { get; private set; }

        public void Init(World value)
        {
            if (world != null) world.Changed -= Changed;
            world = value;
            if (world != null) world.Changed += Changed;
            EnsureResources();
            Hide();
        }

        public Texture2D TextureForStage(int stage)
        {
            EnsureResources();
            return stages[Mathf.Clamp(stage, 0, 9)];
        }

        public void Show(Cell cell, Voxel value, float progress)
        {
            if (world == null || value.Id == Block.Air || Blocks.IsFluid(value.Id) || float.IsNaN(progress) || float.IsInfinity(progress) || progress <= 0)
            {
                Hide();
                return;
            }
            EnsureResources();
            if (target != cell || !voxel.Equals(value) || !IsVisible) dirty = true;
            target = cell;
            voxel = value;
            if (dirty) Rebuild();
            if (FaceCount == 0) { Hide(); return; }
            Stage = Mathf.Clamp(Mathf.FloorToInt(progress * 10), 0, 9);
            material.mainTexture = stages[Stage];
            view.enabled = FaceCount > 0;
        }

        public void Hide()
        {
            if (view) view.enabled = false;
            Stage = -1;
            dirty = true;
        }

        private void EnsureResources()
        {
            if (mesh) return;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
            mesh = new Mesh { name = "Block fracture surfaces" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Shader.Find("VoxelWilds/BlockBreakOverlay")) { name = "Block fractures" };
            view = gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = material;
            view.shadowCastingMode = ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.enabled = false;
            BuildTextures();
        }

        private void Changed(Cell cell)
        {
            if (cell == target)
            {
                Hide();
                return;
            }
            if (Mathf.Abs(cell.X - target.X) + Mathf.Abs(cell.Y - target.Y) + Mathf.Abs(cell.Z - target.Z) == 1) dirty = true;
        }

        private void Rebuild()
        {
            SurfaceBounds = VisualBounds(target, voxel);
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
            FaceCount = 0;
            for (int face = 0; face < 6; face++)
            {
                if (Covered(face)) continue;
                int first = vertices.Count;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 local = Corners[face][corner];
                    vertices.Add(SurfaceBounds.min + Vector3.Scale(local, SurfaceBounds.size) + Normals[face] * .0015f);
                    float u = corner == 1 || corner == 2 ? 1 : 0;
                    float v = corner >= 2 ? 1 : 0;
                    if (face < 2) { u *= SurfaceBounds.size.y; v *= SurfaceBounds.size.z; }
                    else if (face < 4) { u *= SurfaceBounds.size.x; v *= SurfaceBounds.size.z; }
                    else { u *= SurfaceBounds.size.y; v *= SurfaceBounds.size.x; }
                    uvs.Add(new Vector2(u, v));
                    colors.Add(new Color32(255, 255, 255, 255));
                }
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
                FaceCount++;
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            dirty = false;
        }

        private bool Covered(int face)
        {
            Cell offset = Neighbors[face];
            Cell neighborCell = new Cell(target.X + offset.X, target.Y + offset.Y, target.Z + offset.Z);
            Voxel neighbor = world.Get(neighborCell);
            if (Blocks.IsTransparent(neighbor.Id) && !Blocks.IsBed(neighbor.Id) && neighbor.Id != Block.Door) return false;
            Bounds other = BlockShape.Bounds(neighborCell, neighbor);
            int axis = face / 2;
            int a = axis == 0 ? 1 : 0, b = axis == 2 ? 1 : 2;
            float plane = (face & 1) == 0 ? SurfaceBounds.max[axis] : SurfaceBounds.min[axis];
            float opposing = (face & 1) == 0 ? other.min[axis] : other.max[axis];
            return Mathf.Abs(plane - opposing) < .0001f && other.min[a] <= SurfaceBounds.min[a] && other.max[a] >= SurfaceBounds.max[a]
                && other.min[b] <= SurfaceBounds.min[b] && other.max[b] >= SurfaceBounds.max[b];
        }

        internal static Bounds VisualBounds(Cell cell, Voxel voxel)
        {
            Vector3 size;
            switch (voxel.Id)
            {
                case Block.Chest: size = new Vector3(.86f, .8f, .86f); break;
                case Block.Lantern: size = new Vector3(.38f, .65f, .38f); break;
                case Block.Campfire: size = new Vector3(.8f, .6f, .8f); break;
                case Block.Torch: size = new Vector3(.13f, .52f, .13f); break;
                default: return BlockShape.Bounds(cell, voxel);
            }
            return new Bounds(new Vector3(cell.X + .5f, cell.Y + size.y * .5f, cell.Z + .5f), size);
        }

        private void BuildTextures()
        {
            var mask = new bool[16 * 16];
            int[][] paths = {
                new[] { 7,8, 8,9 },
                new[] { 7,8, 5,7, 4,7, -1,-1, 8,9, 9,11 },
                new[] { 7,8, 8,6, 9,5, -1,-1, 8,9, 6,11 },
                new[] { 9,11, 11,11, 13,13, -1,-1, 4,7, 3,5, 1,4 },
                new[] { 9,5, 11,4, 12,2, -1,-1, 6,11, 5,13, 5,15 },
                new[] { 13,13, 15,13, -1,-1, 1,4, 0,3, -1,-1, 12,2, 11,0, -1,-1, 9,5, 7,3, 6,3 },
                new[] { 6,3, 5,1, 3,0, -1,-1, 4,7, 2,9, 0,9, -1,-1, 11,11, 11,8, 13,7 },
                new[] { 13,7, 15,6, -1,-1, 6,11, 3,11, 2,13, 0,14, -1,-1, 9,11, 9,14, 10,15 },
                new[] { 12,2, 14,3, 15,3, -1,-1, 7,3, 9,2, 11,0, -1,-1, 2,9, 1,7, 0,7, -1,-1, 13,13, 12,15 },
                new[] { 3,5, 4,3, 6,3, -1,-1, 8,6, 11,8, -1,-1, 3,11, 3,14, 5,15, -1,-1, 13,7, 13,10, 15,11 }
            };
            for (int stage = 0; stage < stages.Length; stage++)
            {
                int[] path = paths[stage];
                for (int i = 2; i < path.Length; i += 2)
                    if (path[i - 2] >= 0 && path[i] >= 0) Line(mask, path[i - 2], path[i - 1], path[i], path[i + 1]);
                var pixels = new Color32[mask.Length];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = mask[i] ? new Color32(17, 14, 13, 205) : new Color32(0, 0, 0, 0);
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
                {
                    name = "Block cracks " + stage,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 0
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                stages[stage] = texture;
            }
        }

        private static void Line(bool[] mask, int x, int y, int endX, int endY)
        {
            int dx = Mathf.Abs(endX - x), dy = -Mathf.Abs(endY - y), sx = x < endX ? 1 : -1, sy = y < endY ? 1 : -1, error = dx + dy;
            while (true)
            {
                mask[x + y * 16] = true;
                if (x == endX && y == endY) break;
                int twice = 2 * error;
                if (twice >= dy) { error += dy; x += sx; }
                if (twice <= dx) { error += dx; y += sy; }
            }
        }

        private void OnDestroy()
        {
            if (world != null) world.Changed -= Changed;
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
            foreach (Texture2D texture in stages) if (texture) Destroy(texture);
        }
    }
}
