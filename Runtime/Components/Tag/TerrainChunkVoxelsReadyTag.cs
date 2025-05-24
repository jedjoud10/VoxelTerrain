using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunkVoxelsReadyTag : IComponentData, IEnableableComponent {
    }
}