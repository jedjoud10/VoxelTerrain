using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain.Octree {
    public struct TerrainOctreeConfig : IComponentData {
        public int maxDepth;
    }
}