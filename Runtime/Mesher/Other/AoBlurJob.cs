using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct AoBlurJob : IJobParallelFor {
        [ReadOnly]
        public UnsafePtrList<half> densityDataPtrs;
        [ReadOnly]
        public BitField32 neighbourMask;
        [WriteOnly]
        public NativeArray<half> dstData;
        
        public void Execute(int index) {
            uint3 position = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);

            int count = 0;

            // 3x3x3 blur
            for (int i = 0; i < 27; i++) {
                int3 pos = (int3)position + (int3)VoxelUtils.IndexToPos(i, 3) - 1;
                if (VoxelUtils.CheckPositionInsideMultipleChunks(pos, neighbourMask)) {
                    half density = VoxelUtils.FetchDensityNeighbours(pos, ref densityDataPtrs);
                    if (density > 0.0) {
                        count++;
                    }
                }
            }

            //dstData[index] = (float)count / 27.0f;
        }
    }
}