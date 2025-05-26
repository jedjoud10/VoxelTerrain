using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
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

        public NativeCounter skirtVertexCounter;

        public void Execute() {
            int boundaryVertexCount = 0;

            // -X, -Y, -Z, X, Y, Z
            for (int face = 0; face < 6; face++) {
                uint missing = face < 3 ? 0 : ((uint)VoxelUtils.SIZE - 3);
                int faceElementOffset = face * VoxelUtils.FACE;

                // Loop through the face in 2D and copy the vertices from the boundary in 3D
                for (int i = 0; i < VoxelUtils.FACE; i++) {
                    uint2 flattened = VoxelUtils.IndexToPos2D(i, VoxelUtils.SIZE);
                    uint3 position = SkirtUtils.UnflattenFromFaceRelative(flattened, face % 3, missing);
                    int src = VoxelUtils.PosToIndex(position, VoxelUtils.SIZE);
                    int srcIndex = sourceVertexIndices[src];

                    if (srcIndex != int.MaxValue) {
                        // Valid boundary vertex, copy it
                        skirtVertices[boundaryVertexCount] = sourceVertices[srcIndex];
                        skirtNormals[boundaryVertexCount] = sourceNormals[srcIndex];
                        skirtUvs[boundaryVertexCount] = 1f;

                        // We will merge the generated skirt triangles / skirt vertices back onto the main mesh, so we need to use the original mesh's vertex indices
                        // Set the second highest bit to true for vertices that have been copied (so that we avoid copying them when we merge them to the og mesh)
                        int newIndex = srcIndex;
                        BitUtils.SetBit(ref newIndex, 30, true);
                        skirtVertexIndicesCopied[i + faceElementOffset] = newIndex;
                        boundaryVertexCount++;
                    } else {
                        // Invalid boundary vertex, propagate invalid index (int.MaxValue)
                        skirtVertices[i + faceElementOffset] = 0f;
                        skirtNormals[i + faceElementOffset] = 0f;
                        skirtUvs[i + faceElementOffset] = 0f;
                        skirtVertexIndicesCopied[i + faceElementOffset] = int.MaxValue;
                    }
                }
            }

            // The next job (which is SkirtVertexJob) will need to generate new vertices,
            // so we must update the counter so that the indices are contiguous
            skirtVertexCounter.Count = boundaryVertexCount;
        }
    }
}