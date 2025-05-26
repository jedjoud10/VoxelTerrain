using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    // Surface mesh job that will generate the isosurface mesh vertices
    [BurstCompile(CompileSynchronously = true)]
    public struct VertexJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        [ReadOnly]
        public NativeArray<float3> voxelNormals;

        // Used for fast traversal
        [ReadOnly]
        public NativeArray<byte> enabled;

        // Contains 3D data of the indices of the vertices
        [WriteOnly]
        public NativeArray<int> indices;

        // Vertices that we generated
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> vertices;

        // Normals in case we have a shader that requires them
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> normals;

        // Vertex Counter
        public NativeCounter.Concurrent vertexCounter;
        public float voxelScale;

        // Excuted for each cell within the grid
        public void Execute(int index) {
            uint3 position = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);
            indices[index] = int.MaxValue;

            if (math.any(position > VoxelUtils.SIZE - 3))
                return;

            float3 vertex = float3.zero;
            float3 normal = float3.zero;

            // Fetch the byte that contains the number of corners active
            uint enabledCorners = enabled[index];
            bool empty = enabledCorners == 0 || enabledCorners == 255;

            // Early check to quit if the cell if full / empty
            if (empty) return;

            // Doing some marching cube shit here
            uint code = EdgeMaskUtils.EDGE_MASKS[enabledCorners];
            int count = math.countbits(code);

            // Create the smoothed vertex
            for (int edge = 0; edge < 12; edge++) {
                // Continue if the edge isn't inside
                if (((code >> edge) & 1) == 0) continue;

                uint3 startOffset = EdgePositionUtils.EDGE_POSITIONS_0[edge];
                uint3 endOffset = EdgePositionUtils.EDGE_POSITIONS_1[edge];

                int startIndex = VoxelUtils.PosToIndex(startOffset + position, VoxelUtils.SIZE);
                int endIndex = VoxelUtils.PosToIndex(endOffset + position, VoxelUtils.SIZE);

                float3 startNormal = voxelNormals[startIndex];
                float3 endNormal = voxelNormals[endIndex];

                // Get the Voxels of the edge
                Voxel startVoxel = voxels[startIndex];
                Voxel endVoxel = voxels[endIndex];

                // Create a vertex on the line of the edge
                float value = math.unlerp(startVoxel.density, endVoxel.density, 0);
                vertex += math.lerp(startOffset, endOffset, value);
                normal += math.lerp(startNormal, endNormal, value);
            }

            // Smooth the vertex with the number of edges that have a sign crossing
            // TODO: Test out QEF or other methods for smoothing
            vertex = vertex / (float)count + position;

            // Write vertex data and index
            int vertexIndex = vertexCounter.Increment();
            indices[index] = vertexIndex;
            vertices[vertexIndex] = vertex * voxelScale;
            normals[vertexIndex] = math.normalizesafe(normal, math.up());
        }
    }
}