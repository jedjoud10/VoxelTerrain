using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// States that the terrain segment is at the end of the pipeline and is idling
    /// </summary>
    public struct TerrainSegmentEndOfPipeTag : IComponentData, IEnableableComponent {
    }
}