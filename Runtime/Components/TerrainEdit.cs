using Unity.Entities;

namespace jedjoud.VoxelTerrain.Edits {
    public struct TerrainEdit : IComponentData {
        public TypeIndex type;
    }
}