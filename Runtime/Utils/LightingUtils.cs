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
            NativeArray<float3> precomputedSamples,
            ref UnsafePtrList<half> densityPtrs,
            JobHandle dependency,
            int* deferredVertexCountPtr,
            out JobHandle handle
        ) {
            handle = default;

            densityPtrs.Clear();

            for (var i = 0; i < 27; i++) {
                densityPtrs.Add((half*)IntPtr.Zero);
            }

            if (TryCheckShouldCalculateLighting(mgr, chunkEntity, out NativeArray<Entity> chunks)) {
                BitField32 neighbourMask = mgr.GetComponentData<TerrainChunk>(chunkEntity).neighbourMask;
                
                for (int j = 0; j < 27; j++) {
                    if (chunks[j] != Entity.Null) {
                        TerrainChunkVoxels voxels = mgr.GetComponentData<TerrainChunkVoxels>(chunks[j]);
                        voxels.asyncWriteJobHandle.Complete();
                        densityPtrs[j] = (half*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(voxels.data.densities);
                    }
                }                

                AoJob job = new AoJob() {
                    positions = vertices.positions,
                    normals = vertices.normals,
                    colours = vertices.colours,

                    strength = LightingUtils.AO_STRENGTH,
                    globalOffset = LightingUtils.AO_GLOBAL_OFFSET,

                    neighbourMask = neighbourMask,
                    densityDataPtrs = densityPtrs,
                    precomputedSamples = precomputedSamples
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

        public const int AO_SAMPLES_SEED = 1234;
        public const int AO_SAMPLES = 16;
        public const float AO_STRENGTH = 1f;
        public const float AO_GLOBAL_SPREAD = 2.5f;
        public const float AO_GLOBAL_OFFSET = 0.5f;
        public const float AO_MIN_DOT_NORMAL = 0.2f;
        public const int AO_SAMPLE_CUBE_SIZE = 2;

        public static NativeArray<float3> PrecomputeAoSamples() {
            NativeList<float3> tmp = new NativeList<float3>(Allocator.Temp);

            for (int x = -AO_SAMPLE_CUBE_SIZE; x <= AO_SAMPLE_CUBE_SIZE; x++) {
                for (int y = -AO_SAMPLE_CUBE_SIZE; y <= AO_SAMPLE_CUBE_SIZE; y++) {
                    for (int z = -AO_SAMPLE_CUBE_SIZE; z <= AO_SAMPLE_CUBE_SIZE; z++) {
                        float3 offset = new float3(x, y, z) * AO_GLOBAL_SPREAD;

                        if (math.all(offset == 0f)) {
                            continue;
                        }

                        float3 vec = math.forward();

                        if (math.dot(math.normalize(offset), vec) > AO_MIN_DOT_NORMAL) {
                            tmp.Add(offset);
                        } else {
                            continue;
                        }
                    }
                }
            }

            NativeArray<float3> precomputedSamples = new NativeArray<float3>(AO_SAMPLES, Allocator.Persistent);

            Unity.Mathematics.Random rng = new Unity.Mathematics.Random(AO_SAMPLES_SEED);
            for (int i = 0; i < AO_SAMPLES; i++) {
                int srcIndex = rng.NextInt(tmp.Length);
                precomputedSamples[i] = tmp[srcIndex];
            }

            return precomputedSamples;
        }
    }
}