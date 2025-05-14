using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, OptimizeFor = OptimizeFor.Performance)]
    public struct CornerJob : IJobParallelFor {
        // List of enabled corners like in MC
        [WriteOnly]
        public NativeArray<byte> enabled;

        // Voxel native array
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        [ReadOnly]
        static readonly uint4x3[] offsets = {
            new uint4x3(
                new uint4(0, 1, 0, 1),
                new uint4(0, 0, 1, 1),
                new uint4(0, 0, 0, 0)
            ),

            new uint4x3(
                new uint4(0, 1, 0, 1),
                new uint4(0, 0, 1, 1),
                new uint4(1, 1, 1, 1)
            )
        };

        public void Execute(int index) {
            uint3 position = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);

            if (math.any(position > VoxelUtils.SIZE-2))
                return;

            half4 test = Load4(position, 0);
            half4 test2 = Load4(position, 1);

            bool4 check1 = test <= math.float4(0.0);
            bool4 check2 = test2 <= math.float4(0.0);

            int value = math.bitmask(check1) | (math.bitmask(check2) << 4);

            enabled[index] = (byte)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private half4 Load4(uint3 position, int index) {
            uint4 x = offsets[index].c0 + position.x;
            uint4 y = offsets[index].c1 + position.y;
            uint4 z = offsets[index].c2 + position.z;

            half4 test = math.half4(0.0F);
            for (int i = 0; i < 4; i++) {
                int newIndex = VoxelUtils.PosToIndex(new uint3(x[i], y[i], z[i]), VoxelUtils.SIZE);
                test[i] = voxels[newIndex].density;
            }

            return test;
        }

        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private half4 Load4(uint3 position, int index) {
            int4 indices = math.int4(Morton.EncodeMorton32(offsets[index].c0 + position.x, offsets[index].c1 + position.y, offsets[index].c2 + position.z));
            half4 test = math.half4(0.0F);

            if (X86.Avx2.IsAvx2Supported) {

                // I LOVE MICROOPTIMIZATIONS!!! I LOVE DOING THIS ON A WHIM WITHOUT ACTUALLY TRUSTING PROFILER DATA!!!!
                unsafe {
                    void* baseAddr = voxels.GetUnsafeReadOnlyPtr();
                    v128 indices_v128 = new v128(indices.x, indices.y, indices.z, indices.w);
                    v128 voxels_v128 = X86.Avx2.i32gather_epi32(baseAddr, indices_v128, 4);

                    // deep-seeked fucking kekek
                    v128 shuffleMask = new v128(
                        0x00, 0x01, 0x04, 0x05,  // Bytes 0-1 (0x3C00) and 4-5 (0x4000)
                        0x08, 0x09, 0x0C, 0x0D,  // Bytes 8-9 (0x4200) and 12-13 (0x4400)
                        0x80, 0x80, 0x80, 0x80,  // Zero out upper 64 bits
                        0x80, 0x80, 0x80, 0x80
                    );

                    v128 packedHalfs = X86.Ssse3.shuffle_epi8(voxels_v128, shuffleMask);
                    return *(half4*)&packedHalfs;
                }
            } else {
                for (int i = 0; i < 4; i++) {
                    test[i] = voxels[indices[i]].density;
                }
            }

            return test;
        }
        */
    }
}