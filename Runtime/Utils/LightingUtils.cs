using System;
using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    public static class LightingUtils {
        public static bool TryCheckShouldCalculateLighting(EntityManager entityManager, Entity entity, out NativeArray<Entity> entities) {
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

        public struct UmmmData {
            public BitField32 neighbourMask;
            public UnsafePtrList<half> tempDensityPtrs;
        }

        public static bool TryCalculateLightingForChunkEntity(EntityManager entityManager, Entity chunkEntity, out UmmmData output) {
            output = default;


            if (TryCheckShouldCalculateLighting(entityManager, chunkEntity, out NativeArray<Entity> chunks)) {
                output.neighbourMask = entityManager.GetComponentData<TerrainChunk>(chunkEntity).neighbourMask;

                unsafe {
                    output.tempDensityPtrs = new UnsafePtrList<half>(27, Allocator.Temp);

                    for (int j = 0; j < 27; j++) {
                        output.tempDensityPtrs.Add(IntPtr.Zero);
                    }

                    for (int j = 0; j < 27; j++) {
                        if (entityManager.Exists(chunks[j])) {
                            TerrainChunkVoxels voxels = entityManager.GetComponentData<TerrainChunkVoxels>(chunks[j]);

                            // TODO: remove this; add it as a scheduling dep instead
                            voxels.asyncWriteJobHandle.Complete();

                            output.tempDensityPtrs[j] = (half*)voxels.data.densities.GetUnsafeReadOnlyPtr();
                        } else {
                            output.tempDensityPtrs[j] = (half*)IntPtr.Zero;
                        }
                    }

                    /*
                    handler.mesh = mesh;
                    handler.meshDataArray = meshDataArray;
                    handler.Begin(chunk.neighbourMask, chunkMesh, densityPtrs);
                    */
                }

                return true;
                //entityManager.SetComponentEnabled<TerrainChunkRequestLightingTag>(chunkEntity, false);
            }

            return false;
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