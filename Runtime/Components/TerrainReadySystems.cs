using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public struct TerrainReadySystems : IComponentData {
        public bool manager;
        public bool readback;
        public bool mesher;

        public bool segmentVoxels;
        public bool segmentPropsDispatch;
        public bool segmentManager;
    }
}