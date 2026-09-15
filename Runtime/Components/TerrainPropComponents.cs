using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Props {
    /// <summary>
    /// Terrain prop tag for un-destroyed props
    /// </summary>
    public struct TerrainPropTag : IComponentData {
    }

    /// <summary>
    /// Contains the ID of the current prop. Type and owning segment are stored in the shared component
    /// </summary>
    public struct TerrainPropCleanup : ICleanupComponentData {
        public uint id;
    }

    /// <summary>
    /// Contains the prop type and owning segment as a shared component (since multiple prop entities will have the same values)
    /// </summary>
    public struct TerrainPropSharedCleanup : ICleanupSharedComponentData {
        public int type;
        public int3 segmentPosition;
    }
}