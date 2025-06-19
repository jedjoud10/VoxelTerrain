using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain.Octree {
    public struct TerrainOctree : IComponentData {
        public bool continuous;
        public bool pending;
        public bool readyToSpawn;
        public bool shouldUpdate;

        public NativeList<OctreeNode> added;
        public NativeList<OctreeNode> removed;
        public NativeList<BitField32> neighbourMasks;

        public NativeList<OctreeNode> nodes;

        public JobHandle handle;
    }
}