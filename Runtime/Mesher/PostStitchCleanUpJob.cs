using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Plastic.Antlr3.Runtime;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, OptimizeFor = OptimizeFor.Performance)]
    public unsafe struct PostStitchCleanUpJob : IJob {
        public NativeArray<float3> srcVertices;
        public NativeArray<float3> dstVertices;
        public NativeArray<int> lookUp;
        public NativeArray<int> srcIndices;
        public NativeArray<int> dstIndices;
        public int indexCount;
        public NativeBitArray remappedVertices;

        public void Execute() {
            // remap the indices whilst uniquely remapping the vertices
            int vertexCount = 0;
            for (int i = 0; i < indexCount; i++) {
                int srcVertexIndex = srcIndices[i];

                // check if we need to copy the old vertex data and set the new index
                if (!remappedVertices.IsSet(srcVertexIndex)) {
                    remappedVertices.Set(srcVertexIndex, true);
                    dstVertices[vertexCount] = srcVertices[srcVertexIndex];
                    lookUp[srcVertexIndex] = vertexCount;
                    vertexCount++;
                }

                // do a bit of remapping
                dstIndices[i] = lookUp[srcVertexIndex];
            }
        }
    }
}