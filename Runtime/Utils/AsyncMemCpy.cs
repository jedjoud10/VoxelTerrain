using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace jedjoud.VoxelTerrain {
    [BurstCompile(CompileSynchronously = true)]
    public struct AsyncMemCpy : IJob {
        public NativeArray<Voxel> src;
        public NativeArray<Voxel> dst;
        public void Execute() {
            dst.CopyFrom(src);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
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