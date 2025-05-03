using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, OptimizeFor = OptimizeFor.Performance)]
    public struct Lod1QuadJob : IJobParallelFor {
        // Voxel array for LOD1
        // 3d morton encoded
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        // Extra padding voxels that contain blurred data of LOD0 neighbours
        // 2D morton encoded
        [ReadOnly]
        public NativeArray<Voxel> paddingBlurredFaceVoxels;

        // Contains 3D data of the indices of the vertices
        [ReadOnly]
        public NativeArray<int> indices;

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

        private Voxel FetchVoxel(uint3 position) {
            position.x -= 1;
            if (position.x > 63) {
                //return Voxel.Empty;
                return paddingBlurredFaceVoxels[VoxelUtils.PosToIndexMorton2D(position.yz)];
            } else {
                return voxels[VoxelUtils.PosToIndexMorton(position)];
            }
        }

        private int CalculatePosIndex(uint3 position) {
            //Debug.Log(position.x);
            if (position.x > 63) {
                return VoxelUtils.PosToIndexMorton2D(position.yz) + VoxelUtils.SIZE*VoxelUtils.SIZE;
            } else {
                return VoxelUtils.PosToIndexMorton2D(position.yz);
            }
        }

        // Check and edge and check if we must generate a quad in it's forward facing direction
        void CheckEdge(uint3 basePosition, int index) {
            uint3 forward = quadForwardDirection[index];

            Voxel startVoxel = FetchVoxel(basePosition);
            Voxel endVoxel = FetchVoxel(basePosition + forward);

            if (startVoxel.density > 0 == endVoxel.density > 0)
                return;

            bool flip = (endVoxel.density >= 0.0);

            uint3 offset = basePosition + forward - 1;

            // Fetch the indices of the vertex positions

            int index0 = CalculatePosIndex(offset + quadPerpendicularOffsets[index * 4]);
            int index1 = CalculatePosIndex(offset + quadPerpendicularOffsets[index * 4 + 1]);
            int index2 = CalculatePosIndex(offset + quadPerpendicularOffsets[index * 4 + 2]);
            int index3 = CalculatePosIndex(offset + quadPerpendicularOffsets[index * 4 + 3]);

            // Fetch the actual indices of the vertices
            int vertex0 = indices[index0];
            int vertex1 = indices[index1];
            int vertex2 = indices[index2];
            int vertex3 = indices[index3];

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
        }

        // Excuted for each cell within the grid
        public void Execute(int index) {
            uint2 facePos = VoxelUtils.IndexToPosMorton2D(index);
            uint3 position = new uint3(64, facePos);

            if (math.any(facePos < 1))
                return;

            
            if (math.any(facePos > 62))
                return;
            

            CheckEdge(position, 0);
            CheckEdge(position, 1);
            CheckEdge(position, 2);


        }
    }
}