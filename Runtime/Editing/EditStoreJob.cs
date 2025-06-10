using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    [BurstCompile(CompileSynchronously = true)]
    internal struct EditStoreJob<T> : IJobParallelFor where T: struct, IEdit  {
        public int3 chunkOffset;
        public T edit;
        public NativeArray<Voxel> voxels;

        public void Execute(int index) {
            uint3 id = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);
            float3 worldPosition = (float3)((int3)id + chunkOffset);

            // Read, modify, write
            Voxel voxel = voxels[index];
            float density = voxel.density;
            edit.Modify(worldPosition, ref density);
            voxel.density = (half)density;
            voxels[index] = voxel;
        }
    }
}