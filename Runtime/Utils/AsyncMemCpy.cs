using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain {

    [BurstCompile(CompileSynchronously = true)]
    public struct AsyncMemCpyJob : IJob {
        [ReadOnly]
        public NativeArray<byte> src;
        [WriteOnly]
        public NativeArray<byte> dst;
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
            int sizeOf = UnsafeUtility.SizeOf<T>();
            NativeArray<byte> castedSrc = src.Reinterpret<byte>(sizeOf);
            NativeArray<byte> castedDst = dst.Reinterpret<byte>(sizeOf);

            return new AsyncMemCpyJob {
                src = castedSrc,
                dst = castedDst
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