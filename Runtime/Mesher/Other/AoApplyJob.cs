using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct AoApplyJob : IJobParallelForDefer {
        [ReadOnly]
        public NativeArray<float3> positions;
        [WriteOnly]
        public NativeArray<float4> colours;
        [ReadOnly]
        public NativeArray<half> dstData;
        public void Execute(int index) {
            float3 pos = positions[index];
            float ao = 1;

            colours[index] = new float4(0, 0, 0, ao);
        }
    }
}