using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain {
    public struct TerrainReadySystems : IComponentData {
        public bool manager;
        public bool readback;
        public bool mesher;
    }
}