using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, OptimizeFor = OptimizeFor.Performance)]
    public struct FaceVoxelsDownsampleJob : IJobParallelFor {
        // Source voxels from LOD0.
        [ReadOnly]
        public NativeArray<Voxel> lod0Voxels;

        // morton encoded. we do the slicing manually
        [NativeDisableParallelForRestriction]
        public NativeArray<Voxel> dstFace;

        public int mortonOffset;

        public void Execute(int index) {
            uint2 srcPosFlat = Morton.DecodeMorton2D_32((uint)(index)) * 2;

            // if we were reading a single voxel this is what we'd do
            uint3 srcPos = new uint3(0, srcPosFlat);

            // but in reality, we need a 2x2x2 region, so we need to offset in the direction of sampling (the negative x direction) to account for that
            //srcPos.x -= 1;

            // now we can sample it
            Voxel v = Downsample(srcPos);
            dstFace[mortonOffset + index] = v;
        }

        public Voxel Downsample(uint3 position) {
            // It seems that not blurring the data gives a smoother transition between the cells
            return lod0Voxels[VoxelUtils.PosToIndexMorton(position)];

            /*
            float negativeSum = 0;
            float positiveSum = 0;
            int negative = 0;
            int positive = 0;
            
            for (int i = 0; i < 8; i++) {
                uint3 offset = VoxelUtils.IndexToPosMorton(i);
                int index = VoxelUtils.PosToIndexMorton(offset + position);
                half d = voxels[index].density;

                if (d > 0) {
                    positive++;
                    positiveSum += d;
                } else {
                    negative++;
                    negativeSum += d;
                }
            }

            float density = (positive > negative) ? positiveSum / math.max(1, positive) : negativeSum / math.max(1, negative);

            return new Voxel {
                density = (half)density,
                material = 0,
            };
            */
        }
    }
}