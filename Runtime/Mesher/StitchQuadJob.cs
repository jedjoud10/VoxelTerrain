using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Plastic.Antlr3.Runtime;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    // NOTE!!!!!
    // There are cases where the stitching fails and leaves some gaps behind
    // I know this happens and I think I know why this occurs but I can't manage to replicate it again

    // for now, assume uniformity
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, OptimizeFor = OptimizeFor.Performance)]
    public unsafe struct StitchQuadJob : IJobParallelFor {
        public NativeArray<float4> debugData;
        
        // Source boundary voxels
        [ReadOnly]
        public NativeArray<Voxel> srcBoundaryVoxels;

        // Source boundary indices
        [ReadOnly]
        public NativeArray<int> srcBoundaryIndices;

        // Neighbouring boundary voxels
        [ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public VoxelStitch.GenericBoundaryData<Voxel> neighbourVoxels;

        // Neighbouring boundary vertex indices
        [ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public VoxelStitch.GenericBoundaryData<int> neighbourIndices;

        // Index offsets since we merged all the vertices in a contiguous manner
        [ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public NativeArray<int> indexOffsets;

        // Triangles that we generated
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> triangles;
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
        
        // Quad Counter
        [WriteOnly]
        public Unsafe.NativeCounter.Concurrent counter;

        // Fetches the vertex index of a vertex in a specific position
        // If this crosses the v=65 boundary, use the neighbouring chunks' negative boundary indices instead
        private int GetVertexIndex(uint3 position) {
            /*
            if (neighbourIndices.state.Value != 14325) {
                return int.MaxValue;
            }
            */

            Debug.Log($"GetVertexIndex, pos = {position}, state = {neighbourIndices.state.Value}");
            if (math.any(position >= 64)) {
                int unpackedNeighbourIndex = StitchUtils.FetchUnpackedNeighbourIndex(position, neighbourIndices.state);
                
                if (unpackedNeighbourIndex == -1) {
                    return int.MaxValue;
                }
                
                int indexOffset = indexOffsets[unpackedNeighbourIndex];

                if (indexOffset == -1) {
                    Debug.LogError("what the sigmoid?");
                }
            
                int index = StitchUtils.Sample<int>(position, ref neighbourIndices, -1);

                // it's fine if this happens, just means that we didn't find proper neighbours (happens for the chunks on the map edge)
                if (index == -1) {
                    return int.MaxValue;
                }

                // COULD HAPPEN, AND IT IS VERY BAD IF IT DOES!!!!
                // means that we are thinking we "think" there's a valid vertex there, but it isn't actually there!
                if (index == int.MaxValue) {
                    Debug.LogError("notto good at allu");
                }

                Debug.Log($"NEIGHBOUR!! unpackedNeighbourIndex = {unpackedNeighbourIndex}, indexOffset = {indexOffset}, index = {index}");
                return index + indexOffset;
            } else {
                Debug.Log($"SOURCE!!!");
                return srcBoundaryIndices[StitchUtils.PosToBoundaryIndex(position, 64)];
            }
        }

        // Check and edge and check if we must generate a quad in it's forward facing direction
        void CheckEdge(uint3 basePosition, int index) {
            uint3 forward = quadForwardDirection[index];

            // When we implement multi-res, swap between the two sets here
            // Either sample from pos-boundary of self, or neg-boundary of neighbours
            Voxel startVoxel = srcBoundaryVoxels[StitchUtils.PosToBoundaryIndex(basePosition, 65)];
            Voxel endVoxel = srcBoundaryVoxels[StitchUtils.PosToBoundaryIndex(basePosition + forward, 65)];

            if (startVoxel.density > 0 == endVoxel.density > 0)
                return;

            int bitsset = math.countbits(math.bitmask(new bool4(basePosition == new uint3(64), false)));
            float data = bitsset == 2 ? 0.2f : 0f;
            float3 pos = (float3)basePosition + 0.5f * (float3)forward;
            debugData[StitchUtils.PosToBoundaryIndex(basePosition, 65)] = new float4(pos, 0.0f);

            /*
            if (bitsset != 2)
                return;
            */

            bool flip = (endVoxel.density >= 0.0);

            uint3 offset = basePosition + forward - 1;

            int vertex0 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4]);
            int vertex1 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4 + 1]);
            int vertex2 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4 + 2]);
            int vertex3 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4 + 3]);

            // Don't make a quad if the vertices are invalid
            if ((vertex0 | vertex1 | vertex2 | vertex3) == int.MaxValue)
                return;

            int4 what = new int4(vertex0, vertex1, vertex2, vertex3);
            if (math.any(what > 500000 | what < 0)) {
                Debug.LogError($"what: {what}");
                debugData[StitchUtils.PosToBoundaryIndex(basePosition, 65)] = new float4(pos, 0.2f);
            }

            // Get the triangle index base
            int triIndex = counter.Add(6);

            // Set the first tri
            triangles[triIndex + (flip ? 0 : 2)] = vertex0;
            triangles[triIndex + 1] = vertex1;
            triangles[triIndex + (flip ? 2 : 0)] = vertex2;

            // Set the second tri
            triangles[triIndex + (flip ? 3 : 5)] = vertex2;
            triangles[triIndex + 4] = vertex3;
            triangles[triIndex + (flip ? 5 : 3)] = vertex0;
        }

        // Excuted for each cell within the grid
        public void Execute(int index) {
            // When we implement multi-res, swap between the two sets here
            // Either sample from pos-boundary of self, or neg-boundary of neighbours
            uint3 position = StitchUtils.BoundaryIndexToPos(index, 65);
            
            for (int i = 0; i < 3; i++) {
                // need this to create tris that might be on the v=0 boundary
                bool skipnation = math.any(position < (1 - quadForwardDirection[i]));

                // need this to create tris that might be on the v=63 boundary
                bool skipnation2 = position[i] > 63;

                if (skipnation || skipnation2)
                    continue;

                CheckEdge(position, i);
            }
        }
    }
}