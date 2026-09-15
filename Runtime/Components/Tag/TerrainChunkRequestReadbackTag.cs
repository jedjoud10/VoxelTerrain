using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// States that a chunk should readback voxel data from the GPU
    /// </summary>
    public struct TerrainChunkRequestReadbackTag : IComponentData, IEnableableComponent {
        public bool skipMeshingIfEmpty;
    }
}