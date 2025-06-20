using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static jedjoud.VoxelTerrain.VoxelUtils;

namespace jedjoud.VoxelTerrain.Meshing {
    internal struct MergeMeshHandler : ISubHandler {
        public Vertices mergedVertices;
        public NativeArray<int> mergedIndices;
                
        public JobHandle jobHandle;
        
        public NativeArray<int> submeshIndexOffsets;
        public NativeArray<int> submeshIndexCounts;
        public NativeReference<int> totalVertexCount;
        public NativeReference<int> totalIndexCount;
        

        public void Init() {
            mergedVertices = new Vertices(VOLUME, Allocator.Persistent);
            mergedIndices = new NativeArray<int>(VOLUME * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            
            submeshIndexOffsets = new NativeArray<int>(7, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            submeshIndexCounts = new NativeArray<int>(7, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            totalVertexCount = new NativeReference<int>(Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            totalIndexCount = new NativeReference<int>(Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        public void Schedule(ref CoreSnHandler core, ref SkirtSnHandler skirt) {
            totalVertexCount.Value = 0;
            totalIndexCount.Value = 0;

            MergeMeshJob mergeMeshJob = new MergeMeshJob {
                vertices = core.vertices,
                indices = core.indices,

                vertexCounter = core.vertexCounter,
                triangleCounter = core.triangleCounter,

                skirtVertices = skirt.skirtVertices,

                skirtStitchedIndices = skirt.skirtStitchedIndices,
                skirtForcedPerFaceIndices = skirt.skirtForcedPerFaceIndices,

                skirtVertexCounter = skirt.skirtVertexCounter,

                skirtStitchedTriangleCounter = skirt.skirtStitchedTriangleCounter,
                skirtForcedTriangleCounter = skirt.skirtForcedTriangleCounter,

                submeshIndexCounts = submeshIndexCounts,
                submeshIndexOffsets = submeshIndexOffsets,
                totalIndexCount = totalIndexCount,
                totalVertexCount = totalVertexCount,

                mergedVertices = mergedVertices,
                mergedIndices = mergedIndices,
            };

            JobHandle dependencies = JobHandle.CombineDependencies(skirt.skirtQuadJobHandle, core.quadJobHandle);
            jobHandle = mergeMeshJob.Schedule(dependencies);
        }

        public void Dispose() {
            submeshIndexCounts.Dispose();
            submeshIndexOffsets.Dispose();
            totalIndexCount.Dispose();
            totalVertexCount.Dispose();

            mergedVertices.Dispose();
            mergedIndices.Dispose();
        }
    }
}