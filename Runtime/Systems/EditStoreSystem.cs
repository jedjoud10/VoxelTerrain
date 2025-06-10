using jedjoud.VoxelTerrain.Octree;
using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(OctreeSystem))]
    [UpdateBefore(typeof(ManagerSystem))]
    public partial struct EditStoreSystem : ISystem {
        const int MAX_EDITS_PER_TICK = 4;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainOctree>();
            state.RequireForUpdate<TerrainManager>();
            state.RequireForUpdate<TerrainOctreeConfig>();
            state.RequireForUpdate<TerrainReadySystems>();
            state.RequireForUpdate<TerrainEditConfig>();
            state.RequireForUpdate<TerrainEdits>();

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainEditBounds>().Build();
            state.RequireForUpdate(query);
        }

        public void OnUpdate(ref SystemState state) {
            state.CompleteDependency();
            state.EntityManager.CompleteDependencyBeforeRO<TerrainOctree>();
            TerrainEdits backing = SystemAPI.GetSingleton<TerrainEdits>();

            if (!backing.applySystemHandle.IsCompleted)
                return;
            backing.applySystemHandle.Complete();

            TerrainReadySystems ready = SystemAPI.GetSingleton<TerrainReadySystems>();
            if (!ready.readback || !ready.manager)
                return;

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainEditBounds>().Build();

            // int numEdits = math.min(query.CalculateEntityCount(), MAX_EDITS_PER_TICK);
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            NativeArray<TerrainEditBounds> bounds = query.ToComponentDataArray<TerrainEditBounds>(Allocator.TempJob);
            NativeArray<MinMaxAABB> aabbs = bounds.Reinterpret<MinMaxAABB>();

            AddEditChunks(ref state, ref backing, aabbs, out NativeArray<int3> modifiedChunkEditPositions);
            Debug.Log(modifiedChunkEditPositions.Length);

            ModifyData(ref state, ref backing, entities, aabbs, modifiedChunkEditPositions);

            UpdateChunkMeshes(ref state, modifiedChunkEditPositions);

            SystemAPI.SetSingleton<TerrainEdits>(backing);
            
            bounds.Dispose();
        }

        private void ModifyData(ref SystemState state, ref TerrainEdits backing, NativeArray<Entity> entities, NativeArray<MinMaxAABB> aabbs, NativeArray<int3> modifiedChunkEditPositions) {
            // modify the editted terrain voxels with the new terrain edits
            int modifCount = modifiedChunkEditPositions.Length;
            NativeArray<JobHandle> deps = new NativeArray<JobHandle>(modifCount, Allocator.Temp);

            // set of edit entities that we have applied and that we can destroy
            NativeHashSet<Entity> appliedEditEntities = new NativeHashSet<Entity>(0, Allocator.Temp);

            for (int i = 0; i < modifCount; i++) {
                int3 editChunkPosition = modifiedChunkEditPositions[i];
                MinMaxAABB editChunkAabb = new MinMaxAABB(editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE, editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE + VoxelUtils.PHYSICAL_CHUNK_SIZE);

                if (backing.chunkPositionsToChunkEditIndices.TryGetValue(editChunkPosition, out int index)) {
                    NativeArray<Voxel> editVoxels = backing.chunkEdits[index];

                    // apply each edit for this *tick* in sequence
                    JobHandle sequence = default;
                    for (int j = 0; j < entities.Length; j++) {
                        
                        // only apply the edits that affect this edit chunk
                        if (aabbs[j].Overlaps(editChunkAabb)) {
                            EditStoreJob editJob = new EditStoreJob {
                                center = aabbs[j].Center,
                                chunkOffset = editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE,
                                voxels = editVoxels,
                            };

                            // like a linked list kek
                            sequence = editJob.Schedule(VoxelUtils.VOLUME, BatchUtils.SMALLEST_BATCH, sequence);
                            appliedEditEntities.Add(entities[j]);
                        }
                    }

                    deps[i] = sequence;
                } else {
                    // we will be safe here and forget about this edit so that we can possibly modify it later on when we load the chunk 
                }
            }

            JobHandle.CompleteAll(deps);
            NativeArray<Entity> entitiesToDestroy = appliedEditEntities.ToNativeArray(Allocator.Temp);
            state.EntityManager.DestroyEntity(entitiesToDestroy);
        }

        private void AddEditChunks(ref SystemState state, ref TerrainEdits backing, NativeArray<MinMaxAABB> aabbs, out NativeArray<int3> modifiedChunkEditPositions) {
            TerrainManager manager = SystemAPI.GetSingleton<TerrainManager>();
            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();
            
            // create edit chunks that will contain modified chunk data
            int previousCount = backing.chunkPositionsToChunkEditIndices.Count;
            NativeHashSet<int3> intersecting = new NativeHashSet<int3>(0, Allocator.TempJob);
            GetIntersectingEditChunkPositionsFromBounds createEditChunksJob = new GetIntersectingEditChunkPositionsFromBounds {
                boundsArray = aabbs,
                intersecting = intersecting,
            };
            createEditChunksJob.Schedule().Complete();

            // detect NEW intersecting edit chunks, the ones that we have just added
            NativeList<int3> newIntersecting = new NativeList<int3>(Allocator.Temp);
            foreach (var what in intersecting) {
                if (!backing.chunkPositionsToChunkEditIndices.ContainsKey(what)) {
                    newIntersecting.Add(what);
                }
            }

            // add chunk edit backing native arrays (using LOD 0 chunks' voxel data as source)
            int newChunkEditIndirectIndex = backing.chunkEdits.Length;
            Debug.Log(newIntersecting.Length);
            for (int i = 0; i < newIntersecting.Length; i++) {
                int3 chunkPosOnly = newIntersecting[i];
                OctreeNode node = OctreeNode.LeafLodZeroNode(chunkPosOnly, octreeConfig.maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE);

                // check if the chunk edit represents and LOD0 chunk that we can use for its source voxels
                if (manager.chunks.TryGetValue(node, out Entity chunk)) {
                    NativeArray<Voxel> editVoxels = new NativeArray<Voxel>(VoxelUtils.VOLUME, Allocator.Persistent);
                    TerrainChunkVoxels chunkVoxels = SystemAPI.GetComponent<TerrainChunkVoxels>(chunk);
                    editVoxels.CopyFrom(chunkVoxels.inner);

                    // add a new chunk edit array to the world. this will be stored indefinitely
                    backing.chunkEdits.Add(editVoxels);
                    backing.chunkPositionsToChunkEditIndices.Add(chunkPosOnly, newChunkEditIndirectIndex);
                    newChunkEditIndirectIndex++;
                } else {
                    Debug.LogWarning("Tried accessing chunk that does not exist or non LOD0 chunk. Uh oh...");
                }

            }


            modifiedChunkEditPositions = intersecting.ToNativeArray(Allocator.Temp);
            intersecting.Dispose();
        }

        private void UpdateChunkMeshes(ref SystemState state, NativeArray<int3> modifiedChunkEditPositions) {
            TerrainManager manager = SystemAPI.GetSingleton<TerrainManager>();
            EntityManager mgr = state.EntityManager;
            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();

            // notify the underlying chunks that contain these values
            // the EditApplySystem automatically applies the modified voxel values to the chunks, so we don't have to worry abt that
            // since we will only modify LOD0 chunks, we don't need to do any LOD related stuff either. you will ALWAYS modify the closest chunk, so they are the ones that need their mesh updated immediately
            for (int i = 0; i < modifiedChunkEditPositions.Length; i++) {
                int3 editChunkPosition = modifiedChunkEditPositions[i];
                OctreeNode node = OctreeNode.LeafLodZeroNode(editChunkPosition, octreeConfig.maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE);

                if (manager.chunks.TryGetValue(node, out Entity entity)) {
                    mgr.SetComponentEnabled<TerrainChunkMesh>(entity, false);
                    mgr.SetComponentEnabled<TerrainChunkRequestCollisionTag>(entity, false);
                    mgr.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(entity, true);
                    mgr.SetComponentEnabled<TerrainChunkRequestMeshingTag>(entity, true);
                    mgr.SetComponentEnabled<TerrainChunkEndOfPipeTag>(entity, false);
                    mgr.SetComponentEnabled<TerrainChunkVoxels>(entity, true);
                    mgr.SetComponentEnabled<TerrainChunkRequestReadbackTag>(entity, false);
                    SystemAPI.GetComponentRW<TerrainChunkRequestMeshingTag>(entity).ValueRW.deferredVisibility = false;
                }
            }
        }
    }
}