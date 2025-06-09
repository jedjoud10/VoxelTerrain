using System;
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
    [UpdateAfter(typeof(OctreeSystem))]
    [UpdateBefore(typeof(ManagerSystem))]
    public partial struct EditStoreSystem : ISystem {
        const int MAX_EDITS_PER_TICK = 4;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainOctree>();
            state.RequireForUpdate<TerrainManager>();
            state.RequireForUpdate<TerrainOctreeConfig>();
            state.RequireForUpdate<TerrainReadySystems>();
            state.RequireForUpdate<TerrainEditConfig>();
            state.RequireForUpdate<TerrainEdits>();

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainEdit>().Build();
            state.RequireForUpdate(query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            state.CompleteDependency();
            state.EntityManager.CompleteDependencyBeforeRO<TerrainOctree>();

            TerrainEdits backing = SystemAPI.GetSingleton<TerrainEdits>();

            if (!backing.applySystemHandle.IsCompleted)
                return;
            
            backing.applySystemHandle.Complete();

            ref NativeHashMap<int3, int> chunkPositionsToChunkEditIndices = ref backing.chunkPositionsToChunkEditIndices;
            ref UnsafeList<NativeArray<Voxel>> chunkEdits = ref backing.chunkEdits;

            TerrainReadySystems ready = SystemAPI.GetSingleton<TerrainReadySystems>();
            TerrainManager manager = SystemAPI.GetSingleton<TerrainManager>();
            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainEdit>().Build();

            if (!ready.readback || !ready.manager)
                return;

            int numEdits = math.min(query.CalculateEntityCount(), MAX_EDITS_PER_TICK);

            NativeArray<MinMaxAABB> aabbs = new NativeArray<MinMaxAABB>(numEdits, Allocator.TempJob);
            NativeArray<TerrainEdit> edits = query.ToComponentDataArray<TerrainEdit>(Allocator.Temp).GetSubArray(0, numEdits);
            NativeArray<Entity> editEntities = query.ToEntityArray(Allocator.Temp).GetSubArray(0, numEdits);

            for (int i = 0; i < numEdits; i++) {
                aabbs[i] = MinMaxAABB.CreateFromCenterAndExtents(edits[i].center, 10);
            }

            // create edit chunks that will contain modified chunk data
            int previousCount = chunkPositionsToChunkEditIndices.Count;
            NativeHashSet<int3> modifiedChunkEditsPositionDedupped = new NativeHashSet<int3>(0, Allocator.TempJob);
            NativeList<int3> addedChunkEditPositions = new NativeList<int3>(Allocator.TempJob);
            CreateEditChunksFromBoundsJob createEditChunksJob = new CreateEditChunksFromBoundsJob {
                boundsArray = aabbs,
                chunkPositionsToChunkEditIndices = chunkPositionsToChunkEditIndices,
                addedChunkEditPositions = addedChunkEditPositions,
                modifiedChunkEditPositions = modifiedChunkEditsPositionDedupped
            };
            createEditChunksJob.Schedule().Complete();
            aabbs.Dispose();


            NativeArray<int3> modifiedChunkEditsPosition = modifiedChunkEditsPositionDedupped.ToNativeArray(Allocator.Temp);
            modifiedChunkEditsPositionDedupped.Dispose();

            // add chunk edit backing native arrays (using LOD 0 chunks' voxel data as source)
            int addedCount = chunkPositionsToChunkEditIndices.Count - previousCount;
            for (int i = 0; i < addedCount; i++) {
                int3 addedEditChunkPosition = addedChunkEditPositions[i];
                //Debug.Log($"Creating edit chunk {addedEditChunkPosition}");
                NativeArray<Voxel> editVoxels = new NativeArray<Voxel>(VoxelUtils.VOLUME, Allocator.Persistent);
                OctreeNode node = OctreeNode.LeafLodZeroNode(addedEditChunkPosition, octreeConfig.maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE);

                if (manager.chunks.TryGetValue(node, out Entity chunk)) {
                    //Debug.Log($"Fetch voxel data of chunk... {chunk}");
                    TerrainChunkVoxels tmp = SystemAPI.GetComponent<TerrainChunkVoxels>(chunk);
                    NativeArray<Voxel> chunkVoxels = tmp.inner;
                    editVoxels.CopyFrom(chunkVoxels);
                } else {
                    editVoxels.AsSpan().Fill(Voxel.Empty);
                    Debug.LogWarning("Tried accessing chunk that does not exist or non LOD0 chunk. Uh oh...");
                }

                chunkEdits.Add(editVoxels);
            }

            addedChunkEditPositions.Dispose();

            // modify the editted terrain voxels with the new terrain edits
            NativeArray<JobHandle> deps = new NativeArray<JobHandle>(modifiedChunkEditsPosition.Length, Allocator.Temp);
            
            for (int i = 0; i < modifiedChunkEditsPosition.Length; i++) {
                int3 editChunkPosition = modifiedChunkEditsPosition[i];
                NativeArray<Voxel> editVoxels = chunkEdits[chunkPositionsToChunkEditIndices[editChunkPosition]];

                // apply each edit for this *tick* in sequence
                JobHandle sequence = default;
                for (int j = 0; j < edits.Length; j++) {
                    EditStoreJob editJob = new EditStoreJob {
                        center = edits[j].center,
                        chunkOffset = editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE,
                        voxels = editVoxels,
                    };

                    // like a linked list kek
                    sequence = editJob.Schedule(VoxelUtils.VOLUME, BatchUtils.REALLY_SMALLEST_BATCH, sequence);
                }

                deps[i] = sequence;

                //Debug.Log($"Modfying edit chunk {editChunkPosition}");
            }

            JobHandle.CompleteAll(deps);

            // notify the underlying chunks that contain these values
            // the EditApplySystem automatically applies the modified voxel values to the chunks, so we don't have to worry abt that
            for (int i = 0; i < modifiedChunkEditsPosition.Length; i++) {
                int3 editChunkPosition = modifiedChunkEditsPosition[i];
                OctreeNode node = OctreeNode.LeafLodZeroNode(editChunkPosition, octreeConfig.maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE);

                if (manager.chunks.TryGetValue(node, out Entity entity)) {
                    SystemAPI.SetComponentEnabled<TerrainChunkMesh>(entity, false);
                    SystemAPI.SetComponentEnabled<TerrainChunkRequestCollisionTag>(entity, false);
                    SystemAPI.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(entity, true);
                    SystemAPI.SetComponentEnabled<TerrainChunkRequestMeshingTag>(entity, true);
                    SystemAPI.SetComponentEnabled<TerrainChunkEndOfPipeTag>(entity, false);
                    SystemAPI.SetComponentEnabled<TerrainChunkVoxels>(entity, true);
                    SystemAPI.SetComponentEnabled<TerrainChunkRequestReadbackTag>(entity, false);
                    ref TerrainChunk chunk = ref SystemAPI.GetComponentRW<TerrainChunk>(entity).ValueRW;
                    chunk.deferredVisibility = false;
                }
            }

            state.EntityManager.DestroyEntity(editEntities);

            SystemAPI.SetSingleton<TerrainEdits>(backing);
        }
    }
}