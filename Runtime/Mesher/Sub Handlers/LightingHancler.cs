using static jedjoud.VoxelTerrain.VoxelUtils;
using static jedjoud.VoxelTerrain.BatchUtils;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Meshing {
    internal struct LightingHandler : ISubHandler {
        public JobHandle jobHandle;

        public void Init() {
        }

        public void Schedule(ref VoxelData voxels, ref MergeMeshHandler merger, JobHandle dependency, Entity chunkEntity, EntityManager mgr) {
            Vertices vertices = merger.mergedVertices;

            if (LightingUtils.TryCalculateLightingForChunkEntity(mgr, chunkEntity, out LightingUtils.UmmmData data)) {
                AoJob job = new AoJob() {
                    positions = vertices.positions,
                    normals = vertices.normals,
                    colours = vertices.colours,
                    strength = 1f,
                    globalSpread = 2f,
                    globalOffset = 0.5f,
                    minDotNormal = 0.5f,
                    neighbourMask = data.neighbourMask,
                    densityDataPtrs = data.densityPtrs,
                };

                unsafe {
                    int* countPtr = merger.totalVertexCount.GetUnsafePtrWithoutChecks();

                    JobHandle voxelDeps = dependency;
                    jobHandle = job.Schedule(countPtr, BatchUtils.SMALLEST_VERTEX_BATCH, JobHandle.CombineDependencies(merger.jobHandle, voxelDeps));
                }
            } else {
                jobHandle = default;
            }
        }

        public void Dispose() {
            jobHandle.Complete();
        }
    }
}