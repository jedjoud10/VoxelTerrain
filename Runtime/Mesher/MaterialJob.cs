using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading;
using System.Runtime.CompilerServices;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct MaterialJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        [ReadOnly]
        public UnsafePtrList<Voxel> neighbours;

        [ReadOnly]
        public BitField32 neighbourMask;

        // 8 uints that are used for atomic ors
        public NativeArray<uint> buckets;

        // https://github.com/dotnet/runtime/blob/1d1bf92fcf43aa6981804dc53c5174445069c9e4/src/libraries/System.Private.CoreLib/src/System/Threading/Interlocked.cs#L319C13-L320C25
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Or(ref int location1, int value) {
            int current = location1;
            while (true) {
                int newValue = current | value;
                int oldValue = Interlocked.CompareExchange(ref location1, newValue, current);
                if (oldValue == current) {
                    return oldValue;
                }
                current = oldValue;
            }
        }

        // TODO: make this faster. I still feel like 5ms is too slow for this shit
        // I most definitely caved in for the micro optimizations kek. I need to rework the whole CPU side mesher jobs...
        public unsafe void Execute(int index) {
            Voxel voxel = voxels[index];
            byte material = voxel.material;

            int bucketIndex = material / 32;
            int bitIndex = material % 32;
            uint* ptr = (uint*)buckets.GetUnsafePtr();

            Or(ref UnsafeUtility.ArrayElementAsRef<int>(ptr, bucketIndex), 1 << bitIndex);
        }
    }
}