using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public struct TerrainShouldUpdate : IComponentData {
        public bool octree;
        public bool segments;
    }
}