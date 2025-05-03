using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct CreatePaddingVerticesLod1Job : IJobParallelFor {
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

        // Voxel array for LOD1
        // 3d morton encoded
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        // Extra padding voxels that contain blurred data of LOD0 neighbours
        // 2D morton encoded
        [ReadOnly]
        public NativeArray<Voxel> paddingBlurredFaceVoxels;

        // Contains 3D data of the indices of the vertices
        [WriteOnly]
        public NativeArray<int> indices;

        // Vertices that we generated
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> vertices;

        // Vertex Counter
        public Unsafe.NativeCounter.Concurrent counter;

        private Voxel FetchVoxel(uint3 position) {
            if (position.x > 63) {
                return paddingBlurredFaceVoxels[VoxelUtils.PosToIndexMorton2D(position.yz)];
            } else {
                return voxels[VoxelUtils.PosToIndexMorton(position)];
            }
        }

        public void Execute(int _index) {
            indices[_index] = int.MaxValue;


            // we run this as 64x64x2 so we need to take account for the x2
            int index = _index % (VoxelUtils.SIZE * VoxelUtils.SIZE);
            int slice = _index / (VoxelUtils.SIZE * VoxelUtils.SIZE);

            uint2 facePos = VoxelUtils.IndexToPosMorton2D(index);
            uint3 position = new uint3(62 + (uint)slice, facePos);

            if (!math.all(facePos < 63))
                return;

            float3 vertex = float3.zero;

            // Create the smoothed vertex
            // TODO: Test out QEF or other methods for smoothing here
            int count = 0;
            for (int edge = 0; edge < 12; edge++) {
                uint3 startOffset = edgePositions0[edge];
                uint3 endOffset = edgePositions1[edge];
                
                Voxel startVoxel = FetchVoxel(startOffset + position);
                Voxel endVoxel = FetchVoxel(endOffset + position);

                if (startVoxel.density > 0f ^ endVoxel.density > 0f) {
                    count++;
                    float value = math.unlerp(startVoxel.density, endVoxel.density, 0);
                    vertex += math.lerp(startOffset, endOffset, value) - math.float3(0.5);
                }
            }

            if (count == 0)
                return;

            if (count >= 1 && VoxelUtils.BLOCKY) {
                count = 1;
                vertex = 0f;
            }

            // Must be offset by
            int vertexIndex = counter.Increment();
            indices[_index] = vertexIndex;

            // Output vertex in object space
            float3 offset = (vertex / (float)count);
            float3 outputVertex = (offset) + position;
            vertices[vertexIndex] = outputVertex + 0.5f;
        }
    }
}