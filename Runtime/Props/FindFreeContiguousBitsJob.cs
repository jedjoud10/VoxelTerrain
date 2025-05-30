using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Segments {
    [BurstCompile(CompileSynchronously = true)]
    public struct FindFreeContiguousBitsJob : IJob {
        [ReadOnly]
        public NativeBitArray permPropsInUseBitset;
        public NativeReference<int> dstOffset;

        // we need to find a sequence of non-set bits that's at least this long
        public int count;

        // src offset in permPropsInUseBitset
        public int permOffset;

        public void Execute() {
        }
    }
}