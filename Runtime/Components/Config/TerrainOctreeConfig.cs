using Unity.Entities;

namespace jedjoud.VoxelTerrain.Octree {
    public struct TerrainOctreeConfig : IComponentData {
        public int maxDepth;
    }
}