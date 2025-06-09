using System;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Meshing;
using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(ReadbackSystem))]
    [UpdateBefore(typeof(MeshingSystem))]
    public partial struct EditApplySystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainOctree>();
            state.RequireForUpdate<TerrainManager>();
            state.RequireForUpdate<TerrainOctreeConfig>();
            state.RequireForUpdate<TerrainReadySystems>();
            state.RequireForUpdate<TerrainEditConfig>();
            state.RequireForUpdate<TerrainEdits>();

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunkVoxels, TerrainChunk, TerrainChunkRequestMeshingTag, TerrainChunkVoxelsReadyTag>().Build();
            state.RequireForUpdate(query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunkVoxels, TerrainChunk, TerrainChunkRequestMeshingTag, TerrainChunkVoxelsReadyTag>().Build();

            TerrainEdits backing = SystemAPI.GetSingleton<TerrainEdits>();
            ref NativeHashMap<int3, int> chunkPositionsToChunkEditIndices = ref backing.chunkPositionsToChunkEditIndices;
            ref UnsafeList<NativeArray<Voxel>> chunkEdits = ref backing.chunkEdits;
            NativeArray<int3> editChunkPositions = chunkPositionsToChunkEditIndices.GetKeyArray(Allocator.Temp);


            NativeArray<TerrainChunk> chunks = query.ToComponentDataArray<TerrainChunk>(Allocator.Temp);
            NativeArray<Entity> chunkEntities = query.ToEntityArray(Allocator.Temp);
            NativeList<Entity> modifiedChunkEntities = new NativeList<Entity>(Allocator.Temp);

            // loop over all the chunks that are going to be meshed and check if we need to inject the custom edit data into them
            // stupid double for loop, should work tho
            for (int i = 0; i < chunks.Length; i++) {
                MinMaxAABB chunkBounds = chunks[i].node.Bounds;

                foreach (var editChunkPosition in editChunkPositions) {
                    float3 min = editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE;
                    float3 max = editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE;
                    MinMaxAABB editChunkBounds = new MinMaxAABB(min, max);

                    if (chunkBounds.Overlaps(editChunkBounds))
                        modifiedChunkEntities.Add(chunkEntities[i]);
                }
            }

            UnsafePtrList<Voxel> chunkEditsRaw = new UnsafePtrList<Voxel>(chunkEdits.Length, Allocator.TempJob);
            unsafe {
                foreach (var what in chunkEdits) {
                    chunkEditsRaw.Add(what.GetUnsafeReadOnlyPtr());
                }
            }

            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();
            foreach (var entity in modifiedChunkEntities) {
                OctreeNode node = SystemAPI.GetComponent<TerrainChunk>(entity).node;

                Debug.LogWarning($"Apply edit to chunk... {entity}");
                NativeArray<Voxel> voxels = SystemAPI.GetComponent<TerrainChunkVoxels>(entity).inner;
                //dstVoxels.CopyFrom(srcVoxels);

                EditApplyJob job = new EditApplyJob {
                    chunkPositionsToChunkEditIndices = chunkPositionsToChunkEditIndices,
                    chunkEditsRaw = chunkEditsRaw,

                    chunkScale = node.size / VoxelUtils.PHYSICAL_CHUNK_SIZE,
                    chunkOffset = node.position,

                    voxels = voxels
                };

                job.Schedule(VoxelUtils.VOLUME, BatchUtils.REALLY_SMALLEST_BATCH).Complete();
            }

            chunkEditsRaw.Dispose();
        }
    }
}