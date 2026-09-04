using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// States that a chunk has voxel data ready for use (reading & writing)
    /// </summary>
    public struct TerrainChunkVoxelsReadyTag : IComponentData, IEnableableComponent {
    }
}