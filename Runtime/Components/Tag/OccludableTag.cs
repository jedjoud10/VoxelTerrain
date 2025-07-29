using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// States whether or not a terrain chunk or skirt is occluded by other chunks. Enabled/disabled based on occlusion culling. This is the second check after the TerrainVisibleTag
    /// </summary>
    public struct OccludableTag : IComponentData, IEnableableComponent {
    }
}