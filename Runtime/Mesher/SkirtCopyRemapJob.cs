using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SkirtCopyRemapJob : IJob {
        [WriteOnly]
        public NativeArray<int> skirtVertexIndicesCopied;

        [WriteOnly]
        public NativeArray<float3> skirtVertices;
        [WriteOnly]
        public NativeArray<float3> skirtNormals;
        [WriteOnly]
        public NativeArray<float2> skirtUvs;

        [ReadOnly]
        public NativeArray<int> sourceVertexIndices;

        [ReadOnly]
        public NativeArray<float3> sourceVertices;
        [ReadOnly]
        public NativeArray<float3> sourceNormals;

        public NativeMultiCounter skirtVertexCounter;

        public void Execute() {
            // -X, -Y, -Z, X, Y, Z
            for (int face = 0; face < 6; face++) {
                // Skirt faces are all separate, so their vertex indices aren't sequential
                int boundaryVertexCount = 0;

                uint missing = face < 3 ? 0 : ((uint)VoxelUtils.SIZE - 3);
                
                int dstVertexFaceOffset = VoxelUtils.SKIRT_FACE * face;


                int dstIndexFaceOffset = face * VoxelUtils.FACE;


                // Loop through the face in 2D and copy the vertices from the boundary in 3D
                for (int i = 0; i < VoxelUtils.FACE; i++) {
                    uint2 flattened = VoxelUtils.IndexToPos2D(i, VoxelUtils.SIZE);
                    uint3 position = SkirtUtils.UnflattenFromFaceRelative(flattened, face % 3, missing);
                    int src = VoxelUtils.PosToIndex(position, VoxelUtils.SIZE);
                    int srcIndex = sourceVertexIndices[src];

                    if (srcIndex != int.MaxValue) {
                        // Valid boundary vertex, copy it
                        skirtVertices[dstVertexFaceOffset + boundaryVertexCount] = sourceVertices[srcIndex];
                        skirtNormals[dstVertexFaceOffset + boundaryVertexCount] = sourceNormals[srcIndex];
                        skirtUvs[dstVertexFaceOffset + boundaryVertexCount] = 1f;
                        skirtVertexIndicesCopied[i + dstIndexFaceOffset] = boundaryVertexCount;
                        boundaryVertexCount++;
                    } else {
                        // Invalid boundary vertex, propagate invalid index (int.MaxValue)
                        skirtVertices[i + dstVertexFaceOffset] = 0f;
                        skirtNormals[i + dstVertexFaceOffset] = 0f;
                        skirtUvs[i + dstVertexFaceOffset] = 0f;
                        skirtVertexIndicesCopied[i + dstIndexFaceOffset] = int.MaxValue;
                    }
                }

                // The next job (which is SkirtVertexJob) will need to generate new vertices,
                // so we must update the counter so that the indices are contiguous
                skirtVertexCounter[face] = boundaryVertexCount;
            }            
        }
    }
}