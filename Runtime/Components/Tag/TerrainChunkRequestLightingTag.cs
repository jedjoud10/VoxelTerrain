using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// States that a chunk should have its lighting recalculated based on its neighbouring data
    /// </summary>
    public struct TerrainChunkRequestLightingTag : IComponentData, IEnableableComponent {
    }
}