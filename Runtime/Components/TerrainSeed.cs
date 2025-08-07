using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public struct TerrainSeed : IComponentData {
        public int3 permutationSeed;
        public int3 moduloSeed;
        public int seed;
        public bool dirty;
    }
}