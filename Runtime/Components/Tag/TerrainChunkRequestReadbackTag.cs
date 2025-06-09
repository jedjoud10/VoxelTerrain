using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunkRequestReadbackTag : IComponentData, IEnableableComponent {
        public bool skipMeshingIfEmpty;
    }
}