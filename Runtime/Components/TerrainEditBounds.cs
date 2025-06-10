using System.Runtime.InteropServices;
using Unity.Entities;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainEditBounds : IComponentData {
        public MinMaxAABB bounds;
    }
}