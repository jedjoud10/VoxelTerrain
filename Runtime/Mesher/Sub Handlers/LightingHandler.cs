using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Meshing {
    internal struct LightingHandler : ISubHandler {
        public JobHandle jobHandle;
        private NativeArray<float3> precomputedSamples;
        private UnsafePtrList<half> densityPtrs;


        public void Init() {
            densityPtrs = new UnsafePtrList<half>(0, Allocator.Persistent);
            precomputedSamples = LightingUtils.PrecomputeAoSamples();
        }


        public void Schedule(ref VoxelData voxels, ref MergeMeshHandler merger, JobHandle dependency, Entity entity, EntityManager mgr) {
            Vertices vertices = merger.mergedVertices;

            unsafe {
                JobHandle dep = JobHandle.CombineDependencies(merger.jobHandle, dependency);
                if (LightingUtils.TryCalculateLightingForChunkEntity(mgr, entity, vertices, precomputedSamples, ref densityPtrs, dep, merger.totalVertexCount.GetUnsafePtrWithoutChecks(), out JobHandle handle)) {
                    jobHandle = handle;
                } else {
                    jobHandle = default;

                    // since we can't calculate lighting rn, defer it to the TerrainLightingSystem for later
                    mgr.SetComponentEnabled<TerrainChunkRequestLightingTag>(entity, true);
                }
            }
        }

        public void Dispose() {
            precomputedSamples.Dispose();
            jobHandle.Complete();
            densityPtrs.Dispose();
        }
    }
}