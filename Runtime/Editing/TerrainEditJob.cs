using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    [BurstCompile(CompileSynchronously = true)]
    internal struct TerrainEditJob : IJobParallelFor {
        public float3 offset;
        public NativeArray<Voxel> voxels;

        public void Execute(int index) {
            uint3 id = VoxelUtils.IndexToPos(index, 66);
            float3 position = (math.float3(id));
            position += offset;

            // Read, modify, write
            Voxel voxel = voxels[index];
            voxel.density = (half)((float)voxel.density + 10f);
            //Voxel newVoxel = edit.Modify(position, oldVoxel);
            voxels[index] = voxel;
        }
    }
}