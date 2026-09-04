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

    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct UnsafeAsyncFillJob : IJob {
        [ReadOnly]
        public NativeArray<byte> src;
        [WriteOnly]
        public NativeArray<byte> dst;
        public int byteSize;
        public int dstCount;
        public void Execute() {
            UnsafeUtility.MemCpyReplicate(dst.GetUnsafePtr(), src.GetUnsafeReadOnlyPtr(), byteSize, dstCount);
        }
    }

    public static class AsyncMemCpyUtils {
        public static JobHandle CopyAsync<T>(NativeArray<T> src, NativeArray<T> dst, JobHandle dep = default) where T: unmanaged {
            int sizeOf = UnsafeUtility.SizeOf<T>();
            NativeArray<byte> castedSrc = src.Reinterpret<byte>(sizeOf);
            NativeArray<byte> castedDst = dst.Reinterpret<byte>(sizeOf);

            return new AsyncMemCpyJob {
                src = castedSrc,
                dst = castedDst
            }.Schedule(dep);
        }

        public static JobHandle CopyFromAsync<T>(this NativeArray<T> self, NativeArray<T> other, JobHandle dep = default) where T : unmanaged {
            return CopyAsync<T>(other, self, dep);
        }

        public static unsafe JobHandle RawCopyAsync(void* src, void* dst, int size, JobHandle dep = default) {
            return new UnsafeAsyncMemCpyJob() {
                src = src,
                dst = dst,
                byteSize = size
            }.Schedule(dep);
        }

        public static JobHandle FillAsync<T>(this NativeArray<T> array, T value = default, JobHandle dep = default) where T : unmanaged {
            int sizeOf = UnsafeUtility.SizeOf<T>();
            NativeArray<byte> castedDst = array.Reinterpret<byte>(sizeOf);
            NativeArray<byte> srcValue = new NativeArray<byte>(sizeOf, Allocator.TempJob);
            unsafe {
                UnsafeUtility.MemCpy(srcValue.GetUnsafePtr(), &value, sizeOf);
            }

            JobHandle job = new UnsafeAsyncFillJob {
                src = srcValue,
                dst = castedDst,
                byteSize= sizeOf,
                dstCount = array.Length,
            }.Schedule(dep);
            srcValue.Dispose(job);
            return job;
        }
    }
}