using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SkirtQuadJob2 : IJobParallelFor {
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
        int FetchIndex(int3 position, int face) {
            int lookupOffset = 0;
            uint3 fetchingPosition = int.MaxValue;
            if (math.any(position < 0 | position > VoxelUtils.SIZE - 3)) {
                // skirt index
                fetchingPosition = (uint3)math.clamp(position, 0, VoxelUtils.SIZE - 3);
                lookupOffset = FACE;
            } else {
                // copied boundary index
                fetchingPosition = (uint3)position;
                lookupOffset = 0;
            }


            int direction = face % 3;
            uint2 flattened = SkirtUtils.FlattenToFaceRelative((uint3)fetchingPosition, direction);
            int lookup = VoxelUtils.PosToIndex2D(flattened, VoxelUtils.SIZE);

            return skirtVertexIndices[lookup + lookupOffset + 2 * FACE * face];
        }

        // Check and edge and check if we must generate a quad in it's forward facing direction
        void CheckEdge(uint2 flattened, uint3 unflattened, int index, bool force, bool negative, int face) {
            uint3 forward = quadForwardDirection[index];

            bool flip = !negative;
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
                if (negative) {
                    offset[index] -= 1;
                } else {
                    offset[index] += 1;
                }
            }

            int vertex0 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4], face);
            int vertex1 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 1], face);
            int vertex2 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 2], face);
            int vertex3 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 3], face);

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

        const int FACE = VoxelUtils.SIZE * VoxelUtils.SIZE;

        public void Execute(int index) {
            int face = index / FACE;
            int direction = face % 3;
            bool negative = face < 3;
            int localIndex = index % FACE;

            uint missing = negative ? 0 : ((uint)VoxelUtils.SIZE - 2);

            uint2 flattened = VoxelUtils.IndexToPos2D(localIndex, VoxelUtils.SIZE);
            uint3 position = SkirtUtils.UnflattenFromFaceRelative(flattened, direction, missing);

            if (math.any(flattened > VoxelUtils.SIZE - 2))
                return;

            CheckEdge(flattened, position, 0, direction == 0, negative, face);
            CheckEdge(flattened, position, 1, direction == 1, negative, face);
            CheckEdge(flattened, position, 2, direction == 2, negative, face);
        }
    }
}