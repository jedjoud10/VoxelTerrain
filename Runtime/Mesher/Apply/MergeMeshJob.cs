using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct MergeMeshJob : IJob {
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

        // Used for the SetMeshDataJob
        public NativeArray<int> submeshIndexOffsets;
        public NativeArray<int> submeshIndexCounts;
        public NativeReference<int> totalVertexCount;
        public NativeReference<int> totalIndexCount;

        // Merged mesh buffers
        public NativeArray<float3> mergedVertices;
        public NativeArray<float3> mergedNormals;
        public NativeArray<int> mergedIndices;

        private void Copy<T>(NativeArray<T> src, NativeArray<T> dst, int dstOffset, int length) where T: unmanaged {
            NativeArray<T> tmpSrc = src.GetSubArray(0, length);
            tmpSrc.CopyTo(dst.GetSubArray(dstOffset, length));
        }

        public void Execute() {

            // We will store ALL the vertices (uniform + skirt)
            totalVertexCount.Value = vertexCounter.Count + skirtVertexCounter.Count;

            // Merge the main mesh vertices
            Copy(vertices, mergedVertices, 0, vertexCounter.Count);
            Copy(normals, mergedNormals, 0, vertexCounter.Count);

            // Merge the skirt vertices
            Copy(skirtVertices, mergedVertices, vertexCounter.Count, skirtVertexCounter.Count);
            Copy(skirtNormals, mergedNormals, vertexCounter.Count, skirtVertexCounter.Count);

            // We will store ALL the indices (uniform + stitch + forced)
            totalIndexCount.Value = triangleCounter.Count * 3 + skirtStitchedTriangleCounter.Count * 3 + skirtForcedTriangleCounter.Sum() * 3;

            // Merge indices
            Copy(indices, mergedIndices, 0, triangleCounter.Count * 3);
            Copy(skirtStitchedIndices, mergedIndices, triangleCounter.Count * 3, skirtStitchedTriangleCounter.Count * 3);

            // Write submesh data for the base submesh
            submeshIndexOffsets[0] = 0;
            submeshIndexCounts[0] = triangleCounter.Count * 3 + skirtStitchedTriangleCounter.Count * 3;

            // Merge the forced indices (the ones that we forcefully generated) in different submeshes
            int contiguousIndexOffset = triangleCounter.Count * 3 + skirtStitchedTriangleCounter.Count * 3;
            for (int face = 0; face < 6; face++) {
                int perFaceIndexCount = skirtForcedTriangleCounter[face] * 3;

                // Copy the scattered forced skirt indices into a contiguous array
                NativeArray<int> tmpSrc = skirtForcedPerFaceIndices.GetSubArray(face * VoxelUtils.SKIRT_FACE * 6, perFaceIndexCount);
                Copy(tmpSrc, mergedIndices, contiguousIndexOffset, perFaceIndexCount);

                // Write submesh data for this skirt face submesh
                submeshIndexOffsets[face + 1] = contiguousIndexOffset;
                submeshIndexCounts[face + 1] = perFaceIndexCount;


                contiguousIndexOffset += perFaceIndexCount;
            }
        }
    }
}