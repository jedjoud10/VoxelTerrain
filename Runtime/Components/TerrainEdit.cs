using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    public struct TerrainEdit : IComponentData {
        public float3 center;
        //public float radius;
    }
}