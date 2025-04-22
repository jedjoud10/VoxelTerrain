using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    // Corner job that will store the locations of completely empty / filled cells in the mesh to speed up meshing
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

        public NativeParallelHashSet<ushort>.ParallelWriter materialHashSet;
        public NativeParallelHashMap<ushort, int>.ParallelWriter materialHashMap;
        public Unsafe.NativeCounter.Concurrent materialCounter;

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
            uint3 position = VoxelUtils.IndexToPos(index, VoxelUtils.Size + 1);

            int4 indices = math.int4(Morton.EncodeMorton32(offsets[0].c0 + position.x, offsets[0].c1 + position.y, offsets[0].c2 + position.z));
            float4 test = math.float4(0.0F);

            for (int i = 0; i < 4; i++) {
                test[i] = VoxelUtils.FetchWithNeighbours(indices[i], ref voxels, ref neighbours).density;
            }

            int4 indices2 = math.int4(Morton.EncodeMorton32(offsets[1].c0 + position.x, offsets[1].c1 + position.y, offsets[1].c2 + position.z));
            float4 test2 = math.float4(0.0F);

            for (int i = 0; i < 4; i++) {
                test2[i] = VoxelUtils.FetchWithNeighbours(indices2[i], ref voxels, ref neighbours).density;
            }

            bool4 check1 = test < math.float4(0.0);
            bool4 check2 = test2 < math.float4(0.0);

            int value = math.bitmask(check1) | (math.bitmask(check2) << 4);

            enabled[index] = (byte)value;

            // We moved the material shenanigan stuff here instead...
            Voxel voxel = VoxelUtils.FetchWithNeighbours(VoxelUtils.PosToIndexMorton(position), ref voxels, ref neighbours);
            if (value != 0 && value != 255 && voxel.density < 0.0) {
                if (materialHashSet.Add(voxel.material)) {
                    materialHashMap.TryAdd(voxel.material, materialCounter.Increment());
                }
            }
        }
    }
}