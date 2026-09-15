using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct AoJob : IJobParallelForDefer {
        [ReadOnly]
        public NativeArray<float3> positions;
        [ReadOnly]
        public NativeArray<float3> normals;
        [WriteOnly]
        public NativeArray<float4> colours;
        [ReadOnly]
        public UnsafePtrList<half> densityDataPtrs;
        [ReadOnly]
        public BitField32 neighbourMask;
        [ReadOnly]
        public NativeArray<float4> precomputedSamples;
        
        public void Execute(int index) {
            float3 vertex = positions[index];
            float3 normal = normals[index];

            float sum = 0;
            
            quaternion rotation = quaternion.LookRotationSafe(normal, math.up());
            float3x3 matrix = new float3x3(rotation);
            //float3x3 matrix = float3x3.identity;

            for (int i = 0; i < LightingUtils.AO_SAMPLES; i++) {
                float4 rsample = precomputedSamples[i];
                float3 sample = math.mul(matrix, rsample.xyz);
                float3 position = vertex + sample + LightingUtils.AO_GLOBAL_OFFSET;
                int3 floored = (int3)math.floor(position);

                if (VoxelUtils.CheckCubicVoxelPosition(floored, neighbourMask)) {
                    half density = VoxelUtils.SampleDensityInterpolated(position, ref densityDataPtrs);
                    if (density < 0.0) {
                        sum += rsample.w;
                    }
                }
            }

            float factor = math.clamp((float)sum / (float)LightingUtils.AO_SAMPLES, 0f, 1f);
            float ao = math.saturate(1 - factor);
            colours[index] = new float4(0, 0, 0, ao);
        }
    }
}