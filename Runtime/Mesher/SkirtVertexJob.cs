using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SkirtVertexJob : IJobParallelFor {
        public bool blocky;

        // -X, -Y, -Z, X, Y, Z
        public int faceIndex;
        
        // whole source chunk voxels
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        // indices for the skirt vertices 
        // first 66*66 vertices are from the border of the chunk (already generated)
        // next 66*66 vertices are the ones we generate in this job here
        [WriteOnly]
        public NativeArray<int> skirtVertexIndices;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        // first 66*66 vertices are from the border of the chunk (already generated)
        // next 66*66 vertices are the ones we generate in this job here
        public NativeArray<float3> skirtVertices;

        public Unsafe.NativeCounter.Concurrent skirtVertexCounter;

        // Positions of the first vertex in edges
        public static readonly uint3[] EDGE_POSITIONS_0_CUSTOM = new uint3[] {
            new uint3(0, 0, 0),
            new uint3(0, 0, 1),
            new uint3(0, 1, 1),
            new uint3(0, 1, 0),
        };

        // Positions of the second vertex in edges
        public static readonly uint3[] EDGE_POSITIONS_1_CUSTOM = new uint3[] {
            new uint3(0, 0, 1),
            new uint3(0, 1, 1),
            new uint3(0, 1, 0),
            new uint3(0, 0, 0),
        };

        public void Execute(int index) {
            uint2 flatten = VoxelUtils.IndexToPos2D(index, VoxelUtils.SIZE);
            uint3 position = SkirtUtils.UnflattenFromFaceRelative(flatten, 0);
            skirtVertexIndices[index] = int.MaxValue;

            if (math.any(flatten > VoxelUtils.SIZE - 2))
                return;


            float3 vertex = float3.zero;
            float3 normal = float3.zero;

            int count = 0;
            half average = (half)0f;
            for (int edge = 0; edge < 4; edge++) {
                uint3 startOffset = EDGE_POSITIONS_0_CUSTOM[edge];
                uint3 endOffset = EDGE_POSITIONS_1_CUSTOM[edge];

                int startIndex = VoxelUtils.PosToIndex(startOffset + position, VoxelUtils.SIZE);
                int endIndex = VoxelUtils.PosToIndex(endOffset + position, VoxelUtils.SIZE);

                Voxel startVoxel = voxels[startIndex];
                Voxel endVoxel = voxels[endIndex];

                average += startVoxel.density;
                average += endVoxel.density;

                if (startVoxel.density > 0 ^ endVoxel.density > 0) {
                    count++;
                    float value = math.unlerp(startVoxel.density, endVoxel.density, 0);
                    vertex += math.lerp(startOffset, endOffset, value) - math.float3(0.5);
                    normal += -math.up();

                    if (blocky)
                        break;
                }
            }

            if (count >= 1 && blocky) {
                count = 1;
                vertex = 0f;
                normal = -math.up();
            }

            // forcefully create the vertex 
            average = (half)(average / (half)(8f));
            bool force = average > -10f && average < 0f;
            
            if (count == 0 && !force) {
                return;
            }

            int vertexIndex = skirtVertexCounter.Increment();
            
            // we can use the skirt vertex counter directly since we offset it BEFORE we run this job
            skirtVertexIndices[index] = vertexIndex;

            float3 offset = 0f;

            if (force && count == 0) {
                offset = new float3(-0.5f, 0f, 0f);
            } else {
                offset = (vertex / (float)count);
            }

            float3 outputVertex = (offset) + position;

            // Whatever you FUCKING DO do NOT change the 0.5f offset
            // It is required to place the vertex INSIDE the cube of 8 voxel data points.
            // Just work with it lil bro
            skirtVertices[vertexIndex] = outputVertex + 0.5f;
        }
    }
}