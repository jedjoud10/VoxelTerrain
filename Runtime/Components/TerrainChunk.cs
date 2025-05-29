using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunk : IComponentData {
        public OctreeNode node;
        public FixedList64Bytes<Entity> skirts;
        public bool generateCollisions;
        public byte skirtMask;
        public BitField32 neighbourMask;
    }
}