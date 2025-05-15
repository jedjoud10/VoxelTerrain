using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    // Surface mesh job that will generate the isosurface quads, and thus, the triangles
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, OptimizeFor = OptimizeFor.Performance)]
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
        static readonly int[] shifts = new int[3]
        {
            0, 3, 8
        };
        
        // Used for fast traversal
        [ReadOnly]
        public NativeArray<byte> enabled;

        // Quad Counter for each material
        [WriteOnly]
        public NativeMultiCounter.Concurrent counters;

        // Material counter to keep track of divido
        [ReadOnly]
        public NativeCounter materialCounter;

        // HashMap that converts the material index to submesh index
        [ReadOnly]
        public NativeParallelHashMap<byte, int>.ReadOnly materialHashMap;

        // Check and edge and check if we must generate a quad in it's forward facing direction
        void CheckEdge(uint3 basePosition, int index) {
            uint3 forward = VoxelUtils.FORWARD_DIRECTION[index];

            int baseIndex = VoxelUtils.PosToIndex(basePosition, VoxelUtils.SIZE);
            int endIndex = VoxelUtils.PosToIndex(basePosition + forward, VoxelUtils.SIZE);

            Voxel startVoxel = voxels[baseIndex];
            Voxel endVoxel = voxels[endIndex];

            bool flip = (endVoxel.density > 0.0);

            byte material = flip ? startVoxel.material : endVoxel.material;
            uint3 offset = basePosition + forward - math.uint3(1);

            // Fetch the indices of the vertex positions
            int index0 = VoxelUtils.PosToIndex(offset + VoxelUtils.PERPENDICULAR_OFFSETS[index * 4], VoxelUtils.SIZE);
            int index1 = VoxelUtils.PosToIndex(offset + VoxelUtils.PERPENDICULAR_OFFSETS[index * 4 + 1], VoxelUtils.SIZE);
            int index2 = VoxelUtils.PosToIndex(offset + VoxelUtils.PERPENDICULAR_OFFSETS[index * 4 + 2], VoxelUtils.SIZE);
            int index3 = VoxelUtils.PosToIndex(offset + VoxelUtils.PERPENDICULAR_OFFSETS[index * 4 + 3], VoxelUtils.SIZE);

            // Fetch the actual indices of the vertices
            int vertex0 = vertexIndices[index0];
            int vertex1 = vertexIndices[index1];
            int vertex2 = vertexIndices[index2];
            int vertex3 = vertexIndices[index3];

            // Don't make a quad if the vertices are invalid
            if ((vertex0 | vertex1 | vertex2 | vertex3) == int.MaxValue)
                return;

            // Get the triangle index base
            int packedMaterialIndex = materialHashMap[material];
            int segmentOffset = triangles.Length / materialCounter.Count;
            int triIndex = counters.Increment(packedMaterialIndex) * 6;
            triIndex += segmentOffset * packedMaterialIndex;

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
            uint3 position = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);

            if (math.any(position > VoxelUtils.SIZE-3))
                return;

            // Allows us to save two voxel fetches (very important)
            ushort enabledEdges = VoxelUtils.EdgeMasks[enabled[index]];

            for (int i = 0; i < 3; i++) {
                // we CAN do quad stuff on the v=0 boundary as long as we're doing it parallel to the face boundary
                if (math.any(position < (1 - VoxelUtils.FORWARD_DIRECTION[i])))
                    continue;
                
                if (((enabledEdges >> shifts[i]) & 1) == 1) {
                    CheckEdge(position, i);
                }
            }
        }
    }
}