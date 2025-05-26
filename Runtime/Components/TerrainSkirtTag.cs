using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public struct TerrainSkirtTag : IComponentData {
        public byte direction;
    }
}