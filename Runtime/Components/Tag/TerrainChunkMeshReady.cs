using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunkMeshReady : IComponentData, IEnableableComponent {
        public NativeArray<float3> vertices;
        public NativeArray<int> indices;
    }
}