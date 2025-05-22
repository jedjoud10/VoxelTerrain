using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, Debug = false)]
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

            if (math.any(position > VoxelUtils.SIZE-2))
                return;

            bool4 test = Load4(position, 0, index);
            bool4 test2 = Load4(position, 1, index);

            int value = math.bitmask(test) | (math.bitmask(test2) << 4);

            enabled[index] = (byte)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool4 Load4(uint3 position, int selector, int baseIndex) {
            uint4 indices = (uint4)(offsets[selector] + new int4(baseIndex));

            if (X86.Sse2.IsSse2Supported && X86.Avx2.IsAvx2Supported) {
                unsafe {
                    return DoItTheSimdWay(indices, bits.GetUnsafeReadOnlyPtr());
                }
            } else {
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

        // I LOVE MICROOPTIMIZATIONS!!! I LOVE DOING THIS ON A WHIM WITHOUT ACTUALLY TRUSTING PROFILER DATA!!!!
        // I actually profiled this and it is actually faster. Saved 6ms on the median time. Pretty good desu
        public static unsafe bool4 DoItTheSimdWay(uint4 indices, void* baseAddr) {
            unsafe {
                v128 indices_v128 = new v128(indices.x, indices.y, indices.z, indices.w);
                
                // divide by 32
                v128 component_v128 = X86.Sse2.srli_epi32(indices_v128, 5);
                
                // modulo by 32
                v128 shift_v128 = X86.Sse2.and_si128(indices_v128, new v128(31u));
                
                // fetch the uints using indices
                v128 uints_v128 = X86.Avx2.i32gather_epi32(baseAddr, component_v128, 4);
                
                // shift and and
                v128 shifted_right_v128 = X86.Avx2.srlv_epi32(uints_v128, shift_v128);
                
                v128 anded_v128 = X86.Sse2.and_si128(shifted_right_v128, new v128(1u));

                // check if the bits are set
                uint4 sets = new uint4(anded_v128.UInt0, anded_v128.UInt1, anded_v128.UInt2, anded_v128.UInt3);
                return sets == 1;
            }
        }
    }
}