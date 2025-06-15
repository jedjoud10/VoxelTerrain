using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    [BurstCompile(CompileSynchronously = true)]
    internal struct EditStoreJob<T> : IJobParallelFor where T: struct, IEdit  {
        public int3 chunkOffset;
        public T edit;
        public VoxelData voxels;

        public void Execute(int index) {
            uint3 id = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);
            float3 worldPosition = (float3)((int3)id + chunkOffset);

            // Read, modify, write
            EditVoxel voxel = voxels.FetchEditVoxel(index);
            edit.Modify(worldPosition, ref voxel);
            voxels.StoreEditVoxels(index, voxel);
        }
    }
}