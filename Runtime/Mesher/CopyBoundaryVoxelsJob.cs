using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct CopyBoundaryVoxelsJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        // Voxels at the (x=63 || y=63 || z=63) boundary OR the (x=0 || y=0 || z=0) boundary
        [WriteOnly]
        public NativeArray<Voxel> boundaryVoxels;

        // Whether we copy the data for the positive or negative boundary
        public bool negative;

        public void Execute(int index) {
            /*
            int morton = VoxelUtils.PosToIndexMorton(StitchUtils.BoundaryIndexToPos(index, 64, negative));
            boundaryVoxels[index] = voxels[morton];
            */
        }
    }
} 