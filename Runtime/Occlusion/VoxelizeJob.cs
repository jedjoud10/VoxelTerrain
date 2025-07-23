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

        [WriteOnly]
        public NativeArray<bool> insideSurfaceVoxels;
        public float3 cameraPosition;

        public void Execute(int index) {
            int3 pos = (int3)VoxelUtils.IndexToPos(index, OcclusionUtils.SIZE);
            pos -= OcclusionUtils.SIZE / 2;
            pos += (int3)math.floor(cameraPosition);

            if (!IsVoxelSolid(pos)) {
                insideSurfaceVoxels[index] = false;
                return;
            }

            for (int dz = -1; dz <= 1; dz++) {
                for (int dy = -1; dy <= 1; dy++) {
                    for (int dx = -1; dx <= 1; dx++) {
                        if (!IsVoxelSolid(pos + new int3(dx, dy, dz))) {
                            insideSurfaceVoxels[index] = false;
                            return;
                        }
                    }
                }
            }

            insideSurfaceVoxels[index] = true;
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