using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SkirtIndexReset : IJobParallelFor {
        [WriteOnly]
        public NativeArray<int> indices;
        public void Execute(int index) {
            indices[index] = int.MaxValue;
        }
    }
}