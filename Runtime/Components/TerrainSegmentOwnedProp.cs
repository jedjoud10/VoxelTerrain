using Unity.Entities;

namespace jedjoud.VoxelTerrain.Segments {
    public struct TerrainSegmentOwnedProp : IBufferElementData {
        public Entity entity;
    }
}