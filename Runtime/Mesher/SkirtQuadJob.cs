using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SkirtQuadJob : IJobParallelFor {
        // -X, -Y, -Z, X, Y, Z
        public int faceIndex;
        
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        // indices for the skirt vertices 
        // first 66*66 vertices are from the border of the chunk (already generated)
        // next 66*66 vertices are the ones we generate in this job here
        [ReadOnly]
        public NativeArray<int> skirtVertexIndices;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> skirtIndices;

        public Unsafe.NativeCounter.Concurrent skirtQuadCounter;

        [ReadOnly]
        static readonly uint3[] quadForwardDirection = new uint3[3]
        {
            new uint3(1, 0, 0),
            new uint3(0, 1, 0),
            new uint3(0, 0, 1),
        };

        // Quad vertices offsets based on direction
        [ReadOnly]
        static readonly uint3[] quadPerpendicularOffsets = new uint3[12]
        {
            new uint3(0, 0, 0),
            new uint3(0, 1, 0),
            new uint3(0, 1, 1),
            new uint3(0, 0, 1),

            new uint3(0, 0, 0),
            new uint3(0, 0, 1),
            new uint3(1, 0, 1),
            new uint3(1, 0, 0),

            new uint3(0, 0, 0),
            new uint3(1, 0, 0),
            new uint3(1, 1, 0),
            new uint3(0, 1, 0)
        };

        // Fetch vertex index for a specific position
        // If it goes out of the chunk bounds, assume it is a skirt vertex's position we're trying to fetch
        int FetchIndex(int3 position) {
            int lookupOffset = 0;
            uint3 fetchingPosition = int.MaxValue;
            if (math.any(position < 0 | position > VoxelUtils.SIZE-1)) {
                // skirt index
                fetchingPosition = (uint3)math.clamp(position, 0, VoxelUtils.SIZE - 2);
                lookupOffset = VoxelUtils.SIZE * VoxelUtils.SIZE;
            } else {
                // copied boundary index
                fetchingPosition = (uint3)position;
                lookupOffset = 0;
            }

            uint2 flattened = SkirtUtils.FlattenToFaceRelative((uint3)fetchingPosition, 0);
            int lookup = VoxelUtils.PosToIndex2D(flattened, VoxelUtils.SIZE);

            return lookup + lookupOffset;
        }

        // Check and edge and check if we must generate a quad in it's forward facing direction
        void CheckEdge(uint2 flattened, uint3 unflattened, int index, bool force) {
            uint3 forward = quadForwardDirection[index];

            bool flip = false;
            if (!force) {
                int baseIndex = VoxelUtils.PosToIndex(unflattened, VoxelUtils.SIZE);
                int endIndex = VoxelUtils.PosToIndex(unflattened + forward, VoxelUtils.SIZE);

                Voxel startVoxel = voxels[baseIndex];
                Voxel endVoxel = voxels[endIndex];

                if (startVoxel.density > 0f == endVoxel.density > 0f)
                    return;

                flip = (endVoxel.density > 0.0);
            }

            int3 offset = (int3)(unflattened + forward - math.uint3(1));

            if (force) {
                offset[index] -= 1;
            }

            // Fetch the indices of the vertex positions
            int index0 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4]);
            int index1 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 1]);
            int index2 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 2]);
            int index3 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 3]);

            // Fetch the actual indices of the vertices
            int vertex0 = skirtVertexIndices[index0];
            int vertex1 = skirtVertexIndices[index1];
            int vertex2 = skirtVertexIndices[index2];
            int vertex3 = skirtVertexIndices[index3];

            // Don't make a quad if the vertices are invalid
            if ((vertex0 | vertex1 | vertex2 | vertex3) == int.MaxValue)
                return;


            // Set the first tri
            int triIndex = skirtQuadCounter.Increment() * 6;
            skirtIndices[triIndex + (flip ? 0 : 2)] = vertex0;
            skirtIndices[triIndex + 1] = vertex1;
            skirtIndices[triIndex + (flip ? 2 : 0)] = vertex2;

            // Set the second tri
            skirtIndices[triIndex + (flip ? 3 : 5)] = vertex2;
            skirtIndices[triIndex + 4] = vertex3;
            skirtIndices[triIndex + (flip ? 5 : 3)] = vertex0;
        }

        public void Execute(int index) {
            uint2 flattened = VoxelUtils.IndexToPos2D(index, VoxelUtils.SIZE);
            uint3 position = SkirtUtils.UnflattenFromFaceRelative(flattened, 0);

            if (math.any(position > VoxelUtils.SIZE - 2))
                return;

            CheckEdge(flattened, position, 1, false);
            CheckEdge(flattened, position, 2, false);
            CheckEdge(flattened, position, 0, true);
        }
    }
}