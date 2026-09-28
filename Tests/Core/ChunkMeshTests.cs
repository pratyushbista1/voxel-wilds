using VoxelWilds.Core;

public static class ChunkMeshTests
{
    public static void Run()
    {
        Spec.Run("Chunk mesh snapshots preserve border, height, and fluid state", () =>
        {
            var world = new World(1453, Dimension.Overworld);
            var key = new Cell(-2, 0, 3);
            world.Set(new Cell(-33, 70, 64), Block.Water, 5);
            var snapshot = new ChunkMeshSnapshot(); snapshot.Begin(world, key);
            while (!snapshot.Complete) snapshot.CaptureRows(7);
            for (int y = -1; y <= World.Height; y++)
            for (int z = -1; z <= 16; z++)
            for (int x = -1; x <= 16; x++)
                Spec.Equal(world.Get(new Cell(key.X * 16 + x, y, key.Z * 16 + z)), snapshot.Get(x, y, z));
            Spec.Equal((byte)5, snapshot.Get(-1, 70, 16).Level);
        });
        Spec.Run("Chunk mesh snapshots do not mix edits into an already captured surface", () =>
        {
            var world = new World(1453, Dimension.Overworld);
            var snapshot = new ChunkMeshSnapshot(); snapshot.Begin(world, new Cell(0, 0, 0));
            snapshot.CaptureRows(ChunkMeshSnapshot.RowCount);
            var position = new Cell(2, 80, 3);
            world.Set(position, Block.Glowstone);
            Spec.Equal(Block.Air, snapshot.BlockAt(2, 80, 3));
            snapshot.Begin(world, new Cell(0, 0, 0)); snapshot.CaptureRows(ChunkMeshSnapshot.RowCount);
            Spec.Equal(Block.Glowstone, snapshot.BlockAt(2, 80, 3));
        });
        Spec.Run("End chunk snapshots leave the void boundary empty", () =>
        {
            var snapshot = new ChunkMeshSnapshot(); snapshot.Begin(new World(72, Dimension.End), new Cell(0, 0, 0));
            snapshot.CaptureRows(ChunkMeshSnapshot.RowCount);
            Spec.Equal(Block.Air, snapshot.BlockAt(0, -1, 0));
            Spec.Equal(Block.Air, snapshot.BlockAt(16, World.Height, 16));
        });
    }
}
