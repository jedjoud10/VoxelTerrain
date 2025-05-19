using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain {
    public struct TerrainManagerConfig : IComponentData {
        public float ditherTransitionTime;
        public int voxelSizeReduction;
    }
}