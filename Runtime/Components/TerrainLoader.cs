using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public struct TerrainLoader : IComponentData {
        public float octreeNodeFactor;
        public int3 segmentExtent;
        public float segmentLodFactor;
    }
}