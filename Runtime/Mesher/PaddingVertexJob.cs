using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct PaddingVertexJob : IJobParallelFor {
        // Positions of the first vertex in edges
        [ReadOnly]
        static readonly uint3[] edgePositions0 = new uint3[] {
            new uint3(0, 0, 0),
            new uint3(1, 0, 0),
            new uint3(1, 1, 0),
            new uint3(0, 1, 0),
            new uint3(0, 0, 1),
            new uint3(1, 0, 1),
            new uint3(1, 1, 1),
            new uint3(0, 1, 1),
            new uint3(0, 0, 0),
            new uint3(1, 0, 0),
            new uint3(1, 1, 0),
            new uint3(0, 1, 0),
        };

        // Positions of the second vertex in edges
        [ReadOnly]
        static readonly uint3[] edgePositions1 = new uint3[] {
            new uint3(1, 0, 0),
            new uint3(1, 1, 0),
            new uint3(0, 1, 0),
            new uint3(0, 0, 0),
            new uint3(1, 0, 1),
            new uint3(1, 1, 1),
            new uint3(0, 1, 1),
            new uint3(0, 0, 1),
            new uint3(0, 0, 1),
            new uint3(1, 0, 1),
            new uint3(1, 1, 1),
            new uint3(0, 1, 1),
        };

        // Boundary voxels (at v=63)
        [ReadOnly]
        public NativeArray<Voxel> boundaryVoxels;

        // Padding voxels (at v=64)
        [ReadOnly]
        public NativeArray<Voxel> paddingVoxels;

        [WriteOnly]
        public NativeArray<int> paddingIndices;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> vertices;

        public Unsafe.NativeCounter.Concurrent counter;

        // Selects between fetching from padding voxels or boundary voxels
        private Voxel Fetch(uint3 position) {
            if (StitchUtils.LiesOnBoundary(position, 65)) {
                return paddingVoxels[StitchUtils.PosToBoundaryIndex(position, 65)];
            } else {
                return boundaryVoxels[StitchUtils.PosToBoundaryIndex(position, 64)];
            }
        }

        public void Execute(int index) {
            paddingIndices[index] = int.MaxValue;

            uint3 position = StitchUtils.BoundaryIndexToPos(index, 64);

            float3 vertex = float3.zero;

            // Create the smoothed vertex
            // TODO: Test out QEF or other methods for smoothing here
            int count = 0;
            for (int edge = 0; edge < 12; edge++) {
                uint3 startOffset = edgePositions0[edge];
                uint3 endOffset = edgePositions1[edge];
                
                Voxel startVoxel = Fetch(startOffset + position);
                Voxel endVoxel = Fetch(endOffset + position);

                if (startVoxel.density > 0f ^ endVoxel.density > 0f) {
                    count++;
                    float value = math.unlerp(startVoxel.density, endVoxel.density, 0);
                    vertex += math.lerp(startOffset, endOffset, value) - math.float3(0.5);
                }
            }

            if (count == 0)
                return;

            if (count >= 1 && VoxelUtils.BLOCKY) {
                count = 1;
                vertex = 0f;
            }

            int vertexIndex = counter.Increment();
            paddingIndices[index] = vertexIndex;

            // Output vertex in object space
            float3 offset = vertex / (float)count;
            float3 outputVertex = offset + position;
            vertices[vertexIndex] = outputVertex + 0.5f;
        }
    }
}