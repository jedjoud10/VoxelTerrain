using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// Data of the main camera that we are currently rendering with.
    /// </summary>
    public struct TerrainMainCamera : IComponentData {
        public float4x4 projectionMatrix;
        public float4x4 worldToCameraMatrix;
        public float2 nearFarPlanes;
    }
}