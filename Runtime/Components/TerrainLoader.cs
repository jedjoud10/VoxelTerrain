using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public struct TerrainLoader : IComponentData {
        public float3 position;
        public float octreeNodeFactor;
        public int3 segmentExtent;
        public int3 segmentExtentHigh;
    }
}