using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// States that the chunk is at the end of the pipeline and that it has handled all its pending requests
    /// </summary>
    public struct TerrainChunkEndOfPipeTag : IComponentData, IEnableableComponent {
    }
}