using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunk : IComponentData {
        public OctreeNode node;
        public FixedList64Bytes<Entity> skirts;
        public byte skirtMask;
        public BitField32 neighbourMask;
    }
}