using System;
using jedjoud.VoxelTerrain.Meshing;
using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    public static class LightingUtils {
        public struct AmbientOcclusionCache {
            public NativeArray<float4> precomputedSamples;
            public UnsafePtrList<half> densityPtrs;
            public NativeArray<half> what;

            public void Init() {
                densityPtrs = new UnsafePtrList<half>(27, Allocator.Persistent);
                what = new NativeArray<half>(VoxelUtils.VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                precomputedSamples = LightingUtils.PrecomputeAoSamples(Allocator.Persistent);
            }

            public void Dispose() {
                precomputedSamples.Dispose();
                densityPtrs.Dispose();
                what.Dispose();
            }
        }

        public const float AO_GLOBAL_SPREAD = 0.5f;
        public const float AO_GLOBAL_OFFSET = 0.0f;
        public const float AO_MIN_DOT_NORMAL = 0.2f;
        public const int AO_SAMPLES = (AO_SAMPLE_CUBE_SIZE*2+1) * (AO_SAMPLE_CUBE_SIZE * 2 + 1) * (AO_SAMPLE_CUBE_SIZE * 2 + 1);
        public const int AO_SAMPLE_CUBE_SIZE = 2;

        private static bool TryCheckShouldCalculateLighting(EntityManager entityManager, Entity entity, out NativeArray<Entity> entities) {
            TerrainManager terrainManager = new EntityQueryBuilder(Allocator.Temp).WithAll<TerrainManager>().Build(entityManager).GetSingleton<TerrainManager>();

            TerrainChunk chunk = entityManager.GetComponentData<TerrainChunk>(entity);

            OctreeNode self = chunk.node;
            BitField32 mask = chunk.neighbourMask;

            entities = new NativeArray<Entity>(27, Allocator.Temp);
            entities.FillArray(Entity.Null);

            for (int j = 0; j < 27; j++) {
                uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                int3 offset = (int3)_offset - 1;

                if (math.all(offset == int3.zero)) {
                    entities[j] = entity;
                    continue;
                }

                if (mask.IsSet(j)) {
                    OctreeNode neighbourNode = new OctreeNode {
                        size = self.size,
                        childBaseIndex = -1,
                        depth = self.depth,

                        // doesn't matter since we don't consider this in the hash/equality check!!!
                        index = -1,
                        parentIndex = -1,

                        position = self.position + offset * self.size,
                    };

                    if (terrainManager.chunks.TryGetValue(neighbourNode, out var neighbourChunk)) {
                        if (entityManager.IsComponentEnabled<TerrainChunkVoxels>(neighbourChunk)) {
                            entities[j] = neighbourChunk;
                        } else {
                            return false;
                        }
                    } else {
                        return false;
                    }
                }
            }

            return true;
        }

        public unsafe static bool TryCalculateLightingForChunkEntity(
            EntityManager mgr,
            Entity chunkEntity,
            Vertices vertices,
            ref AmbientOcclusionCache cache,
            JobHandle dependency,
            int* deferredVertexCountPtr,
            out JobHandle handle
        ) {
            handle = default;

            cache.densityPtrs.Clear();

            for (var i = 0; i < 27; i++) {
                cache.densityPtrs.Add((half*)IntPtr.Zero);
            }

            if (TryCheckShouldCalculateLighting(mgr, chunkEntity, out NativeArray<Entity> chunks)) {
                BitField32 neighbourMask = mgr.GetComponentData<TerrainChunk>(chunkEntity).neighbourMask;
                
                for (int j = 0; j < 27; j++) {
                    if (chunks[j] != Entity.Null) {
                        TerrainChunkVoxels voxels = mgr.GetComponentData<TerrainChunkVoxels>(chunks[j]);
                        voxels.asyncWriteJobHandle.Complete();
                        cache.densityPtrs[j] = (half*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(voxels.data.densities);
                    }
                }

                /*
                AoBlurJob blur = new AoBlurJob {
                    densityDataPtrs = cache.densityPtrs,
                    dstData = cache.what,
                    neighbourMask = neighbourMask,
                };

                AoApplyJob apply = new AoApplyJob {
                    colours = vertices.colours,
                    dstData = cache.what,
                    positions = vertices.positions,
                };

                JobHandle blurHandle = blur.Schedule(VoxelUtils.VOLUME, BatchUtils.QUARTER_BATCH, dependency);
                handle = apply.Schedule(deferredVertexCountPtr, BatchUtils.SMALLEST_VERTEX_BATCH, blurHandle);
                */

                AoJob job = new AoJob() {
                    positions = vertices.positions,
                    normals = vertices.normals,
                    colours = vertices.colours,

                    neighbourMask = neighbourMask,
                    densityDataPtrs = cache.densityPtrs,
                    precomputedSamples = cache.precomputedSamples
                };
                handle = job.Schedule(deferredVertexCountPtr, BatchUtils.SMALLEST_VERTEX_BATCH, dependency);

                for (int j = 0; j < 27; j++) {
                    if (chunks[j] != Entity.Null) {
                        TerrainChunkVoxels voxels = mgr.GetComponentData<TerrainChunkVoxels>(chunks[j]);
                        voxels.asyncReadJobHandle = JobHandle.CombineDependencies(voxels.asyncReadJobHandle, handle);
                        mgr.SetComponentData(chunks[j], voxels);
                    }
                }
                return true;
            } else {
                return false;
            }            
        }
        public static NativeArray<float4> PrecomputeAoSamples(Allocator allocator) {
            NativeArray<float4> tmp = new NativeArray<float4>(AO_SAMPLES, allocator);

            int index = 0;
            for (int x = -AO_SAMPLE_CUBE_SIZE; x <= AO_SAMPLE_CUBE_SIZE; x++) {
                for (int y = -AO_SAMPLE_CUBE_SIZE; y <= AO_SAMPLE_CUBE_SIZE; y++) {
                    for (int z = -AO_SAMPLE_CUBE_SIZE; z <= AO_SAMPLE_CUBE_SIZE; z++) {
                        float3 offset = new float3(x, y, z) * AO_GLOBAL_SPREAD;
                        float3 vec = math.forward();

                        float dotted = math.dot(math.normalize(offset), vec);
                        float strength = math.select(0, 1, dotted < AO_MIN_DOT_NORMAL);
                        tmp[index] = new float4(offset, 1 - strength);
                        index++;
                    }
                }
            }


            return tmp;
        }
    }
}