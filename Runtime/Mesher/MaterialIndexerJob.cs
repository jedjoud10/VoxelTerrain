using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct MaterialIndexerJob : IJob {
        public NativeArray<uint> buckets;
        public NativeParallelHashMap<byte, int> materialHashMap;
        public Unsafe.NativeCounter materialCounter;

        public unsafe void Execute() {
            for (int i = 0; i < VoxelUtils.MAX_MATERIAL_COUNT; i++) {
                int bucketIndex = i / 32;
                int bitIndex = i % 32;

                uint bucket = buckets[bucketIndex];
                if (((bucket >> bitIndex) & 1) == 1) {
                    int cnt = materialCounter.Count;
                    materialCounter.Increment();
                    materialHashMap.Add((byte)i, cnt);  
                }
            }
        }
    }
}