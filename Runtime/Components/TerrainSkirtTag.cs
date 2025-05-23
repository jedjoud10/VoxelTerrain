using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public struct TerrainSkirtTag : IComponentData {
        // Is set to byte.MaxValue for the main skirt desu
        public byte direction;
    }
}