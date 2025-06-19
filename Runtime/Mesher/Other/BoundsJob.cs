using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct BoundsJob: IJob {
        [ReadOnly]
        public NativeArray<float3> mergedVertices;
        [ReadOnly]
        public NativeReference<int> totalVertexCount;
        public NativeReference<MinMaxAABB> bounds;

        public void Execute() {
            float3 min = 10000;
            float3 max = -10000;

            for (int i = 0; i < totalVertexCount.Value; i++) {
                min = math.min(min, mergedVertices[i]);
                max = math.max(max, mergedVertices[i]);
            }

            bounds.Value = new MinMaxAABB {
                Min = min,
                Max = max
            }; 
        }
    }
}