using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SkirtIndexReset : IJobParallelFor {
        [WriteOnly]
        public NativeArray<int> skirtVertexIndices;
        public void Execute(int index) {
            skirtVertexIndices[index] = int.MaxValue;
        }
    }
}