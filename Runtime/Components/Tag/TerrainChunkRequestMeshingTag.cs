using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// States that a chunk should have a mesh computed for it after it gets voxel data
    /// </summary>
    public struct TerrainChunkRequestMeshingTag : IComponentData, IEnableableComponent {
        public bool deferredVisibility;
    }
}