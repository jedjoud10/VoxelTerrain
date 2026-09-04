using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// States that the chunk is requesting to have a mesh collider built after it gets a proper mesh
    /// </summary>
    public struct TerrainChunkRequestCollisionTag : IComponentData, IEnableableComponent {
    }
}