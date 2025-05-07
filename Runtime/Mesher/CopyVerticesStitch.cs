using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public unsafe struct CopyVerticesStitch : IJob {
        public NativeArray<int> indexOffsets;
        public NativeArray<int> vertexCounts;
        public UnsafePtrList<float3> neighbourVertices;
        public NativeArray<float3> vertices;
        
        public NativeArray<float3> boundaryVertices;
        public int boundaryVerticesCount;

        public void Execute() {
            // copy boundary vertices THEN padding vertices
            vertices.Slice(0, boundaryVerticesCount).CopyFrom(boundaryVertices.Slice(0, boundaryVerticesCount));

            for (int i = 0; i < 19; i++) {
                int count = vertexCounts[i];
                int offset = indexOffsets[i];
                if (offset != -1 && count != -1 && count != 0) {
                    NativeSlice<float3> dst = vertices.Slice(offset, count);
                    float3* src = neighbourVertices[i];
                    UnsafeUtility.MemCpy(dst.GetUnsafePtr<float3>(), src, sizeof(float3) * count);
                }
            }
        }
    }
}