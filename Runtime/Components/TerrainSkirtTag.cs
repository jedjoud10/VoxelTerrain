using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public struct TerrainSkirtTag : IComponentData {
        // Is set to byte.MaxValue for the main skirt desu
        public byte direction;
    }
}