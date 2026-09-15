using System.Runtime.InteropServices;
using Unity.Entities;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    /// <summary>
    /// Depicts the world absolute bounds for a terrain edit
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainEditBounds : IComponentData {
        public MinMaxAABB bounds;
    }
}