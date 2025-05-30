using Unity.Entities;

namespace jedjoud.VoxelTerrain.Segments {
    public struct TerrainSegmentOwnedPropBuffer : IBufferElementData {
        public Entity entity;
    }
}