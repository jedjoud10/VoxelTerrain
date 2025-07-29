using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    /*
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainOctreeSystem))]
    [UpdateBefore(typeof(TerrainManagerSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial class TerrainEditStoreSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<TerrainOctree>();
            RequireForUpdate<TerrainManager>();
            RequireForUpdate<TerrainOctreeConfig>();
            RequireForUpdate<TerrainReadySystems>();
            RequireForUpdate<TerrainEditConfig>();
            RequireForUpdate<TerrainEdit>();
            RequireForUpdate<TerrainEdits>();
        }

        protected override void OnUpdate() {
            TerrainEdits backing = SystemAPI.ManagedAPI.GetSingleton<TerrainEdits>();
            if (!backing.applySystemHandle.IsCompleted)
                return;
            backing.applySystemHandle.Complete();

            TerrainReadySystems ready = SystemAPI.GetSingleton<TerrainReadySystems>();
            if (!ready.readback || !ready.manager)
                return;

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainEditBounds, TerrainEdit>().Build();
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            NativeArray<TerrainEditBounds> bounds = query.ToComponentDataArray<TerrainEditBounds>(Allocator.TempJob);
            NativeArray<MinMaxAABB> aabbs = bounds.Reinterpret<MinMaxAABB>();

            // if we need to add voxel data for edited chunks, do add it
            AddEditChunks(backing, aabbs, out NativeArray<int3> modifiedChunkEditPositions);

            // modify the voxel data for the editted chunks
            ModifyData(backing, entities, aabbs, modifiedChunkEditPositions);

            // request to update the chunks' meshes
            UpdateChunkMeshes(modifiedChunkEditPositions);
            bounds.Dispose();
        }

        private void ModifyData(TerrainEdits backing, NativeArray<Entity> entities, NativeArray<MinMaxAABB> aabbs, NativeArray<int3> modifiedChunkEditPositions) {
            // modify the editted terrain voxels with the new terrain edits
            int modifCount = modifiedChunkEditPositions.Length;
            NativeArray<JobHandle> deps = new NativeArray<JobHandle>(modifCount, Allocator.Temp);

            // set of edit entities that we have applied and that we can destroy
            NativeHashSet<Entity> appliedEditEntities = new NativeHashSet<Entity>(0, Allocator.Temp);

            for (int i = 0; i < modifCount; i++) {
                int3 editChunkPosition = modifiedChunkEditPositions[i];
                MinMaxAABB editChunkAabb = new MinMaxAABB(editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE, editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE + VoxelUtils.PHYSICAL_CHUNK_SIZE);

                if (backing.chunkPositionsToChunkEditIndices.TryGetValue(editChunkPosition, out int index)) {
                    VoxelData editVoxels = backing.chunkEdits[index];
                    int3 chunkOffset = editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE;

                    // apply each edit for this *tick* in sequence
                    JobHandle sequence = default;
                    for (int j = 0; j < entities.Length; j++) {
                        
                        // only apply the edits that affect this edit chunk
                        if (aabbs[j].Overlaps(editChunkAabb)) {
                            Entity editEntity = entities[j];
                            sequence = backing.registry.ApplyEdit(editEntity, editVoxels, chunkOffset, sequence);
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
            EntityManager.DestroyEntity(entitiesToDestroy);
        }

        private void AddEditChunks(TerrainEdits backing, NativeArray<MinMaxAABB> aabbs, out NativeArray<int3> modifiedChunkEditPositions) {
            TerrainManager manager = SystemAPI.GetSingleton<TerrainManager>();
            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();
            
            // create edit chunks that will contain modified chunk data
            NativeHashSet<int3> intersecting = new NativeHashSet<int3>(0, Allocator.Temp);
            foreach (var bounds in aabbs) {
                int3 min = (int3)math.floor(bounds.Min / (float)VoxelUtils.PHYSICAL_CHUNK_SIZE);
                int3 max = (int3)math.floor(bounds.Max / (float)VoxelUtils.PHYSICAL_CHUNK_SIZE);

                for (int z = min.z; z <= max.z; z++) {
                    for (int y = min.y; y <= max.y; y++) {
                        for (int x = min.x; x <= max.x; x++) {
                            int3 chunkPos = new int3(x, y, z);
                            intersecting.Add(chunkPos);
                        }
                    }
                }
            }

            // detect NEW intersecting edit chunks, the ones that we have just added
            NativeList<int3> newIntersecting = new NativeList<int3>(Allocator.Temp);
            foreach (var what in intersecting) {
                if (!backing.chunkPositionsToChunkEditIndices.ContainsKey(what)) {
                    newIntersecting.Add(what);
                }
            }

            // add chunk edit backing native arrays (using LOD 0 chunks' voxel data as source)
            int newChunkEditIndirectIndex = backing.chunkEdits.Count;
            for (int i = 0; i < newIntersecting.Length; i++) {
                int3 chunkPosOnly = newIntersecting[i];
                OctreeNode node = OctreeNode.LeafLodZeroNode(chunkPosOnly, octreeConfig.maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE);

                // check if the chunk edit represents and LOD0 chunk that we can use for its source voxels
                if (manager.chunks.TryGetValue(node, out Entity chunk)) {
                    VoxelData editVoxels = new VoxelData(Allocator.Persistent);
                    TerrainChunkVoxels chunkVoxels = SystemAPI.GetComponent<TerrainChunkVoxels>(chunk);
                    editVoxels.CopyFrom(chunkVoxels.data);

                    // add a new chunk edit array to the world. this will be stored indefinitely
                    backing.chunkEdits.Add(editVoxels);
                    backing.chunkPositionsToChunkEditIndices.Add(chunkPosOnly, newChunkEditIndirectIndex);
                    newChunkEditIndirectIndex++;
                } else {
                    Debug.LogWarning("Tried accessing chunk that does not exist or non LOD0 chunk. Uh oh...");
                }
            }


            modifiedChunkEditPositions = intersecting.ToNativeArray(Allocator.Temp);
        }

        private void UpdateChunkMeshes(NativeArray<int3> modifiedChunkEditPositions) {

        }
    }
    */
}