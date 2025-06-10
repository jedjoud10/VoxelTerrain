using Unity.Entities;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    public struct TerrainEditBounds : IComponentData {
        public MinMaxAABB bounds;
    }
}