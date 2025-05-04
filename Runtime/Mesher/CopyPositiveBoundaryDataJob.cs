using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct CopyPositiveBoundaryDataJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        [ReadOnly]
        public NativeArray<int> indices;

        [ReadOnly]
        public NativeArray<float3> vertices;

        public Unsafe.NativeCounter.Concurrent counter;

        // Voxels|Indices at the (x=63 || y=63 || z=63) boundary
        [WriteOnly]
        public NativeArray<Voxel> boundaryVoxels;
        [WriteOnly]
        public NativeArray<int> boundaryIndices;

        // Vertices are sequential, so we can combine multiple ones easily later one
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> boundaryVertices;

        public void Execute(int index) {
            int morton = VoxelUtils.PosToIndexMorton(StitchUtils.BoundaryIndexToPos(index, 64));
            boundaryVoxels[index] = voxels[morton];

            int oldVertexIndex = indices[morton];

            int newVertexIndex = counter.Increment();
            boundaryIndices[index] = newVertexIndex;

            boundaryVertices[newVertexIndex] = vertices[oldVertexIndex];
        }
    }
}