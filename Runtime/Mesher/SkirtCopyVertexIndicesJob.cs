using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct SkirtCopyVertexIndicesJob : IJob {
        [WriteOnly]
        public NativeArray<int> skirtVertexIndicesCopied;

        [ReadOnly]
        public NativeArray<int> sourceVertexIndices;

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
                        // The "remapped" index is simply the old index!
                        // This is because we will only use the skirtVertexIndicesCopied indices when we generate the base skirt mesh (the one that fills the gaps)
                        skirtVertexIndicesCopied[i + faceElementOffset] = srcIndex;
                        boundaryVertexCount++;
                    } else {
                        // Invalid boundary vertex, propagate invalid index (int.MaxValue)
                        skirtVertexIndicesCopied[i + faceElementOffset] = int.MaxValue;
                    }
                }
            }
        }
    }
}