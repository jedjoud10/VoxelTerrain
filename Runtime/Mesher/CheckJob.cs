using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct CheckJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<Voxel> voxels;
        [WriteOnly]
        public NativeArray<uint> bits;

        public void Execute(int index) {
            uint packed = 0;

            int count = math.min(voxels.Length - index * 32, 32);

            for (int j = 0; j < count; j++) {
                Voxel voxel = voxels[j + index * 32];
                uint bit = (voxel.density <= 0f) ? 1u : 0u;
                packed |= bit << j;
            }

            bits[index] = packed;
        }
    }
}