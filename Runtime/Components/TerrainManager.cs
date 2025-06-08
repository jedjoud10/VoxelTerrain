using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public struct TerrainManager : IComponentData {
        public  NativeHashMap<OctreeNode, Entity> chunks;
    }
}