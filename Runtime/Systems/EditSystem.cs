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
    public partial struct EditSystem : ISystem {
        private NativeHashMap<int3, int> chunkPositionsToChunkEditIndices;
        private UnsafeList<NativeArray<Voxel>> chunkEdits;
        public bool dirty;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            chunkPositionsToChunkEditIndices = new NativeHashMap<int3, int>(0, Allocator.Persistent);
            chunkEdits = new UnsafeList<NativeArray<Voxel>>(0, Allocator.Persistent);
            state.RequireForUpdate<TerrainOctree>();
            state.RequireForUpdate<TerrainManager>();
            state.RequireForUpdate<TerrainOctreeConfig>();
            state.RequireForUpdate<TerrainReadySystems>();
            state.RequireForUpdate<TerrainEditConfig>();
            dirty = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            state.CompleteDependency();
            state.EntityManager.CompleteDependencyBeforeRO<TerrainOctree>();

            uint tick = SystemAPI.GetSingleton<TickSystem.Singleton>().tick;
            CreateEditChunks(ref state, tick);
            UpdateExistingChunksIfTheyNeedIt(ref state, tick);
        }

        private void UpdateExistingChunksIfTheyNeedIt(ref SystemState state, uint tick) {
            if (!dirty) {
                return;
            }

            TerrainOctree octree = SystemAPI.GetSingleton<TerrainOctree>();
            TerrainManager manager = SystemAPI.GetSingleton<TerrainManager>();
            TerrainReadySystems ready = SystemAPI.GetSingleton<TerrainReadySystems>();
            
            if (octree.pending || !ready.mesher || !ready.readback || !ready.manager) {
                Debug.LogWarning("not ready yet... desu nano yo");
                return;
            }

            NativeArray<int3> editChunkPositions = chunkPositionsToChunkEditIndices.GetKeyArray(Allocator.Temp);
            NativeArray<MinMaxAABB> editChunkBounds = new NativeArray<MinMaxAABB>(editChunkPositions.Length, Allocator.TempJob);
            for (int i = 0; i < editChunkPositions.Length; i++) {
                float3 min = editChunkPositions[i] * VoxelUtils.PHYSICAL_CHUNK_SIZE;
                float3 max = editChunkPositions[i] * VoxelUtils.PHYSICAL_CHUNK_SIZE;
                editChunkBounds[i] = new MinMaxAABB(min, max);
            }

            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();
            NativeList<int> intersecting = new NativeList<int>(0, Allocator.TempJob);
            RecurseBoundsIntersectJob job = new RecurseBoundsIntersectJob {
                boundsArray = editChunkBounds,
                intersecting = intersecting,
                nodes = octree.nodes,
            };
            job.Schedule().Complete();

            foreach (int intersectingNodeIndex in intersecting) {
                OctreeNode node = octree.nodes[intersectingNodeIndex];

                if (manager.chunks.TryGetValue(node, out Entity entity)) {
                    if (octreeConfig.maxDepth == node.depth) {
                        int3 chunkEditPosition = node.position / VoxelUtils.PHYSICAL_CHUNK_SIZE;

                        if (chunkPositionsToChunkEditIndices.TryGetValue(chunkEditPosition, out int chunkEditIndex)) {
                            NativeArray<Voxel> srcVoxels = chunkEdits[chunkEditIndex];

                            // modify LOD0chunk here...
                            Debug.LogWarning($"Apply edit to chunk... {entity}");
                            NativeArray<Voxel> dstVoxels = SystemAPI.GetComponent<TerrainChunkVoxels>(entity).inner;
                            dstVoxels.CopyFrom(srcVoxels);

                            SystemAPI.SetComponentEnabled<TerrainChunkMesh>(entity, false);
                            SystemAPI.SetComponentEnabled<TerrainChunkRequestCollisionTag>(entity, false);
                            SystemAPI.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(entity, true);
                            SystemAPI.SetComponentEnabled<TerrainChunkRequestMeshingTag>(entity, true);
                            SystemAPI.SetComponentEnabled<TerrainChunkEndOfPipeTag>(entity, false);
                            SystemAPI.SetComponentEnabled<TerrainChunkVoxels>(entity, true);
                            SystemAPI.SetComponentEnabled<TerrainChunkRequestReadbackTag>(entity, false);
                            ref TerrainChunk chunk = ref SystemAPI.GetComponentRW<TerrainChunk>(entity).ValueRW;
                            chunk.deferredVisibility = false;

                            Debug.Log($"Recomputing: {entity}");
                        }
                    }
                }
            }

            intersecting.Dispose();
            editChunkBounds.Dispose();
            dirty = false;
        }

        private void CreateEditChunks(ref SystemState state, uint tick) {
            TerrainReadySystems ready = SystemAPI.GetSingleton<TerrainReadySystems>();
            TerrainManager manager = SystemAPI.GetSingleton<TerrainManager>();
            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainEdit, LocalToWorld>().Build();

            if (!ready.readback || !ready.manager || query.IsEmpty) 
                return;

            NativeArray<MinMaxAABB> aabbs = new NativeArray<MinMaxAABB>(query.CalculateEntityCount(), Allocator.TempJob);
            state.EntityManager.DestroyEntity(query);

            NativeArray<LocalToWorld> transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

            for (int i = 0; i < transforms.Length; i++) {
                aabbs[i] = MinMaxAABB.CreateFromCenterAndExtents(transforms[i].Position, 10);
            }

            // create edit chunks that will contain modified chunk data
            int previousCount = chunkPositionsToChunkEditIndices.Count;
            NativeList<int3> modifiedChunkEditsPosition = new NativeList<int3>(Allocator.TempJob);
            CreateEditChunksFromBoundsJob createEditChunksJob = new CreateEditChunksFromBoundsJob {
                boundsArray = aabbs,
                chunkPositionsToChunkEditIndices = chunkPositionsToChunkEditIndices,
                modifiedChunkEditPositions = modifiedChunkEditsPosition
            };
            createEditChunksJob.Schedule().Complete();
            aabbs.Dispose();

            // add chunk edit backing native arrays (using LOD 0 chunks' voxel data as source)
            int addedCount = chunkPositionsToChunkEditIndices.Count - previousCount;
            chunkEdits.AddReplicate(default, addedCount);
            for (int i = 0; i < addedCount; i++) {
                int3 editChunkPosition = chunkPositionsToChunkEditIndices[i + previousCount];
                Debug.Log($"Creating edit chunk {editChunkPosition}");
                NativeArray<Voxel> editVoxels = new NativeArray<Voxel>(VoxelUtils.VOLUME, Allocator.Persistent);
                OctreeNode node = OctreeNode.LeafLodZeroNode(editChunkPosition, octreeConfig.maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE);

                if (manager.chunks.TryGetValue(node, out Entity chunk)) {
                    Debug.Log($"Fetch voxel data of chunk... {chunk}");
                    TerrainChunkVoxels tmp = SystemAPI.GetComponent<TerrainChunkVoxels>(chunk);
                    NativeArray<Voxel> chunkVoxels = tmp.inner;
                    editVoxels.CopyFrom(chunkVoxels);
                } else {
                    editVoxels.AsSpan().Fill(Voxel.Empty);
                    Debug.LogWarning("Tried accessing chunk that does not exist or non LOD0 chunk. Uh oh...");
                }

                chunkEdits[previousCount + i] = editVoxels;
            }
            
            // modify the editted terrain voxels with the new terrain edits
            for (int i = 0; i < modifiedChunkEditsPosition.Length; i++) {
                int3 editChunkPosition = modifiedChunkEditsPosition[i];
                NativeArray<Voxel> editVoxels = chunkEdits[chunkPositionsToChunkEditIndices[editChunkPosition]];
                TerrainEditJob editJob = new TerrainEditJob {
                    offset = editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE,
                    voxels = editVoxels,
                };

                editJob.Schedule(VoxelUtils.VOLUME, BatchUtils.REALLY_SMALLEST_BATCH).Complete();
                Debug.Log($"Modfying edit chunk {editChunkPosition}");
            }

            modifiedChunkEditsPosition.Dispose();
            dirty = true;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            chunkPositionsToChunkEditIndices.Dispose();

            foreach (var editData in chunkEdits) {
                editData.Dispose();
            }

            chunkEdits.Dispose();
        }
    }
}