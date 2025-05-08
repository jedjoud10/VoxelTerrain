using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct CopyBoundaryVerticesJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<int> indices;

        [ReadOnly]
        public NativeArray<float3> vertices;

        public Unsafe.NativeCounter.Concurrent counter;

        // Indices at the (x=63 || y=63 || z=63) boundary OR the (x=0 || y=0 || z=0)
        [WriteOnly]
        public NativeArray<int> boundaryIndices;

        // Vertices are sequential, so we can combine multiple ones easily later one
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> boundaryVertices;

        // Whether we copy the data for the positive or negative boundary
        public bool negative;

        public void Execute(int index) {
            int morton = VoxelUtils.PosToIndex(StitchUtils.BoundaryIndexToPos(index, 64, negative), 65);
            
            int oldVertexIndex = indices[morton];

            boundaryIndices[index] = int.MaxValue;
            if (oldVertexIndex == int.MaxValue)
                return;

            int newVertexIndex = counter.Increment();
            boundaryIndices[index] = newVertexIndex;

            boundaryVertices[newVertexIndex] = vertices[oldVertexIndex];
        }
    }
} 