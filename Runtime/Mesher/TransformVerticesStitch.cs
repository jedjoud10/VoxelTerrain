using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public unsafe struct TransformVerticesStitch : IJobParallelFor {
        public NativeSlice<float3> localVertices;
        public float3 globalOffset;
        public float globalScale;

        public void Execute(int index) {
            float3 temp = localVertices[index];
            temp *= globalScale;
            temp += globalOffset;
            localVertices[index] = temp;
        }
    }
}