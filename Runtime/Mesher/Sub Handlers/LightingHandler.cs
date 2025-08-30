using Unity.Mathematics;
using Unity.Jobs;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Meshing {
    internal struct LightingHandler : ISubHandler {
        public JobHandle jobHandle;
        public LightingUtils.AmbientOcclusionCache aoCache;

        public void Init() {
            aoCache.Init();
        }

        public void Schedule(ref VoxelData voxels, ref MergeMeshHandler merger, JobHandle dependency, Entity entity, EntityManager mgr) {
            JobHandle dep = JobHandle.CombineDependencies(merger.jobHandle, dependency);
            jobHandle = AsyncMemCpyUtils.FillAsync(merger.mergedVertices.colours, new float4(1), dep);
            /*
            Vertices vertices = merger.mergedVertices;

            unsafe {
                if (LightingUtils.TryCalculateLightingForChunkEntity(mgr, entity, vertices, ref aoCache, dep, merger.totalVertexCount.GetUnsafePtrWithoutChecks(), out JobHandle handle)) {
                    jobHandle = handle;
                } else {
                    jobHandle = default;

                    // since we can't calculate lighting rn, defer it to the TerrainLightingSystem for later
                    mgr.SetComponentEnabled<TerrainChunkRequestLightingTag>(entity, true);
                }
            }
            */
        }

        public void Dispose() {
            jobHandle.Complete();
            aoCache.Dispose();
        }
    }
}