using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct SetMeshDataJob : IJob {
        [WriteOnly]
        public Mesh.MeshData data;
        [ReadOnly]
        public NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;

        // Merged buffers that include the original mesh and the skirt meshes
        [ReadOnly]
        public NativeArray<float3> vertices;
        [ReadOnly]
        public NativeArray<float3> normals;
        [ReadOnly]
        public NativeArray<int> indices;

        // Used from the MergeMeshJob
        [ReadOnly]
        public NativeArray<int> submeshIndexOffsets;
        [ReadOnly]
        public NativeArray<int> submeshIndexCounts;
        [ReadOnly]
        public NativeReference<int> totalVertexCount;
        [ReadOnly]
        public NativeReference<int> totalIndexCount;


        public void Execute() {
            // We will store ALL the vertices (uniform + skirt)
            data.SetVertexBufferParams(totalVertexCount.Value, vertexAttributeDescriptors);
            vertices.GetSubArray(0, totalVertexCount.Value).CopyTo(data.GetVertexData<float3>(0));

            // We will store ALL the indices (uniform + skirt)
            data.SetIndexBufferParams(totalIndexCount.Value, IndexFormat.UInt32);
            indices.GetSubArray(0, totalIndexCount.Value).CopyTo(data.GetIndexData<int>());

            data.subMeshCount = 1;
            data.SetSubMesh(0, new SubMeshDescriptor {
                indexStart = submeshIndexOffsets[0],
                indexCount = submeshIndexCounts[0],
                topology = MeshTopology.Triangles,
            }, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds);
        }
    }
}