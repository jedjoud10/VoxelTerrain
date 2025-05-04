using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        public UnsafePtrList<Voxel> neighbours;

        [ReadOnly]
        public BitField32 neighbourMask;

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
            uint3 position = VoxelUtils.IndexToPosMorton(index);

            if (!VoxelUtils.CheckCubicVoxelPosition((int3)position, neighbourMask))
                return;

            /*
            BitField32 value = new BitField32(0);
            for (int i = 0; i < 8; i++) {
                uint3 pos = VoxelUtils.IndexToPosMorton(i) + position;
                bool set = VoxelUtils.FetchNeighboursOnlyPositive(VoxelUtils.PosToIndexMorton(pos), ref voxels, ref neighbours).density < 0.0;
                value.SetBits(i, set);
            }
            enabled[index] = (byte)(value.Value);
            */
            

            int4 indices = math.int4(Morton.EncodeMorton32(offsets[0].c0 + position.x, offsets[0].c1 + position.y, offsets[0].c2 + position.z));
            float4 test = math.float4(0.0F);

            for (int i = 0; i < 4; i++) {
                test[i] = VoxelUtils.FetchVoxelNeighbours(indices[i], ref voxels, ref neighbours).density;
            }

            int4 indices2 = math.int4(Morton.EncodeMorton32(offsets[1].c0 + position.x, offsets[1].c1 + position.y, offsets[1].c2 + position.z));
            float4 test2 = math.float4(0.0F);

            for (int i = 0; i < 4; i++) {
                test2[i] = VoxelUtils.FetchVoxelNeighbours(indices2[i], ref voxels, ref neighbours).density;
            }

            bool4 check1 = test < math.float4(0.0);
            bool4 check2 = test2 < math.float4(0.0);

            int value = math.bitmask(check1) | (math.bitmask(check2) << 4);

            enabled[index] = (byte)value;
        }
    }
}