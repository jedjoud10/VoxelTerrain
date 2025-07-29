using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// States that the chunk is at the end of the pipeline and is idling
    /// </summary>
    public struct TerrainChunkEndOfPipeTag : IComponentData, IEnableableComponent {
    }
}