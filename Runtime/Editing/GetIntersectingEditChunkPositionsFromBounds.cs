using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


namespace jedjoud.VoxelTerrain.Edits {
    [BurstCompile(CompileSynchronously = true)]
    public struct GetIntersectingEditChunkPositionsFromBounds : IJob {
        [ReadOnly]
        public NativeArray<Unity.Mathematics.Geometry.MinMaxAABB> boundsArray;

        public NativeHashSet<int3> intersecting;

        public void Execute() {
            foreach (var bounds in boundsArray) {
                int3 min = (int3)math.floor(bounds.Min / (float)VoxelUtils.PHYSICAL_CHUNK_SIZE);
                int3 max = (int3)math.floor(bounds.Max / (float)VoxelUtils.PHYSICAL_CHUNK_SIZE);

                for (int z = min.z; z <= max.z; z++) {
                    for (int y = min.y; y <= max.y; y++) {
                        for (int x = min.x; x <= max.x; x++) {
                            int3 chunkPos = new int3(x, y, z);
                            intersecting.Add(chunkPos);
                        }
                    }
                }
            }
        }
    }
}