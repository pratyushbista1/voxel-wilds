using System;

namespace VoxelWilds.Core
{
    public readonly struct Cell : IEquatable<Cell>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public Cell(int x, int y, int z) { X = x; Y = y; Z = z; }
        public Cell Up => new Cell(X, Y + 1, Z);
        public Cell Down => new Cell(X, Y - 1, Z);
        public static readonly Cell[] Sides = { new Cell(1, 0, 0), new Cell(-1, 0, 0), new Cell(0, 0, 1), new Cell(0, 0, -1) };
        public static readonly Cell[] Cardinal = { new Cell(1, 0, 0), new Cell(-1, 0, 0), new Cell(0, 0, 1), new Cell(0, 0, -1), new Cell(0, 1, 0), new Cell(0, -1, 0) };
        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Cell operator -(Cell a, Cell b) => new Cell(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
        public bool Equals(Cell other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() { unchecked { return (X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791); } }
        public override string ToString() => X + "," + Y + "," + Z;
    }

    public struct Voxel : IEquatable<Voxel>
    {
        public Block Id;
        public byte Level;
        public Voxel(Block id, byte level = 0) { Id = id; Level = Blocks.IsFluid(id) ? (byte)Math.Min(8, (int)level) : (byte)0; }
        public bool Equals(Voxel other) => Id == other.Id && Level == other.Level;
        public override bool Equals(object obj) => obj is Voxel other && Equals(other);
        public override int GetHashCode() => ((int)Id << 8) | Level;
    }
}
