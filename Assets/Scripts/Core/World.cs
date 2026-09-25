using System;
using System.Collections.Generic;

namespace VoxelWilds.Core
{
    public sealed class World
    {
        public const int Height = 96, ChunkSize = 16;
        private readonly Dictionary<Cell, Voxel[]> chunks = new Dictionary<Cell, Voxel[]>();
        private readonly Dictionary<Cell, Voxel> edits = new Dictionary<Cell, Voxel>();
        private readonly Dictionary<Cell, Dictionary<Cell, Voxel>> chunkEdits = new Dictionary<Cell, Dictionary<Cell, Voxel>>();
        private readonly List<StructureMarker> markers = new List<StructureMarker>();
        public int Seed { get; }
        public Dimension Dimension { get; }
        public IReadOnlyList<StructureMarker> Markers => markers;
        public IEnumerable<KeyValuePair<Cell, Voxel>> Edits => edits;
        public IEnumerable<Cell> LoadedChunks => chunks.Keys;
        public event Action<Cell> Changed;
        public event Action<int, int> ChunkGenerated;
        public World(int seed, Dimension dimension) { Seed = seed; Dimension = dimension; }
        public static int FloorDiv(int value, int divisor) => value >= 0 ? value / divisor : (int)(((long)value - divisor + 1) / divisor);
        internal static int Index(int x, int y, int z) => x + z * ChunkSize + y * ChunkSize * ChunkSize;
        private static Cell ChunkKey(Cell p) => new Cell(FloorDiv(p.X, ChunkSize), 0, FloorDiv(p.Z, ChunkSize));
        public bool IsLoaded(Cell p) => p.Y >= 0 && p.Y < Height && chunks.ContainsKey(ChunkKey(p));
        public void EnsureChunk(int cx, int cz)
        {
            Cell key = new Cell(cx, 0, cz);
            if (chunks.ContainsKey(key)) return;
            var data = new Voxel[ChunkSize * ChunkSize * Height];
            Terrain.Fill(Seed, Dimension, cx, cz, data);
            Structures.Decorate(Seed, Dimension, cx, cz, data, markers);
            if (chunkEdits.TryGetValue(key, out var localEdits))
                foreach (var entry in localEdits) data[Index(entry.Key.X - cx * ChunkSize, entry.Key.Y, entry.Key.Z - cz * ChunkSize)] = entry.Value;
            NormalizeGeneratedDoors(key, data, false);
            chunks.Add(key, data);
            ChunkGenerated?.Invoke(cx, cz);
        }
        private void NormalizeGeneratedDoors(Cell key, Voxel[] data, bool notify)
        {
            chunkEdits.TryGetValue(key, out var localEdits);
            for (int y = 0; y < Height; y++)
            for (int z = 0; z < ChunkSize; z++)
            for (int x = 0; x < ChunkSize; x++)
            {
                int index = Index(x, y, z);
                Voxel door = data[index];
                if (door.Id != Block.Door) continue;
                Cell position = new Cell(key.X * ChunkSize + x, y, key.Z * ChunkSize + z);
                if (localEdits != null && localEdits.ContainsKey(position)) continue;
                int otherY = y + (DoorRules.IsUpper(door) ? -1 : 1);
                bool supported = DoorRules.IsUpper(door) || y > 0 && DoorRules.Supports(data[Index(x, y - 1, z)].Id);
                if (!supported || otherY < 0 || otherY >= Height || !DoorRules.Paired(door, data[Index(x, otherY, z)]))
                {
                    data[index] = new Voxel(Block.Air);
                    if (notify) Changed?.Invoke(position);
                }
            }
        }
        public Voxel Get(Cell p)
        {
            if (p.Y < 0) return new Voxel(Dimension == Dimension.End ? Block.Air : Block.Bedrock);
            if (p.Y >= Height) return new Voxel(Block.Air);
            Cell key = ChunkKey(p);
            EnsureChunk(key.X, key.Z);
            return chunks[key][Index(p.X - key.X * ChunkSize, p.Y, p.Z - key.Z * ChunkSize)];
        }
        public Block GetBlock(Cell p) => Get(p).Id;
        public bool Solid(Cell p) { Voxel voxel = Get(p); return Blocks.IsSolid(voxel.Id) && !DoorRules.IsOpen(voxel); }
        public void Set(Cell p, Block id, byte level = 0)
        {
            if (p.Y < 0 || p.Y >= Height || (int)id > (int)Block.EndFrame) return;
            var value = new Voxel(id, level);
            Voxel previous = Get(p);
            if (previous.Equals(value)) return;
            Cell key = ChunkKey(p);
            chunks[key][Index(p.X - key.X * ChunkSize, p.Y, p.Z - key.Z * ChunkSize)] = value;
            StoreEdit(p, value);
            Changed?.Invoke(p);
            if (Blocks.IsBed(previous.Id) && id != previous.Id)
            {
                Cell partner = BedRules.Partner(p, previous.Id);
                if (BedRules.Paired(previous.Id, GetBlock(partner))) Set(partner, Block.Air);
            }
            if (previous.Id == Block.Door && id != Block.Door)
            {
                Cell partner = DoorRules.Partner(p, previous);
                if (DoorRules.Paired(previous, Get(partner))) Set(partner, Block.Air);
            }
            if (DoorRules.Supports(previous.Id) && !DoorRules.Supports(id) && IsLoaded(p.Up))
            {
                Voxel above = Get(p.Up);
                if (above.Id == Block.Door && !DoorRules.IsUpper(above)) Set(p.Up, Block.Air);
            }
        }
        private void StoreEdit(Cell p, Voxel value)
        {
            edits[p] = value;
            Cell key = ChunkKey(p);
            if (!chunkEdits.TryGetValue(key, out var local)) { local = new Dictionary<Cell, Voxel>(); chunkEdits.Add(key, local); }
            local[p] = value;
        }
        public void ApplyEdits(IEnumerable<KeyValuePair<Cell, Voxel>> values)
        {
            if (values == null) return;
            var changedChunks = new HashSet<Cell>();
            foreach (var entry in values)
            {
                Cell p = entry.Key;
                if (p.Y < 0 || p.Y >= Height || (int)entry.Value.Id > (int)Block.EndFrame) continue;
                var value = new Voxel(entry.Value.Id, entry.Value.Level);
                StoreEdit(p, value);
                Cell key = ChunkKey(p);
                if (chunks.TryGetValue(key, out var data))
                {
                    changedChunks.Add(key);
                    int index = Index(p.X - key.X * ChunkSize, p.Y, p.Z - key.Z * ChunkSize);
                    if (data[index].Equals(value)) continue;
                    data[index] = value;
                    Changed?.Invoke(p);
                }
            }
            foreach (Cell key in changedChunks) NormalizeGeneratedDoors(key, chunks[key], true);
        }
        public int HeightAt(int x, int z)
        {
            for (int y = Height - 1; y >= 0; y--) if (Solid(new Cell(x, y, z))) return y;
            return -1;
        }
        public bool SkyVisible(Cell p)
        {
            for (int y = Math.Max(0, p.Y + 1); y < Height; y++) if (!Blocks.IsTransparent(GetBlock(new Cell(p.X, y, p.Z)))) return false;
            return true;
        }
        public IEnumerable<KeyValuePair<Cell, Voxel>> GeneratedVoxels
        {
            get
            {
                foreach (var chunk in chunks)
                for (int y = 0; y < Height; y++)
                for (int z = 0; z < ChunkSize; z++)
                for (int x = 0; x < ChunkSize; x++)
                {
                    var v = chunk.Value[Index(x, y, z)];
                    if (v.Id != Block.Air) yield return new KeyValuePair<Cell, Voxel>(new Cell(chunk.Key.X * ChunkSize + x, y, chunk.Key.Z * ChunkSize + z), v);
                }
            }
        }
    }
}
