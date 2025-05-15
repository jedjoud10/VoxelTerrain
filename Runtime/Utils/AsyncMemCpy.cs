using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain {
    [BurstCompile(CompileSynchronously = true, Debug = true)]
    public struct AsyncMemCpy<T> : IJob where T: unmanaged {
        [ReadOnly]
        public NativeArray<T> src;
        [WriteOnly]
        public NativeArray<T> dst;
        public void Execute() {
            dst.CopyFrom(src);
        }
    }

    [BurstCompile(CompileSynchronously = true, Debug = true)]
    public unsafe struct UnsafeAsyncMemCpy : IJob {
        [NativeDisableUnsafePtrRestriction]
        public void* src;
        [NativeDisableUnsafePtrRestriction]
        public void* dst;
        public int byteSize;
        public void Execute() {
            UnsafeUtility.MemCpy(dst, src, byteSize);
        }
    }
}