using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    // Surface mesh job that will generate the isosurface mesh vertices
    [BurstCompile(CompileSynchronously = true)]
    public struct VertexJob : IJobParallelFor {
        [ReadOnly]
        public VoxelData voxels;

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
        public Vertices vertices;

        // Vertex Counter
        public NativeCounter.Concurrent vertexCounter;

        // Excuted for each cell within the grid
        public void Execute(int index) {
            uint3 position = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);
            indices[index] = int.MaxValue;

            if (math.any(position > VoxelUtils.SIZE - 3))
                return;

            // Fetch the byte that contains the number of corners active
            uint enabledCorners = enabled[index];
            bool empty = enabledCorners == 0 || enabledCorners == 255;

            // Early check to quit if the cell if full / empty
            if (empty) return;

            // Doing some marching cube shit here
            uint code = EdgeMaskUtils.EDGE_MASKS[enabledCorners];
            int count = math.countbits(code);

            // Create the smoothed vertex
            Vertices.Single vertex = new Vertices.Single();
            for (int edge = 0; edge < 12; edge++) {
                // Continue if the edge isn't inside
                if (((code >> edge) & 1) == 0) continue;

                uint3 startOffset = EdgePositionUtils.EDGE_POSITIONS_0[edge];
                uint3 endOffset = EdgePositionUtils.EDGE_POSITIONS_1[edge];

                int startIndex = VoxelUtils.PosToIndex(position + startOffset, VoxelUtils.SIZE);
                int endIndex = VoxelUtils.PosToIndex(position + endOffset, VoxelUtils.SIZE);
                vertex.Add(startOffset, endOffset, startIndex, endIndex, ref voxels, ref voxelNormals);
            }

            vertex.Finalize(count);
            vertex.position += position;

            int vertexIndex = vertexCounter.Increment();
            indices[index] = vertexIndex;
            vertices[vertexIndex] = vertex;
        }
    }
}