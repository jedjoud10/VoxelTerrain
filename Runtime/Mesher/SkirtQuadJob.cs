using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SkirtQuadJob : IJobParallelFor {
        public NativeList<float3>.ParallelWriter debugData;

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
            int direction = face % 3;
            int lookupOffset = 0;
            //Debug.Log($"what?: {position}, face: {face}");

            int2 flattened = SkirtUtils.FlattenToFaceRelative(position, direction);
            int other = position[direction];

            if (other < 0 || other > VoxelUtils.SIZE-3) {
                flattened += 1;
                Debug.Log($"SKIRT flattened={flattened}");
                lookupOffset = FACE;
            } else {
                lookupOffset = 0;
                Debug.Log($"BOUNDARY flattened={flattened}");
            }

            flattened = math.clamp(flattened, 0, VoxelUtils.SIZE);
            int lookup = VoxelUtils.PosToIndex2D((uint2)flattened, VoxelUtils.SIZE);
            int res = skirtVertexIndices[lookup + lookupOffset + 2 * FACE * face];
            Debug.Log($"RES: {res}");
            return res;
        }

        // Check and edge and check if we must generate a quad in it's forward facing direction
        void CheckEdge(uint2 flattened, uint3 unflattened, int index, bool negative, int face) {
            uint3 forward = quadForwardDirection[index];

            int baseIndex = VoxelUtils.PosToIndex(unflattened, VoxelUtils.SIZE);
            int endIndex = VoxelUtils.PosToIndex(unflattened + forward, VoxelUtils.SIZE);

            Voxel startVoxel = voxels[baseIndex];
            Voxel endVoxel = voxels[endIndex];

            if (startVoxel.density > 0f == endVoxel.density > 0f)
                return;

            bool flip = (endVoxel.density > 0.0);


            int3 offset = (int3)((int3)unflattened + (int3)forward - math.int3(1));
            //debugData.AddNoResize((float3)offset);
            Debug.Log("BIG THINGS HAPPENING!!!");
            int vertex0 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4], face);
            int vertex1 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 1], face);
            int vertex2 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 2], face);
            int vertex3 = FetchIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 3], face);

            int4 v = new int4(vertex0, vertex1, vertex2, vertex3);
            SkirtUtils.AddQuadsOrTris(flip, v, ref skirtQuadCounter, ref skirtIndices);
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

            if (math.any(flattened > VoxelUtils.SIZE - 2 | flattened < 0)) {
                return;
            }


            
            CheckEdge(flattened, position, 0, negative, face);
            CheckEdge(flattened, position, 1, negative, face);
            CheckEdge(flattened, position, 2, negative, face);
        }
    }
}