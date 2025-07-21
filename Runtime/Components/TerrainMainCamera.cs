using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public struct TerrainMainCamera : IComponentData {
        public float4x4 projectionMatrix;
        public float4x4 worldToCamera;
    }
}