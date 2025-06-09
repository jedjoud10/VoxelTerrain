using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


namespace jedjoud.VoxelTerrain.Edits {
    [BurstCompile(CompileSynchronously = true)]
    public struct CreateEditChunksFromBoundsJob : IJob {
        [ReadOnly]
        public NativeArray<Unity.Mathematics.Geometry.MinMaxAABB> boundsArray;

        public NativeHashMap<int3, int> chunkPositionsToChunkEditIndices;
        public NativeHashSet<int3> modifiedChunkEditPositions;
        public NativeList<int3> addedChunkEditPositions;

        public void Execute() {
            int count = chunkPositionsToChunkEditIndices.Count;

            foreach (var bounds in boundsArray) {
                int3 min = (int3)math.floor(bounds.Min / (float)VoxelUtils.PHYSICAL_CHUNK_SIZE);
                int3 max = (int3)math.floor(bounds.Max / (float)VoxelUtils.PHYSICAL_CHUNK_SIZE);

                for (int z = min.z; z <= max.z; z++) {
                    for (int y = min.y; y <= max.y; y++) {
                        for (int x = min.x; x <= max.x; x++) {
                            int3 chunkPos = new int3(x, y, z);

                            if (!chunkPositionsToChunkEditIndices.ContainsKey(chunkPos)) {
                                chunkPositionsToChunkEditIndices.Add(chunkPos, count);
                                addedChunkEditPositions.Add(chunkPos);
                                count++;
                            }

                            modifiedChunkEditPositions.Add(chunkPos);
                        }
                    }
                }
            }
        }
    }
}