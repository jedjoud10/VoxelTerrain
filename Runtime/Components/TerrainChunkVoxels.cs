using Unity.Collections;
using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunkVoxels : IComponentData, IEnableableComponent {
        public NativeArray<Voxel> inner;
    }
}