using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct NormalsPrefetchJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        [WriteOnly]
        public NativeArray<half> val;

        public void Execute(int index) {
            val[index] = voxels[index].density;
        }
    }
}