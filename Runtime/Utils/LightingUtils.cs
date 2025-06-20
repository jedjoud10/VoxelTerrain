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
            public UnsafePtrList<half> densityPtrs;
        }

        public static bool TryCalculateLightingForChunkEntity(EntityManager entityManager, Entity chunkEntity, out UmmmData output) {
            output = default;


            if (TryCheckShouldCalculateLighting(entityManager, chunkEntity, out NativeArray<Entity> chunks)) {
                output.neighbourMask = entityManager.GetComponentData<TerrainChunk>(chunkEntity).neighbourMask;

                unsafe {
                    output.densityPtrs = new UnsafePtrList<half>(27, Allocator.Persistent);

                    for (int j = 0; j < 27; j++) {
                        output.densityPtrs.Add(IntPtr.Zero);
                    }

                    for (int j = 0; j < 27; j++) {
                        if (entityManager.Exists(chunks[j])) {
                            TerrainChunkVoxels voxels = entityManager.GetComponentData<TerrainChunkVoxels>(chunks[j]);

                            // TODO: remove this; add it as a scheduling dep instead
                            voxels.asyncWriteJobHandle.Complete();

                            output.densityPtrs[j] = (half*)voxels.data.densities.GetUnsafeReadOnlyPtr();
                        } else {
                            output.densityPtrs[j] = (half*)IntPtr.Zero;
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
    }
}