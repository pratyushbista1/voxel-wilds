namespace VoxelWilds.Core
{
    public sealed class ChunkMeshSnapshot
    {
        public const int Width = World.ChunkSize + 2;
        public const int RowCount = Width * (World.Height + 2);
        private readonly Voxel[] voxels = new Voxel[Width * RowCount];
        private World world;
        private int row, originX, originZ;
        public bool Complete => row == RowCount;
        public void Begin(World source, Cell chunk)
        {
            world = source; originX = chunk.X * World.ChunkSize; originZ = chunk.Z * World.ChunkSize; row = 0;
        }
        public void CaptureRows(int count)
        {
            int end = System.Math.Min(RowCount, row + count);
            while (row < end)
            {
                int y = row / Width - 1, z = row % Width - 1, start = row * Width;
                for (int x = -1; x <= World.ChunkSize; x++) voxels[start + x + 1] = world.Get(new Cell(originX + x, y, originZ + z));
                row++;
            }
        }
        public Voxel Get(int x, int y, int z) => voxels[(y + 1) * Width * Width + (z + 1) * Width + x + 1];
        public Block BlockAt(int x, int y, int z) => Get(x, y, z).Id;
    }
}
