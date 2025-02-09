using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace jedjoud.VoxelTerrain.Meshing {
    // Sum job that will add the offset of each material onto the last one to have a sequential native array
    [BurstCompile(CompileSynchronously = true)]
    public struct SumJob : IJobParallelFor {
        // Offsets for each material type 
        [WriteOnly]
        public NativeArray<int> materialSegmentOffsets;

        // Multiple counters for each material type
        [ReadOnly]
        public Unsafe.NativeMultiCounter countersQuad;

        // Global material counter
        [ReadOnly]
        public Unsafe.NativeCounter materialCounter;

        public void Execute(int index) {
            if (index > materialCounter.Count)
                return;


            int sum = 0;

            for (int i = 0; i < index; i++) {
                sum += countersQuad[i];
            }

            materialSegmentOffsets[index] = sum * 6;
        }
    }
}