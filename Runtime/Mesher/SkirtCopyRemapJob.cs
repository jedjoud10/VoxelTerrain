using jedjoud.VoxelTerrain.Unsafe;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SkirtCopyRemapJob : IJob {
        // -X, -Y, -Z, X, Y, Z
        public int faceIndex;
        
        [WriteOnly]
        public NativeArray<int> skirtIndices;

        [WriteOnly]
        public NativeArray<float3> skirtVertices;

        [ReadOnly]
        public NativeArray<int> indices;

        [ReadOnly]
        public NativeArray<float3> vertices;

        public NativeCounter skirtVertexCounter;

        public void Execute() {
            int boundaryVertexCount = 0;
            
            for (int i = 0; i < VoxelUtils.SIZE * VoxelUtils.SIZE; i++) {
                uint2 flattened = VoxelUtils.IndexToPos2D(i, VoxelUtils.SIZE);
                uint3 position = SkirtUtils.UnflattenFromFaceRelative(flattened, 0);
                int src = VoxelUtils.PosToIndex(position, VoxelUtils.SIZE);
                int srcIndex = indices[src];

                if (srcIndex != int.MaxValue) {
                    skirtVertices[boundaryVertexCount] = vertices[srcIndex];
                    skirtIndices[i] = boundaryVertexCount;
                    boundaryVertexCount++;
                } else {
                    skirtVertices[i] = 0f;
                    skirtIndices[i] = int.MaxValue;
                }
            }

            // start at an offset for the new skirt verts
            skirtVertexCounter.Count = boundaryVertexCount;
        }
    }
}