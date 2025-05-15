using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace jedjoud.VoxelTerrain.Meshing {
    // Sum job that will add the offset of each material onto the last one to have a sequential native array
    [BurstCompile(CompileSynchronously = true)]
    public struct SumJob : IJob {
        [WriteOnly]
        public NativeArray<int> materialSegmentOffsets;

        [ReadOnly]
        public NativeMultiCounter countersQuad;

        [ReadOnly]
        public NativeCounter materialCounter;

        public void Execute() {
            for (int index = 0; index < materialCounter.Count; index++) {
                int sum = 0;

                for (int i = 0; i < index; i++) {
                    sum += countersQuad[i];
                }

                materialSegmentOffsets[index] = sum * 6;
            }
        }
    }
}