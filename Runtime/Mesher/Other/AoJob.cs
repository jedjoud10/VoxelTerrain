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
        public NativeArray<float3> precomputeSamples;
        
        public float globalOffset;
        public float minDotNormal;
        public float globalSpread;
        public float strength;
        const int SIZE = 2;

        public void Execute(int index) {
            float3 vertex = positions[index];
            float3 normal = normals[index];

            int sum = 0;
            int total = 0;

            for (int x = -SIZE; x <= SIZE; x++) {
                for (int y = -SIZE; y <= SIZE; y++) {
                    for (int z = -SIZE; z <= SIZE; z++) {
                        float3 offset = new float3(x, y, z) * globalSpread;

                        if (math.all(offset == 0f)) {
                            continue;
                        }

                        if (math.dot(math.normalize(offset), normal) > minDotNormal) {
                            total++;
                        } else {
                            continue;
                        }

                        float3 position = vertex + offset + globalOffset;
                        int3 floored = (int3)math.floor(position);

                        if (VoxelUtils.CheckCubicVoxelPosition(floored, neighbourMask)) {
                            half density = VoxelUtils.SampleDensityInterpolated(position, ref densityDataPtrs);
                            if (density < 0.0) {
                                sum++;
                            }
                        }
                    }
                }
            }

            float factor = math.clamp((float)sum / (float)total, 0f, 1f);
            float ao = math.saturate(1 - factor * strength);
            colours[index] = new float4(ao, 0, 0, 0);
        }
    }
}