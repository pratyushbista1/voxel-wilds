using System;
using System.Collections.Generic;
using System.Linq;

namespace VoxelWilds.Core
{
    public readonly struct PortalFrame
    {
        public Cell Origin { get; }
        public int Width { get; }
        public int Height { get; }
        public bool AlongX { get; }
        public Block Portal => AlongX ? Block.PortalX : Block.PortalZ;
        public PortalFrame(Cell origin, int width, int height, bool alongX)
        {
            Origin = origin; Width = width; Height = height; AlongX = alongX;
        }
        public Cell At(int x, int y) => Origin + new Cell(AlongX ? x : 0, y, AlongX ? 0 : x);
        public IEnumerable<Cell> Interior
        {
            get { for (int y = 1; y <= Height; y++) for (int x = 1; x <= Width; x++) yield return At(x, y); }
        }
    }

    public static class PortalRules
    {
        public const int MinWidth = 2, MinHeight = 3, MaxInterior = 21;
        public static bool IsPortal(Block id) => id == Block.PortalX || id == Block.PortalZ;
        private static bool Interior(Block id, Block portal) => id == Block.Air || id == portal;

        public static bool TryFind(World world, Cell inside, out PortalFrame frame)
        {
            frame = default;
            if (world == null || world.Dimension == Dimension.End) return false;
            return TryFind(world, inside, true, out frame) || TryFind(world, inside, false, out frame);
        }

        public static bool TryFind(World world, Cell inside, bool alongX, out PortalFrame frame)
        {
            frame = default;
            if (world == null || world.Dimension == Dimension.End || inside.Y < 1 || inside.Y >= World.Height - 1) return false;
            Block portal = alongX ? Block.PortalX : Block.PortalZ;
            if (!Interior(world.GetBlock(inside), portal)) return false;
            Cell bottom = inside;
            int depth = 0;
            while (bottom.Y > 0 && Interior(world.GetBlock(bottom.Down), portal) && depth < MaxInterior)
            {
                bottom = bottom.Down; depth++;
            }
            if (depth >= MaxInterior || world.GetBlock(bottom.Down) != Block.Obsidian) return false;
            Cell step = alongX ? new Cell(1, 0, 0) : new Cell(0, 0, 1);
            Cell left = bottom;
            int distance = 0;
            while (Interior(world.GetBlock(left - step), portal) && distance < MaxInterior)
            {
                left -= step; distance++;
            }
            if (distance >= MaxInterior || world.GetBlock(left - step) != Block.Obsidian) return false;
            int width = 0;
            Cell probe = left;
            while (width <= MaxInterior && Interior(world.GetBlock(probe), portal))
            {
                if (world.GetBlock(probe.Down) != Block.Obsidian) return false;
                width++; probe += step;
            }
            if (width < MinWidth || width > MaxInterior || world.GetBlock(probe) != Block.Obsidian) return false;
            Cell origin = left - step + new Cell(0, -1, 0);
            for (int height = 0; height <= MaxInterior; height++)
            {
                bool top = true;
                for (int x = 0; x < width; x++)
                {
                    Cell p = left + new Cell(alongX ? x : 0, height, alongX ? 0 : x);
                    if (world.GetBlock(p) != Block.Obsidian) { top = false; break; }
                }
                if (top)
                {
                    if (height < MinHeight || inside.Y >= left.Y + height) return false;
                    frame = new PortalFrame(origin, width, height, alongX);
                    return true;
                }
                if (height == MaxInterior) return false;
                Cell row = left + new Cell(0, height, 0);
                if (world.GetBlock(row - step) != Block.Obsidian || world.GetBlock(row + new Cell(alongX ? width : 0, 0, alongX ? 0 : width)) != Block.Obsidian) return false;
                for (int x = 0; x < width; x++)
                    if (!Interior(world.GetBlock(row + new Cell(alongX ? x : 0, 0, alongX ? 0 : x)), portal)) return false;
            }
            return false;
        }

        public static bool IsLit(World world, PortalFrame frame)
        {
            if (!TryFind(world, frame.At(1, 1), frame.AlongX, out var actual) || actual.Origin != frame.Origin || actual.Width != frame.Width || actual.Height != frame.Height) return false;
            foreach (Cell p in frame.Interior) if (world.GetBlock(p) != frame.Portal) return false;
            return true;
        }

        public static bool TryIgnite(World world, Cell inside, out PortalFrame frame)
        {
            if (!TryFind(world, inside, out frame) || IsLit(world, frame)) return false;
            foreach (Cell p in frame.Interior) world.Set(p, frame.Portal);
            return true;
        }

        public static bool TryNearest(World world, Cell target, int radius, out PortalFrame frame)
        {
            frame = default;
            long best = long.MaxValue;
            var seen = new HashSet<Cell>();
            var candidates = world.Edits.Where(e => IsPortal(e.Value.Id)).Select(e => e.Key).ToArray();
            foreach (Cell p in candidates)
            {
                if (Math.Abs((long)p.X - target.X) > radius || Math.Abs((long)p.Z - target.Z) > radius || seen.Contains(p)) continue;
                if (!TryFind(world, p, out var candidate) || !IsLit(world, candidate)) continue;
                foreach (Cell cell in candidate.Interior) seen.Add(cell);
                Cell first = candidate.At(1, 1), last = candidate.At(candidate.Width, candidate.Height);
                long dx = Math.Max(first.X, Math.Min(last.X, target.X)) - (long)target.X;
                long dy = Math.Max(first.Y, Math.Min(last.Y, target.Y)) - (long)target.Y;
                long dz = Math.Max(first.Z, Math.Min(last.Z, target.Z)) - (long)target.Z;
                long distance = dx * dx + dy * dy + dz * dz;
                if (distance >= best) continue;
                best = distance; frame = candidate;
            }
            return best != long.MaxValue;
        }
    }

    public sealed class PortalSimulation : IDisposable
    {
        private readonly World world;
        private readonly HashSet<Cell> pending = new HashSet<Cell>();
        public PortalSimulation(World world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            foreach (var edit in world.Edits) if (PortalRules.IsPortal(edit.Value.Id)) pending.Add(edit.Key);
            world.Changed += Changed;
        }
        public void Dispose() { world.Changed -= Changed; pending.Clear(); }
        private void Changed(Cell p)
        {
            if (PortalRules.IsPortal(world.GetBlock(p))) pending.Add(p);
            foreach (Cell side in Cell.Cardinal)
            {
                Cell neighbor = p + side;
                if (world.IsLoaded(neighbor) && PortalRules.IsPortal(world.GetBlock(neighbor))) pending.Add(neighbor);
            }
        }
        public void Tick()
        {
            if (pending.Count == 0) return;
            var changed = pending.ToArray(); pending.Clear();
            var checkedCells = new HashSet<Cell>();
            foreach (Cell p in changed)
            {
                Block id = world.GetBlock(p);
                if (!PortalRules.IsPortal(id) || checkedCells.Contains(p)) continue;
                if (PortalRules.TryFind(world, p, id == Block.PortalX, out var frame) && PortalRules.IsLit(world, frame))
                {
                    foreach (Cell cell in frame.Interior) checkedCells.Add(cell);
                    continue;
                }
                var queue = new Queue<Cell>(); queue.Enqueue(p); checkedCells.Add(p);
                Cell along = id == Block.PortalX ? new Cell(1, 0, 0) : new Cell(0, 0, 1);
                while (queue.Count > 0)
                {
                    Cell cell = queue.Dequeue();
                    world.Set(cell, Block.Air);
                    foreach (Cell neighbor in new[] { cell + along, cell - along, cell.Up, cell.Down })
                        if (world.GetBlock(neighbor) == id && checkedCells.Add(neighbor)) queue.Enqueue(neighbor);
                }
            }
        }
    }
}
