using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct AmbientOcclusionJob : IJobParallelForBatch {
        [ReadOnly]
        public NativeArray<Voxel> voxels;
        [WriteOnly]
        public NativeArray<float2> uvs;
        [ReadOnly]
        public NativeArray<float3> normals;
        [ReadOnly]
        public NativeArray<float3> vertices;
        [ReadOnly]
        public Unsafe.NativeCounter counter;
        [ReadOnly]
        public UnsafePtrList<Voxel> neighbours;
        [ReadOnly]
        public BitField32 neighbourMask;

        public float globalOffset;
        public float minDotNormal;
        public float globalSpread;
        public float strength;
        public float voxelScale;
        const int SIZE = 2;

        public void Execute(int startIndex, int count) {
            if (startIndex >= counter.Count)
                return;
            int endIndex = math.min(startIndex + count, counter.Count);

            for (int index = startIndex; index < endIndex; index++) {
                float3 vertex = vertices[index] / voxelScale;

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
                                half density = VoxelUtils.SampleDensityInterpolated(position, ref voxels, ref neighbours);
                                if (density < 0.0) {
                                    sum++;
                                }
                            }
                        }
                    }
                }

                float factor = math.clamp((float)sum / (float)total, 0f, 1f);
                uvs[index] = new float2(1 - factor * strength, 0.0f);
                //uvs[index] = new float2(1, 0.0f);
            }
        }
    }
}