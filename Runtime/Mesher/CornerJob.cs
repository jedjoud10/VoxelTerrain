using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct CornerJob : IJobParallelFor {
        [WriteOnly]
        public NativeArray<byte> enabled;

        [ReadOnly]
        public NativeArray<uint> bits;

        [ReadOnly]
        static readonly int4[] offsets = {
            new int4(
                PosToIndex(0), // none
                PosToIndex(new uint3(1, 0, 0)), // x
                PosToIndex(new uint3(0, 1, 0)), // y
                PosToIndex(new uint3(1, 1, 0)) // x y
            ),

            new int4(
                PosToIndex(new uint3(0, 0, 1)), // z
                PosToIndex(new uint3(1, 0, 1)), // x z
                PosToIndex(new uint3(0, 1, 1)), // y z
                PosToIndex(new uint3(1, 1, 1)) // x y z
            )
        };

        private static int PosToIndex(uint3 position) {
            return (int)(position.y * VoxelUtils.SIZE * VoxelUtils.SIZE + (position.z * VoxelUtils.SIZE) + position.x);
        }

        public void Execute(int index) {
            uint3 position = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);

            if (math.any(position > VoxelUtils.SIZE - 2))
                return;

            enabled[index] = (byte)(CalculateMarchingCubesCode(position, index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe int CalculateMarchingCubesCode(uint3 position, int baseIndex) {
            // https://docs.unity3d.com/Packages/com.unity.burst@1.4/manual/docs/CSharpLanguageSupport_BurstIntrinsics.html
            // I LOVE MICROOPTIMIZATIONS!!! I LOVE DOING THIS ON A WHIM WITHOUT ACTUALLY TRUSTING PROFILER DATA!!!!
            // I actually profiled this and it is actually faster. Saved 3ms on the median time. Pretty good desu
            if (X86.Avx2.IsAvx2Supported) {
                uint4 indices = (uint4)(offsets[0] + new int4(baseIndex));
                uint4 indices2 = (uint4)(offsets[1] + new int4(baseIndex));

                v256 indices_v256 = new v256(indices.x, indices.y, indices.z, indices.w, indices2.x, indices2.y, indices2.z, indices2.w);

                // divide by 32
                v256 component_v256 = X86.Avx2.mm256_srli_epi32(indices_v256, 5);

                // modulo by 32
                v256 shift_v256 = X86.Avx2.mm256_and_si256(indices_v256, new v256(31u));

                // fetch the uints using indices
                v256 uints_v256 = X86.Avx2.mm256_i32gather_epi32(bits.GetUnsafeReadOnlyPtr(), component_v256, 4);

                // "shift" and "and"
                v256 shifted_right_v256 = X86.Avx2.mm256_srlv_epi32(uints_v256, shift_v256);
                v256 anded_v256 = X86.Avx2.mm256_and_si256(shifted_right_v256, new v256(1u));

                // check if the bits are set
                uint4 sets1 = new uint4(anded_v256.UInt0, anded_v256.UInt1, anded_v256.UInt2, anded_v256.UInt3);
                uint4 sets2 = new uint4(anded_v256.UInt4, anded_v256.UInt5, anded_v256.UInt6, anded_v256.UInt7);
                return math.bitmask(sets1 == 0) | (math.bitmask(sets2 == 0) << 4);
            } else {
                bool4 test = Load4(position, 0, baseIndex);
                bool4 test2 = Load4(position, 1, baseIndex);
                return math.bitmask(test) | (math.bitmask(test2) << 4);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool4 Load4(uint3 position, int selector, int baseIndex) {
            uint4 indices = (uint4)(offsets[selector] + new int4(baseIndex));

            bool4 hits = false;

            for (int i = 0; i < 4; i++) {
                int index = (int)indices[i];

                int component = index / 32;
                int shift = index % 32;

                uint batch = bits[component];

                hits[i] = ((batch >> shift) & 1U) == 1;
            }

            return hits;
        }
    }
}