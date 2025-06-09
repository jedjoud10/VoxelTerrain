using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunkRequestMeshingTag : IComponentData, IEnableableComponent {
        public bool deferredVisibility;
    }
}