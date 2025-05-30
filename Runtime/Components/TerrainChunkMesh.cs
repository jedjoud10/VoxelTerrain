using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunkMesh : IComponentData, IEnableableComponent {
        public NativeArray<float3> vertices;
        public NativeArray<int> indices;
        public BatchMeshID meshId;
    }
}