using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain {

    [BurstCompile(CompileSynchronously = true)]
    public struct AsyncMemCpyJob<T> : IJob where T : unmanaged {
        [ReadOnly]
        public NativeArray<T> src;
        [WriteOnly]
        public NativeArray<T> dst;
        public void Execute() {
            dst.CopyFrom(src);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct UnsafeAsyncMemCpyJob : IJob {
        [NativeDisableUnsafePtrRestriction]
        public void* src;
        [NativeDisableUnsafePtrRestriction]
        public void* dst;
        public int byteSize;
        public void Execute() {
            UnsafeUtility.MemCpy(dst, src, byteSize);
        }
    }

    public static class AsyncMemCpyUtils {
        public static JobHandle Copy<T>(NativeArray<T> src, NativeArray<T> dst, JobHandle dep = default) where T: unmanaged {
            return new AsyncMemCpyJob<T> {
                src = src,
                dst = dst
            }.Schedule(dep);
        }

        public static unsafe JobHandle RawCopy(void* src, void* dst, int size, JobHandle dep = default) {
            return new UnsafeAsyncMemCpyJob() {
                src = src,
                dst = dst,
                byteSize = size
            }.Schedule(dep);
        }
    }
}