using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public struct TerrainManagerConfig : IComponentData {
        public float ditherTransitionTime;
        public int voxelSizeReduction;
    }
}