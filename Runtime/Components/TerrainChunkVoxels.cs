using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunkVoxels : IComponentData, IEnableableComponent {
        public bool disposed;
        public NativeArray<Voxel> inner;
        public JobHandle asyncWriteJob;
        public JobHandle asyncReadJob;
    }
}