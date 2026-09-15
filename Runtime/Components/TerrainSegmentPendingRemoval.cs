using Unity.Entities;

namespace jedjoud.VoxelTerrain.Segments {
    public struct TerrainSegmentPendingRemoval : IComponentData {
        public bool propsNeedCleanup;
    }
}