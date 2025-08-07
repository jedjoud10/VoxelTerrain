using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Occlusion {
    [BurstCompile(CompileSynchronously = true)]
    public struct VoxelizeJob : IJobParallelFor {
        [ReadOnly]
        public NativeHashMap<int3, int> chunkPositionsLookup;
        public UnsafePtrList<half> chunkDensityPtrs;

        public float3 cameraPosition;

        [WriteOnly]
        public NativeArray<uint> preRelaxationBits;

        public int volume;
        public int size;

        public void Execute(int batchIndex) {
            uint packed = 0;
            int count = math.min(volume - batchIndex * 32, 32);

            for (int j = 0; j < count; j++) {
                int index = j + batchIndex * 32;
                int3 pos = (int3)VoxelUtils.IndexToPos(index, size);
                pos -= size / 2;
                pos += (int3)math.floor(cameraPosition);
                bool solid = IsVoxelSolid(pos);
                uint bit = solid ? 1u : 0u;
                packed |= bit << j;
            }

            preRelaxationBits[batchIndex] = packed;
        }

        private bool IsVoxelSolid(int3 position) {
            VoxelUtils.WorldVoxelPosToChunkSpace(position, out int3 chunkPosition, out uint3 localVoxelPos);

            if (chunkPositionsLookup.TryGetValue(chunkPosition, out int chunkIndexLookup)) {
                int voxelIndex = VoxelUtils.PosToIndex(localVoxelPos, VoxelUtils.SIZE);

                unsafe {
                    half* ptr = chunkDensityPtrs[chunkIndexLookup];
                    half density = *(ptr + voxelIndex);
                    return density < 0;
                }
            }

            return false;
        }
    }
}