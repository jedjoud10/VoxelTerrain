using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    // NOTE!!!!!
    // There are cases where the stitching fails and leaves some gaps behind
    // I know this happens and I think I know why this occurs but I can't manage to replicate it again


    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, OptimizeFor = OptimizeFor.Performance)]
    public struct StitchQuadJob : IJobParallelFor {
        // LOD0 data for the current LOD0 chunk
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        // LOD0 chunk offset relative to LOD1 big man thingy
        // in the range of 0 to 1
        // this is in the FACE space, where X,Y are just face local coordinates
        public uint2 relativeOffsetToLod1;

        // LOD0 FACE indices
        // 2D mortonated
        // remember that this is 64x64x2, so we must add a 64x64 offset first to access the ones on the boundary
        [ReadOnly]
        public NativeArray<int> lod0indices;

        // LOD1 FACE indices
        // 2D mortonated
        [ReadOnly]
        public NativeArray<int> lod1indices;

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

        /*
        // Fetches the vertex index of a vertex in a specific position
        // If x=-1, then we use the vertex data from LOD1
        // If x=0, then we use vertex data from LOD0
        private int GetVertexIndex(int3 position) {
            if (position.x < 0) {
                uint2 offset = relativeOffsetToLod1 * VoxelUtils.SIZE / 2;

                // LOD1 voxels are twice as big as LOD0
                uint2 rounded = (uint2)position.yz / 2 + offset;
                return lod1indices[VoxelUtils.PosToIndexMorton2D(rounded) + VoxelUtils.SIZE * VoxelUtils.SIZE];
            } else {
                return lod0indices[VoxelUtils.PosToIndexMorton2D((uint2)position.yz)];
            }
        }
        */

        // Check and edge and check if we must generate a quad in it's forward facing direction
        void CheckEdge(uint3 basePosition, int index) {
            /*
            uint3 forward = quadForwardDirection[index];

            Voxel startVoxel = voxels[VoxelUtils.PosToIndexMorton(basePosition)];
            Voxel endVoxel = voxels[VoxelUtils.PosToIndexMorton(basePosition + forward)];

            if (startVoxel.density > 0 == endVoxel.density > 0)
                return;

            bool flip = (endVoxel.density >= 0.0);

            // not a uint anymore since we will cross ze boundary...
            int3 offset = (int3)basePosition + (int3)forward - 1;

            // Fetch the indices of the vertex positions ACROSS THE LOD BOUNDARY 
            // Does the shit in multi-resolution style
            int vertex0 = GetVertexIndex(offset + (int3)quadPerpendicularOffsets[index * 4]);
            int vertex1 = GetVertexIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 1]);
            int vertex2 = GetVertexIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 2]);
            int vertex3 = GetVertexIndex(offset + (int3)quadPerpendicularOffsets[index * 4 + 3]);

            // Don't make a quad if the vertices are invalid
            if ((vertex0 | vertex1 | vertex2 | vertex3) == int.MaxValue)
                return;

            // Get the triangle index base
            int triIndex = counter.Increment() * 6;

            // Set the first tri
            triangles[triIndex + (flip ? 0 : 2)] = vertex0;
            triangles[triIndex + 1] = vertex1;
            triangles[triIndex + (flip ? 2 : 0)] = vertex2;

            // Set the second tri
            triangles[triIndex + (flip ? 3 : 5)] = vertex2;
            triangles[triIndex + 4] = vertex3;
            triangles[triIndex + (flip ? 5 : 3)] = vertex0;
            */
        }

        // Excuted for each cell within the grid
        public void Execute(int index) {
            /*
            uint2 facePos = VoxelUtils.IndexToPosMorton2D(index);
            uint3 position = new uint3(0, facePos);

            if (math.any(facePos < 1))
                return;

            if (math.any(facePos > 62))
                return;
            

            CheckEdge(position, 0);
            CheckEdge(position, 1);
            CheckEdge(position, 2);
            */
        }
    }
}