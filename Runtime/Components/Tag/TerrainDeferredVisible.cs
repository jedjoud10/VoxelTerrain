using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// To render a skirt / chunk, we must first check if this component is enabled. If it isn't, then we do not render (we disabled the MaterialMeshInfo)
    /// </summary>
    public struct TerrainDeferredVisible : IComponentData, IEnableableComponent {
    }
}