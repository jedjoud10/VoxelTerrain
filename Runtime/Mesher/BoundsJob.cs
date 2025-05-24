using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct BoundsJob: IJob {
        [ReadOnly]
        public NativeArray<float3> vertices;
        [ReadOnly]
        public NativeCounter vertexCounter;
        [WriteOnly]
        public NativeArray<float3> bounds;

        public void Execute() {
            float3 min = 100000;
            float3 max = -10000;

            for (int i = 0; i < vertexCounter.Count; i++) {
                min = math.min(min, vertices[i]);
                max = math.max(max, vertices[i]);
            }

            bounds[0] = min;
            bounds[1] = max;
        }
    }
}