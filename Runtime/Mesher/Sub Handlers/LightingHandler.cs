using static jedjoud.VoxelTerrain.BatchUtils;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

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

            if (LightingUtils.TryCalculateLightingForChunkEntity(mgr, entity, out LightingUtils.UmmmData data)) {
                densityPtrs.Clear();
                densityPtrs.AddRange(data.tempDensityPtrs);

                AoJob job = new AoJob() {
                    positions = vertices.positions,
                    normals = vertices.normals,
                    colours = vertices.colours,

                    strength = LightingUtils.AO_STRENGTH,
                    globalSpread = LightingUtils.AO_GLOBAL_SPREAD,
                    globalOffset = LightingUtils.AO_GLOBAL_OFFSET,
                    minDotNormal = LightingUtils.AO_MIN_DOT_NORMAL,

                    neighbourMask = data.neighbourMask,
                    densityDataPtrs = densityPtrs,
                    precomputedSamples = precomputedSamples
                };

                unsafe {
                    int* countPtr = merger.totalVertexCount.GetUnsafePtrWithoutChecks();

                    JobHandle voxelDeps = dependency;
                    jobHandle = job.Schedule(countPtr, BatchUtils.SMALLEST_VERTEX_BATCH, JobHandle.CombineDependencies(merger.jobHandle, voxelDeps));
                }
            } else {
                jobHandle = default;

                // since we can't calculate lighting rn, defer it to the TerrainLightingSystem for later
                mgr.SetComponentEnabled<TerrainChunkRequestLightingTag>(entity, true);
            }
        }

        public void Dispose() {
            precomputedSamples.Dispose();
            jobHandle.Complete();
            densityPtrs.Dispose();
        }
    }
}