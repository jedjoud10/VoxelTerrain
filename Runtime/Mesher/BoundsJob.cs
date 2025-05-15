using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct BoundsJob: IJob {
        [ReadOnly]
        public NativeArray<float3> vertices;
        [ReadOnly]
        public NativeCounter counter;
        public NativeArray<float3> bounds;

        public void Execute() {
            for (int i = 0; i < counter.Count; i++) {
                bounds[0] = math.min(bounds[0], vertices[i]);
                bounds[1] = math.max(bounds[1], vertices[i]);
            }
        }
    }
}