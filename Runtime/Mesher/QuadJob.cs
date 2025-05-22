using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct QuadJob : IJobParallelFor {
        // Voxel native array
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        // Contains 3D data of the indices of the vertices
        [ReadOnly]
        public NativeArray<int> vertexIndices;

        // Triangles that we generated
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> triangles;

        // Bit shift used to check for edges
        [ReadOnly]
        static readonly int[] shifts = new int[3] {
            0, 3, 8
        };

        const int EMPTY_MASK = 1 << 0 | 1 << 3 | 1 << 8;
        
        // Used for fast traversal
        [ReadOnly]
        public NativeArray<byte> enabled;

        [WriteOnly]
        public NativeCounter.Concurrent quadCounter;

        // Check and edge and check if we must generate a quad in it's forward facing direction
        void CheckEdge(int index, uint3 basePosition, int direction) {
            uint3 forward = EdgeMaskUtils2.FORWARD_DIRECTION[direction];
            int forwardIndexOffset = EdgeMaskUtils3.FORWARD_DIRECTION_INDEX_OFFSET[direction];

            int baseIndex = index;
            int endIndex = forwardIndexOffset + index;

            Voxel startVoxel = voxels[baseIndex];
            Voxel endVoxel = voxels[endIndex];

            bool flip = (endVoxel.density > 0.0);

            byte material = flip ? startVoxel.material : endVoxel.material;
            uint3 offset = basePosition + forward - math.uint3(1);

            // Fetch the indices of the vertex positions
            int4 indices = int.MaxValue;
            int4 positionalIndex = index + EdgeMaskUtils3.NEGATIVE_ONE_OFFSET + EdgeMaskUtils3.PERPENDICULAR_OFFSETS_INDEX_OFFSET[direction];
            for (int i = 0; i < 4; i++) {
                //int positionalIndex = VoxelUtils.PosToIndex(offset + EdgeMaskUtils2.PERPENDICULAR_OFFSETS[direction * 4 + i], VoxelUtils.SIZE);
                //int positionalIndex = 
                indices[i] = vertexIndices[positionalIndex[i]];
                //indices[i] = vertexIndices[positionalIndex];
            }

            // Don't make a quad if the vertices are invalid
            if (math.cmax(indices) == int.MaxValue)
                return;

            // Get the triangle index base
            int triIndex = quadCounter.Increment() * 6;
            
            // Set the first tri
            triangles[triIndex + (flip ? 0 : 2)] = indices[0];
            triangles[triIndex + 1] = indices[1];
            triangles[triIndex + (flip ? 2 : 0)] = indices[2];

            // Set the second tri
            triangles[triIndex + (flip ? 3 : 5)] = indices[2];
            triangles[triIndex + 4] = indices[3];
            triangles[triIndex + (flip ? 5 : 3)] = indices[0];
        }

        // Excuted for each cell within the grid
        public void Execute(int index) {
            uint3 position = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);

            if (math.any(position > VoxelUtils.SIZE-3))
                return;

            byte code = enabled[index];
            if (code == 0 || code == 255)
                return;

            // Allows us to save two voxel fetches (very important)
            ushort enabledEdges = EdgeMaskUtils.EDGE_MASKS[code];

            if ((enabledEdges & EMPTY_MASK) == 0)
                return;

            for (int i = 0; i < 3; i++) {
                // we CAN do quad stuff on the v=0 boundary as long as we're doing it parallel to the face boundary
                if (math.any(position < (1 - EdgeMaskUtils2.FORWARD_DIRECTION[i])))
                    continue;
                
                if (((enabledEdges >> shifts[i]) & 1) == 1) {
                    CheckEdge(index, position, i);
                }
            }
        }
    }
}