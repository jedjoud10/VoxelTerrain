using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {

    [BurstCompile]
    public struct SetSkirtMeshDataJob : IJob {
        [WriteOnly]
        public Mesh.MeshData data;

        [ReadOnly]
        public NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;



        public void Execute() {
            /*
            int maxSkirtVerticesCnt = skirtVertexCounter.Count;
            data.SetVertexBufferParams(maxSkirtVerticesCnt, vertexAttributeDescriptors);

            skirtVertices.GetSubArray(0, maxSkirtVerticesCnt).CopyTo(data.GetVertexData<float3>(0));
            skirtNormals.GetSubArray(0, maxSkirtVerticesCnt).CopyTo(data.GetVertexData<float3>(1));
            skirtUvs.GetSubArray(0, maxSkirtVerticesCnt).CopyTo(data.GetVertexData<float2>(2));

            NativeArray<int> indexStarts = new NativeArray<int>(6, Allocator.Temp);
            NativeArray<int> indexCounts = new NativeArray<int>(6, Allocator.Temp);


            int baseSkirtIndexCount = skirtStitchedTriangleCounter.Count * 3;
            int totalIndices = baseSkirtIndexCount;

            for (int i = 0; i < 6; i++) {
                int cnt = skirtForcedTriangleCounter[i] * 3;
                indexStarts[i] = totalIndices;
                indexCounts[i] = cnt;
                totalIndices += cnt;
            }

            data.SetIndexBufferParams(totalIndices, IndexFormat.UInt32);

            // Copy the indices for the stitched mesh (submesh=0). Always will be enabled and visible
            NativeArray<int> indexData = data.GetIndexData<int>();
            NativeArray<int> dst = indexData.GetSubArray(0, baseSkirtIndexCount);
            NativeArray<int> src = skirtStitchedIndices.GetSubArray(0, baseSkirtIndexCount);
            src.CopyTo(dst);

            // Copy the triangles for each face
            for (int i = 0; i < 6; i++) {
                dst = indexData.GetSubArray(indexStarts[i], indexCounts[i]);
                src = skirtForcedPerFaceIndices.GetSubArray(VoxelUtils.SKIRT_FACE * i * 6, indexCounts[i]);
                src.CopyTo(dst);
            }

            // Set the main skirt submesh 
            data.subMeshCount = 7;
            data.SetSubMesh(0, new SubMeshDescriptor {
                indexStart = 0,
                indexCount = baseSkirtIndexCount,
                topology = MeshTopology.Triangles,
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            // Set the submeshes desu
            for (int i = 0; i < 6; i++) {
                data.SetSubMesh(i + 1, new SubMeshDescriptor {
                    indexStart = indexStarts[i],
                    indexCount = indexCounts[i],
                    topology = MeshTopology.Triangles,
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            }

            indexStarts.Dispose();
            indexCounts.Dispose();
            */
        }
    }
}