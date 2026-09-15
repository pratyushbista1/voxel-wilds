using System;
using System.Collections.Generic;

namespace VoxelWilds.Core
{
    public sealed class FluidSimulation : IDisposable
    {
        private const int MaximumPending = 100000;
        private readonly World world;
        private readonly SortedDictionary<long, Queue<Cell>> queue = new SortedDictionary<long, Queue<Cell>>();
        private readonly Dictionary<Cell, long> pending = new Dictionary<Cell, long>();
        private double time;
        private long processingTick;
        private bool processing;
        public int PendingCount => pending.Count;
        public long ElapsedTicks => (long)Math.Floor(time);
        public int LastProcessedCount { get; private set; }
        public FluidSimulation(World world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            world.Changed += Wake;
            world.ChunkGenerated += OnChunkGenerated;
            RebuildActive();
        }
        public void Dispose() { world.Changed -= Wake; world.ChunkGenerated -= OnChunkGenerated; queue.Clear(); pending.Clear(); }
        public int DelayTicks(Block fluid) => fluid == Block.Lava ? (world.Dimension == Dimension.Nether ? 10 : 30) : 5;
        private int Decay(Block fluid) => fluid == Block.Lava && world.Dimension != Dimension.Nether ? 2 : 1;
        private Voxel Known(Cell p) => world.IsLoaded(p) ? world.Get(p) : new Voxel(Block.Bedrock);
        private bool CanOccupy(Cell p, Block fluid)
        {
            if (!world.IsLoaded(p)) return false;
            Block id = Known(p).Id;
            return Blocks.IsReplaceable(id) && (id != Block.Water && id != Block.Lava || id == fluid);
        }
        private void Schedule(Cell p)
        {
            if (!world.IsLoaded(p) || pending.Count >= MaximumPending) return;
            var value = Known(p);
            if (!Blocks.IsFluid(value.Id)) return;
            long due = (processing ? processingTick : ElapsedTicks) + DelayTicks(value.Id);
            if (pending.TryGetValue(p, out long existing) && existing <= due) return;
            pending[p] = due;
            if (!queue.TryGetValue(due, out var bucket)) { bucket = new Queue<Cell>(); queue.Add(due, bucket); }
            bucket.Enqueue(p);
        }
        public void Wake(Cell p)
        {
            Schedule(p);
            foreach (Cell side in Cell.Cardinal) Schedule(p + side);
            foreach (Cell side in Cell.Sides) Schedule(p.Up + side);
        }
        public void RebuildActive()
        {
            queue.Clear(); pending.Clear();
            foreach (var entry in world.GeneratedVoxels)
                if (Blocks.IsFluid(entry.Value.Id) && IsBoundary(entry.Key, entry.Value)) Schedule(entry.Key);
        }
        private bool IsBoundary(Cell p, Voxel fluid)
        {
            if (fluid.Level != 0 || world.Dimension == Dimension.Nether && fluid.Id == Block.Water) return true;
            foreach (Cell side in Cell.Cardinal)
            {
                var neighbor = Known(p + side);
                if (neighbor.Id != fluid.Id && Blocks.IsReplaceable(neighbor.Id)) return true;
            }
            return false;
        }
        private void OnChunkGenerated(int cx, int cz)
        {
            for (int z = cz * 16; z < cz * 16 + 16; z++)
            for (int x = cx * 16; x < cx * 16 + 16; x++)
            for (int y = 0; y < World.Height; y++)
            {
                Cell p = new Cell(x, y, z);
                var v = Known(p);
                if (Blocks.IsFluid(v.Id) && IsBoundary(p, v)) Schedule(p);
                if (x == cx * 16 || z == cz * 16 || x == cx * 16 + 15 || z == cz * 16 + 15)
                    foreach (Cell side in Cell.Sides) Schedule(p + side);
            }
        }
        public void Tick(float seconds, int budget = 1024)
        {
            LastProcessedCount = 0;
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || budget <= 0) return;
            time += seconds * 20.0;
            long target = ElapsedTicks;
            while (queue.Count > 0 && LastProcessedCount < budget)
            {
                long due; Queue<Cell> bucket;
                using (var it = queue.GetEnumerator()) { it.MoveNext(); due = it.Current.Key; bucket = it.Current.Value; }
                if (due > target) break;
                Cell p = bucket.Dequeue();
                if (bucket.Count == 0) queue.Remove(due);
                if (!pending.TryGetValue(p, out long actual) || actual != due) continue;
                pending.Remove(p);
                processingTick = due; processing = true;
                try { Step(p); }
                finally { processing = false; }
                LastProcessedCount++;
            }
        }
        private void Step(Cell p)
        {
            var fluid = Known(p);
            if (!Blocks.IsFluid(fluid.Id)) return;
            if (fluid.Id == Block.Water && world.Dimension == Dimension.Nether)
            {
                world.Set(p, Block.Air); return;
            }
            if (Mix(p, fluid)) return;
            if (fluid.Level != 0)
            {
                int desired = IncomingLevel(p, fluid.Id);
                if (desired < 0) { world.Set(p, Block.Air); return; }
                if (desired != fluid.Level) { world.Set(p, fluid.Id, (byte)desired); fluid.Level = (byte)desired; }
            }
            Cell down = p.Down;
            var below = Known(down);
            if (fluid.Id == Block.Lava && below.Id == Block.Water)
            {
                world.Set(down, Block.Stone); return;
            }
            if (CanOccupy(down, fluid.Id) && !(below.Id == fluid.Id && below.Level == 0))
            {
                if (below.Id != fluid.Id || below.Level != 8) world.Set(down, fluid.Id, 8);
                return;
            }
            int lateral = (fluid.Level == 8 ? 0 : fluid.Level) + Decay(fluid.Id);
            if (lateral > 7) return;
            int best = int.MaxValue;
            var distances = new int[4];
            for (int i = 0; i < Cell.Sides.Length; i++)
            {
                Cell next = p + Cell.Sides[i];
                if (!CanSpreadInto(next, fluid.Id, lateral)) { distances[i] = int.MaxValue; continue; }
                int distance = DropDistance(next, p, fluid.Id, 0, fluid.Id == Block.Lava && world.Dimension != Dimension.Nether ? 2 : 4);
                distances[i] = distance;
                best = Math.Min(best, distance);
            }
            for (int i = 0; i < Cell.Sides.Length; i++)
            {
                Cell next = p + Cell.Sides[i];
                if (distances[i] == best && CanSpreadInto(next, fluid.Id, lateral)) world.Set(next, fluid.Id, (byte)lateral);
            }
        }
        private bool CanSpreadInto(Cell p, Block fluid, int level)
        {
            var target = Known(p);
            if (!CanOccupy(p, fluid)) return false;
            return target.Id != fluid || target.Level > level && target.Level != 8;
        }
        private int IncomingLevel(Cell p, Block fluid)
        {
            var above = Known(p.Up);
            int minimum = 100, sources = 0;
            foreach (Cell side in Cell.Sides)
            {
                Cell neighborPosition = p + side;
                var neighbor = Known(neighborPosition);
                if (neighbor.Id != fluid) continue;
                if (neighbor.Level == 0) sources++;
                var support = Known(neighborPosition.Down);
                if (CanOccupy(neighborPosition.Down, fluid) && !(support.Id == fluid && support.Level == 0)) continue;
                if (neighbor.Level == 8)
                {
                    if (Blocks.IsSolid(support.Id) || support.Id == fluid && support.Level == 0) minimum = Math.Min(minimum, 0);
                }
                else minimum = Math.Min(minimum, neighbor.Level);
            }
            var below = Known(p.Down);
            if (fluid == Block.Water && sources >= 2 && (Blocks.IsSolid(below.Id) || below.Id == Block.Water && below.Level == 0)) return 0;
            if (above.Id == fluid) return 8;
            int level = minimum + Decay(fluid);
            return level <= 7 ? level : -1;
        }
        private int DropDistance(Cell p, Cell previous, Block fluid, int depth, int limit)
        {
            var below = Known(p.Down);
            if (CanOccupy(p.Down, fluid) && (below.Id != fluid || below.Level != 0)) return depth;
            if (depth >= limit) return 1000;
            int result = 1000;
            foreach (Cell side in Cell.Sides)
            {
                Cell next = p + side;
                if (next == previous || !CanOccupy(next, fluid)) continue;
                var neighbor = Known(next);
                if (neighbor.Id == fluid && neighbor.Level == 0) continue;
                result = Math.Min(result, DropDistance(next, p, fluid, depth + 1, limit));
            }
            return result;
        }
        private bool Mix(Cell p, Voxel fluid)
        {
            if (fluid.Id == Block.Lava)
            {
                bool water = Known(p.Up).Id == Block.Water;
                foreach (Cell side in Cell.Sides) water |= Known(p + side).Id == Block.Water;
                if (water) { world.Set(p, fluid.Level == 0 ? Block.Obsidian : Block.Cobble); return true; }
            }
            else
            {
                HardenLava(p.Down);
                foreach (Cell side in Cell.Sides) HardenLava(p + side);
            }
            return false;
        }
        private void HardenLava(Cell p)
        {
            var lava = Known(p);
            if (lava.Id == Block.Lava) world.Set(p, lava.Level == 0 ? Block.Obsidian : Block.Cobble);
        }
    }
}
