using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, OptimizeFor = OptimizeFor.Performance)]
    public struct SampleVoxelsLodJob : IJobParallelFor {
        [WriteOnly]
        public NativeArray<Voxel> paddingVoxels;

        [ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public VoxelStitch.GenericBoundaryData<Voxel> jobData;
        
        public void Execute(int index) {
            uint3 position = StitchUtils.BoundaryIndexToPos(index, 65);

            paddingVoxels[index] = StitchUtils.Sample(position, ref jobData);
        }
    }
}