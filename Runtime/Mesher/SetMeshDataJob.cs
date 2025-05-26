using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

        // Normal mesh buffers
        [ReadOnly]
        public NativeArray<float3> vertices;
        [ReadOnly]
        public NativeArray<float3> normals;
        [ReadOnly]
        public NativeArray<int> indices;


        // Normal mesh counters
        [ReadOnly]
        public NativeCounter vertexCounter;
        [ReadOnly]
        public NativeCounter triangleCounter;





        // Skirt vertices (both stitched + forced)
        [ReadOnly]
        public NativeArray<float3> skirtVertices;
        [ReadOnly]
        public NativeArray<float3> skirtNormals;

        // Skirt indices
        [ReadOnly]
        public NativeArray<int> skirtStitchedIndices;
        [ReadOnly]
        public NativeArray<int> skirtForcedPerFaceIndices;

        // Skirt vertex counter
        [ReadOnly]
        public NativeCounter skirtVertexCounter;

        // Skirt triangle counters
        [ReadOnly]
        public NativeCounter skirtStitchedTriangleCounter;
        [ReadOnly]
        public NativeMultiCounter skirtForcedTriangleCounter;


        public void Execute() {
            // We will store ALL the vertices (uniform + skirt)
            data.SetVertexBufferParams(vertexCounter.Count + skirtVertexCounter.Count, vertexAttributeDescriptors);

            // First store the SN vertices
            NativeArray<float3> src, dst;
            src = vertices.GetSubArray(0, vertexCounter.Count);
            dst = data.GetVertexData<float3>(0).GetSubArray(0, vertexCounter.Count);
            src.CopyTo(dst);

            // Then store the skirt vertices (following the SN vertices)
            src = skirtVertices.GetSubArray(0, skirtVertexCounter.Count);
            dst = data.GetVertexData<float3>(0).GetSubArray(vertexCounter.Count, skirtVertexCounter.Count);
            src.CopyTo(dst);

            // We will store ALL the indices (uniform + skirt)
            data.SetIndexBufferParams(triangleCounter.Count * 3 + skirtStitchedTriangleCounter.Count * 3 /* + skirtForcedTriangleCounter.Sum() * 3 */, IndexFormat.UInt32);

            // First store the SN indices
            NativeArray<int> indexSrc, indexDst;
            indexSrc = indices.GetSubArray(0, triangleCounter.Count * 3);
            indexDst = data.GetIndexData<int>().GetSubArray(0, triangleCounter.Count * 3);
            indexSrc.CopyTo(indexDst);

            // Then store the stitched vertices (the ones that we do NOT forcefully generate) in the same submesh (submesh=0)
            indexSrc = skirtStitchedIndices.GetSubArray(0, skirtStitchedTriangleCounter.Count * 3);
            indexDst = data.GetIndexData<int>().GetSubArray(triangleCounter.Count * 3, skirtStitchedTriangleCounter.Count * 3);
            indexSrc.CopyTo(indexDst);

            data.subMeshCount = 1;
            data.SetSubMesh(0, new SubMeshDescriptor {
                indexStart = 0,
                indexCount = triangleCounter.Count * 3 + skirtStitchedTriangleCounter.Count * 3,
                topology = MeshTopology.Triangles,
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }
    }
}