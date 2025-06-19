using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct CheckJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<half> densities;
        [WriteOnly]
        public NativeArray<uint> bits;

        public void Execute(int index) {
            uint packed = 0;

            int count = math.min(VoxelUtils.VOLUME - index * 32, 32);

            for (int j = 0; j < count; j++) {
                half density = densities[j + index * 32];
                uint bit = (density >= 0f) ? 1u : 0u;
                packed |= bit << j;
            }

            bits[index] = packed;
        }
    }
}