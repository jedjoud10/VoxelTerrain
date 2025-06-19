using Unity.Entities;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunkVoxels : IComponentData, IEnableableComponent {
        public VoxelData data;
        public JobHandle asyncWriteJob;
        public JobHandle asyncReadJob;
        public bool meshingInProgress;
    }
}