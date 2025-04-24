using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain {
    [BurstCompile(CompileSynchronously = true, Debug = true)]
    public struct AsyncMemCpy : IJob {
        [ReadOnly]
        public NativeArray<Voxel> src;
        [WriteOnly]
        public NativeArray<Voxel> dst;
        public void Execute() {
            dst.CopyFrom(src);
        }
    }

    [BurstCompile(CompileSynchronously = true, Debug = true)]
    public unsafe struct UnsafeAsyncMemCpy : IJob {
        [NativeDisableUnsafePtrRestriction]
        public uint* src;
        [NativeDisableUnsafePtrRestriction]
        public uint* dst;
        public void Execute() {
            UnsafeUtility.MemCpy(dst, src, VoxelUtils.VOLUME * Voxel.size);
        }
    }
}