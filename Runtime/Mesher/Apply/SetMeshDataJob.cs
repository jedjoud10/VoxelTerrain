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
        public NativeArray<float3> mergedVertices;
        [ReadOnly]
        public NativeArray<float3> mergedNormals;
        [ReadOnly]
        public NativeArray<int> mergedIndices;

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
            mergedVertices.GetSubArray(0, totalVertexCount.Value).CopyTo(data.GetVertexData<float3>(0));
            mergedNormals.GetSubArray(0, totalVertexCount.Value).CopyTo(data.GetVertexData<float3>(1));

            // We will store ALL the indices (uniform + skirt)
            data.SetIndexBufferParams(totalIndexCount.Value, IndexFormat.UInt32);
            mergedIndices.GetSubArray(0, totalIndexCount.Value).CopyTo(data.GetIndexData<int>());


            // 1 submesh for the main mesh + 6 submeshes per skirt face
            data.subMeshCount = 7;

            // Set each of the submeshes
            for (int i = 0; i < 7; i++) {
                data.SetSubMesh(i, new SubMeshDescriptor {
                    indexStart = submeshIndexOffsets[i],
                    indexCount = submeshIndexCounts[i],
                    topology = MeshTopology.Triangles,
                });
            }
        }
    }
}