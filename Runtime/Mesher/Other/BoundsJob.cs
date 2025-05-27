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
        public NativeReference<MinMaxAABB> bounds;

        public void Execute() {
            float3 min = 10000;
            float3 max = -10000;

            for (int i = 0; i < vertexCounter.Count; i++) {
                min = math.min(min, vertices[i]);
                max = math.max(max, vertices[i]);
            }

            // add a slight offset to encapsulate the skirts as well...
            const float EPSILON = 0.5f;
            min -= new float3(EPSILON);
            max += new float3(EPSILON);

            bounds.Value = new MinMaxAABB {
                Min = min,
                Max = max
            }; 
        }
    }
}