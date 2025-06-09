using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    [BurstCompile(CompileSynchronously = true)]
    internal struct EditApplyJob : IJobParallelFor {
        [ReadOnly]
        public NativeHashMap<int3, int> chunkPositionsToChunkEditIndices;

        [ReadOnly]
        public UnsafePtrList<Voxel> chunkEditsRaw;

        public NativeArray<Voxel> voxels;
        public int3 chunkOffset;
        public int chunkScale;

        public void Execute(int index) {
            uint3 id = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);

            int3 worldPosition = ((int3)id * chunkScale + chunkOffset);
            int3 chunkEditPosition = (int3)math.floor((float3)worldPosition / VoxelUtils.PHYSICAL_CHUNK_SIZE);

            if (chunkPositionsToChunkEditIndices.TryGetValue(chunkEditPosition, out int chunkEditIndex)) {
                uint3 voxelPositionInsideChunkEdit = VoxelUtils.Mod(worldPosition, VoxelUtils.PHYSICAL_CHUNK_SIZE);
                int chunkEditVoxelIndex = VoxelUtils.PosToIndex(voxelPositionInsideChunkEdit, VoxelUtils.SIZE);

                unsafe {
                    Voxel* chunkEditVoxelsPtr = chunkEditsRaw[chunkEditIndex];
                    Voxel srcEditVoxel = chunkEditVoxelsPtr[chunkEditVoxelIndex];
                    voxels[index] = srcEditVoxel;
                }
            }
        }
    }
}