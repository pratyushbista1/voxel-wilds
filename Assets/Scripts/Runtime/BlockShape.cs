using UnityEngine;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public static class BlockShape
    {
        public static Bounds Bounds(Cell cell, Voxel voxel)
        {
            Vector3 minimum = new Vector3(cell.X, cell.Y, cell.Z), size = Vector3.one;
            if (voxel.Id == Block.Door)
            {
                int edge = DoorRules.Edge(voxel);
                if ((edge & 1) == 0)
                {
                    size.z = DoorRules.Thickness;
                    if (edge == 2) minimum.z += 1 - size.z;
                }
                else
                {
                    size.x = DoorRules.Thickness;
                    if (edge == 1) minimum.x += 1 - size.x;
                }
            }
            return new Bounds(minimum + size * .5f, size);
        }

        public static bool BlocksRay(Cell cell, Voxel voxel, Ray ray, float distance)
        {
            if (!Blocks.IsSolid(voxel.Id)) return false;
            return voxel.Id != Block.Door || Bounds(cell, voxel).IntersectRay(ray, out float hit) && hit <= distance;
        }
    }
}
