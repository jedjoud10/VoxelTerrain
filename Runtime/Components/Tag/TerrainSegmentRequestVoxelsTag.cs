using Unity.Entities;

namespace jedjoud.VoxelTerrain.Segments {
    /// <summary>
    /// States that a terrain segment should have its voxel values filled
    /// </summary>
    public struct TerrainSegmentRequestVoxelsTag : IComponentData, IEnableableComponent {
    }
}