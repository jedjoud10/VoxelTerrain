using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct MergeMeshJob : IJob {
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

        // Normal mesh buffers that will ALSO store the merged mesh data
        public NativeArray<float3> vertices;
        public NativeArray<float3> normals;
        public NativeArray<int> indices;


        public void Execute() {
            // We will store ALL the vertices (uniform + skirt)
            totalVertexCount.Value = vertexCounter.Count + skirtVertexCounter.Count;

            // Merge the skirt vertices onto the main mesh vertices
            NativeArray<float3> src, dst;
            src = skirtVertices.GetSubArray(0, skirtVertexCounter.Count);
            dst = vertices.GetSubArray(vertexCounter.Count, skirtVertexCounter.Count);
            src.CopyTo(dst);

            // We will store ALL the indices (uniform + stitch + forced)
            totalIndexCount.Value = triangleCounter.Count * 3 + skirtStitchedTriangleCounter.Count * 3 + skirtForcedTriangleCounter.Sum() * 3;

            // Merge the stitched indices (the ones that we do NOT forcefully generate) in the same submesh (submesh=0)
            NativeArray<int> indexSrc, indexDst;
            indexSrc = skirtStitchedIndices.GetSubArray(0, skirtStitchedTriangleCounter.Count * 3);
            indexDst = indices.GetSubArray(triangleCounter.Count * 3, skirtStitchedTriangleCounter.Count * 3);
            indexSrc.CopyTo(indexDst);

            // Write submesh data for the base submesh
            submeshIndexOffsets[0] = 0;
            submeshIndexCounts[0] = triangleCounter.Count * 3 + skirtStitchedTriangleCounter.Count * 3;

            // Merge the forced indices (the ones that we forcefully generated) in different submeshes
            int contiguousIndexOffset = triangleCounter.Count * 3 + skirtStitchedTriangleCounter.Count * 3;
            for (int face = 0; face < 6; face++) {
                int perFaceIndexCount = skirtForcedTriangleCounter[face] * 3;

                // Copy the scattered forced skirt indices into a contiguous array
                indexSrc = skirtForcedPerFaceIndices.GetSubArray(face * VoxelUtils.SKIRT_FACE * 6, perFaceIndexCount);
                indexDst = indices.GetSubArray(contiguousIndexOffset, perFaceIndexCount);
                indexSrc.CopyTo(indexDst);

                // Write submesh data for this skirt face submesh
                submeshIndexOffsets[face + 1] = contiguousIndexOffset;
                submeshIndexCounts[face + 1] = perFaceIndexCount;


                contiguousIndexOffset += perFaceIndexCount;
            }
        }
    }
}