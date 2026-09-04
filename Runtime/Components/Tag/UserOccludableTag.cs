using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// States that the entity should be culled using the terrain occlusion culling. Could be added to props...
    /// </summary>
    public struct UserOccludableTag : IComponentData {
    }
}