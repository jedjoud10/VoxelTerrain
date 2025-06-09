using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    [BurstCompile(CompileSynchronously = true)]
    internal struct EditStoreJob : IJobParallelFor {
        public float3 center;

        public int3 chunkOffset;
        public NativeArray<Voxel> voxels;

        public void Execute(int index) {
            uint3 id = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);
            float3 worldPosition = (float3)((int3)id + chunkOffset);

            // Read, modify, write
            Voxel voxel = voxels[index];
            float density = voxel.density;

            float sphere = math.length(worldPosition - center) - 5;

            if (sphere < 0) {
                density = -sphere;
            }

            //density = -math.max(density, -sphere);


            voxel.density = (half)(density);
            voxels[index] = voxel;
        }
    }
}