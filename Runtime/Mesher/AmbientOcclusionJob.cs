using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct AmbientOcclusionJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<Voxel> voxels;
        [ReadOnly]
        public UnsafePtrList<Voxel> allNeighbourPtr;
        [WriteOnly]
        public NativeArray<float2> uvs;
        [ReadOnly]
        public NativeArray<float3> normals;
        [ReadOnly]
        public NativeArray<float3> vertices;
        [ReadOnly]
        public Unsafe.NativeCounter counter;
        [ReadOnly]
        public bool3 positiveNeighbourMask;
        [ReadOnly]
        public bool3 negativeNeighbourMask;

        public float globalOffset;
        public float minDotNormal;
        public float globalSpread;
        public float strength;
        public float voxelScale;
        const int SIZE = 2;

        public void Execute(int index) {
            if (index >= counter.Count)
                return;

            float3 vertex = vertices[index] / voxelScale;
            float3 normal = normals[index];

            int sum = 0;
            int total = 0;
            /*
            int3 position = (int3)math.floor(vertex);
            if (VoxelUtils.CheckPositionAllNeighbours(position, negativeNeighbourMask, positiveNeighbourMask)) {
                Voxel voxel = VoxelUtils.FetchVoxelWithAllNeighbours(position, ref voxels, ref allNeighbourPtr);
                factor = math.sin(voxel.density) * 0.5f + 0.5f;
            }
            */

            

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

                        bool valid = VoxelUtils.CheckPositionAllNeighbours(floored, negativeNeighbourMask, positiveNeighbourMask);
                        bool valid2 = VoxelUtils.CheckPositionAllNeighbours(floored + 1, negativeNeighbourMask, positiveNeighbourMask);

                        if (valid && valid2) {
                            half density = VoxelUtils.SampleDensityInterpolated(position, ref voxels, ref allNeighbourPtr);
                            if (density < 0.0) {
                                sum++;
                            }
                        }
                    }
                }
            }

            float factor = math.clamp((float)sum / (float)total, 0f, 1f);
            

            /*
            float3 position = vertex + normal * 4.0f + globalOffset;
            int3 floored = (int3)math.floor(position);
            bool valid = VoxelUtils.CheckPositionAllNeighbours(floored, negativeNeighbourMask, positiveNeighbourMask);
            bool valid2 = VoxelUtils.CheckPositionAllNeighbours(floored + 1, negativeNeighbourMask, positiveNeighbourMask);

            float factor = 0.0f;
            if (valid && valid2) {
                half density = VoxelUtils.SampleDensityInterpolated(position, ref voxels, ref allNeighbourPtr);

                if (density < 0f) {
                    factor = 1.0f;
                }
            }
            */

            uvs[index] = new float2(1 - factor * strength, 0.0f);
        }
    }
}