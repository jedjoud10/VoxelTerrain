using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct DuplicateLod0VerticesJob : IJobParallelFor {
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

        // LOD0 voxels, used to create duplicate vertices
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        // Contains 3D data of the indices of the vertices
        [WriteOnly]
        public NativeArray<int> indices;

        // Vertices that we generated
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> vertices;

        // LOD0 chunk offset relative to LOD1 big man thingy
        // in the range of 0 to 1
        // this is in the FACE space, where X,Y are just face local coordinates
        public uint2 relativeOffsetToLod1;

        // Vertex Counter
        public Unsafe.NativeCounter.Concurrent counter;

        public void Execute(int index) {
            indices[index] = int.MaxValue;
            uint2 facePos = VoxelUtils.IndexToPosMorton2D(index);
            uint3 position = new uint3(0, facePos);

            if (!math.all(facePos < 63))
                return;

            float3 vertex = float3.zero;

            // Create the smoothed vertex
            // TODO: Test out QEF or other methods for smoothing here
            int count = 0;
            for (int edge = 0; edge < 12; edge++) {
                uint3 startOffset = edgePositions0[edge];
                uint3 endOffset = edgePositions1[edge];
                
                Voxel startVoxel = voxels[VoxelUtils.PosToIndexMorton(startOffset + position)];
                Voxel endVoxel = voxels[VoxelUtils.PosToIndexMorton(endOffset + position)];

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
            indices[index] = vertexIndex;

            // LOD0 chunk offset in WORLD SPACE
            float3 relativeNeighbourOffset = new float3(0, relativeOffsetToLod1) * VoxelUtils.SIZE / 2.0f;

            // remember, we are doing all of the shit in the x axis for now...
            relativeNeighbourOffset.x = VoxelUtils.SIZE;

            // Output vertex in object space but SHIFTED!!!
            float3 offset = (vertex / (float)count);
            float3 outputVertex = (offset + position) * 0.5f + relativeNeighbourOffset;
            vertices[vertexIndex] = outputVertex + 0.25f;

            // Tectonic plates be like
            // I'm shifting... I'm shifting... ooh.... I'm shifting
        }
    }
}