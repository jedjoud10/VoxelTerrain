using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public struct TerrainSkirtLinkedParent : IComponentData {
        public Entity chunkParent;
    }
}