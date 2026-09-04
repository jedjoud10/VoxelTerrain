using Unity.Mathematics;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    public interface IEdit {
        public void Modify(float3 position, ref EditVoxel voxel);
        public MinMaxAABB GetBounds();
    }
}