using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public struct TerrainManager : IComponentData {
        public NativeHashMap<OctreeNode, Entity> chunks;
    }
}