using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    // Surface mesh job that will generate the isosurface mesh vertices
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct VertexJob : IJobParallelFor {
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

        // Voxel native array
        [ReadOnly]
        public NativeArray<Voxel> voxels;

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

        // Extra data passed to the shader for per vertex ambient occlusion
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float2> uvs;

        // Vertex Counter
        public Unsafe.NativeCounter.Concurrent counter;
        [ReadOnly] public float voxelScale;

        [ReadOnly]
        public UnsafePtrList<Voxel> neighbours;

        [ReadOnly]
        public BitField32 neighbourMask;

        // Excuted for each cell within the grid
        public void Execute(int index) {
            uint3 position = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE + 1);
            indices[index] = int.MaxValue;

            if (!VoxelUtils.CheckCubicVoxelPosition((int3)position, neighbourMask))
                return;

            float3 vertex = float3.zero;
            float3 normal = float3.zero;

            // Fetch the byte that contains the number of corners active
            uint enabledCorners = enabled[index];
            bool empty = enabledCorners == 0 || enabledCorners == 255;

            // Early check to quit if the cell if full / empty
            if (empty) return;

            // Doing some marching cube shit here
            uint code = VoxelUtils.EdgeMasks[enabledCorners];
            int count = math.countbits(code);

            // Create the smoothed vertex
            // TODO: Test out QEF or other methods for smoothing here
            for (int edge = 0; edge < 12; edge++) {
                // Continue if the edge isn't inside
                if (((code >> edge) & 1) == 0) continue;

                uint3 startOffset = edgePositions0[edge];
                uint3 endOffset = edgePositions1[edge];

                int startIndex = VoxelUtils.PosToIndexMorton(startOffset + position);
                int endIndex = VoxelUtils.PosToIndexMorton(endOffset + position);

                //float3 startNormal = VoxelUtils.SampleGridNormal(startOffset + position, ref voxels, ref neighbours);
                //float3 endNormal = VoxelUtils.SampleGridNormal(endOffset + position, ref voxels, ref neighbours);

                // Get the Voxels of the edge
                Voxel startVoxel = VoxelUtils.FetchVoxelNeighbours(startIndex, ref voxels, ref neighbours);
                Voxel endVoxel = VoxelUtils.FetchVoxelNeighbours(endIndex, ref voxels, ref neighbours);

                // Create a vertex on the line of the edge
                float value = math.unlerp(startVoxel.density, endVoxel.density, 0);
                vertex += math.lerp(startOffset, endOffset, value) - math.float3(0.5);
                normal += -math.up();
                //normal += math.lerp(startNormal, endNormal, value);
            }

            // Must be offset by vec3(1, 1, 1)
            int vertexIndex = counter.Increment();
            indices[index] = vertexIndex;

            // Output vertex in object space
            float3 offset = (vertex / (float)count);
            float3 outputVertex = (offset) + position;

            // Whatever you FUCKING DO do NOT change the 0.5f offset
            // It is required to place the vertex INSIDE the cube of 8 voxel data points.
            // Just work with it lil bro
            vertices[vertexIndex] = outputVertex * voxelScale + 0.5f;

            // Calculate per vertex normals and apply it
            normal = -math.normalizesafe(normal, new float3(0, 1, 0));
            normals[vertexIndex] = normal;
            uvs[vertexIndex] = new float2(0.0f, 0.0f);
        }
    }
}