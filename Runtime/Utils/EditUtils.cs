using jedjoud.VoxelTerrain.Edits;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain {
    public static class EditUtils {
        public struct BootstrappedEditStorage<T> where T : unmanaged, IComponentData, IEdit {
            EntityQuery query;
            public BootstrappedEditStorage(ref SystemState state) {
                query = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<TerrainEditBounds, TerrainEdit, T>());
            }

            public void Update(ref SystemState state, TerrainEdits backing) {
                NativeArray<T> edits = query.ToComponentDataArray<T>(Allocator.Temp);
                NativeArray<Entity> editEntities = query.ToEntityArray(Allocator.Temp);
                NativeArray<MinMaxAABB> aabbs = query.ToComponentDataArray<TerrainEditBounds>(Allocator.Temp).Reinterpret<MinMaxAABB>();


                // modify the editted terrain voxels with the new terrain edits
                NativeList<JobHandle> deps = new NativeList<JobHandle>(Allocator.Temp);

                // set of edit entities that we have applied and that we can destroy
                NativeList<Entity> appliedEditEntities = new NativeList<Entity>(Allocator.Temp);

                foreach (var editChunkPosition in backing.modifiedChunkEditPositions) {
                    MinMaxAABB editChunkAabb = new MinMaxAABB(editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE, editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE + VoxelUtils.PHYSICAL_CHUNK_SIZE);

                    if (backing.chunkPositionsToChunkEditIndices.TryGetValue(editChunkPosition, out int index)) {
                        VoxelData editVoxels = backing.chunkEdits[index];
                        int3 chunkOffset = editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE;

                        // apply each edit for this *tick* in sequence
                        JobHandle sequence = default;
                        for (int j = 0; j < edits.Length; j++) {
                            // only apply the edits that affect this edit chunk
                            MinMaxAABB editBound = aabbs[j];
                            if (editBound.Overlaps(editChunkAabb)) {
                                var job = new EditStoreJob<T> {
                                    chunkOffset = chunkOffset,
                                    edit = edits[j],
                                    voxels = editVoxels,
                                };

                                sequence = job.Schedule(VoxelUtils.VOLUME, BatchUtils.EIGHTH_BATCH, sequence);

                                // keep track of the underlying entity since we need to destroy it 
                                Entity editEntity = editEntities[j];
                                if (!appliedEditEntities.Contains(editEntity))
                                    appliedEditEntities.Add(editEntity);
                            }
                        }

                        deps.Add(sequence);
                    } else {
                        // we will be safe here and forget about this edit so that we can possibly modify it later on when we load the chunk 
                    }
                }

                JobHandle.CompleteAll(deps.AsArray());

                state.EntityManager.DestroyEntity(appliedEditEntities.AsArray());
            }
        }

        public const float BOUNDS_EXPAND_OFFSET = 2f;

        public static void CreateEditEntity<T>(EntityManager mgr, T edit) where T : unmanaged, IComponentData, IEdit {
            Entity entity = mgr.CreateEntity();
            mgr.AddComponent<TerrainEdit>(entity);
            mgr.AddComponent<TerrainEditBounds>(entity);
            mgr.AddComponent<T>(entity);

            MinMaxAABB bounds = edit.GetBounds();
            bounds.Expand(BOUNDS_EXPAND_OFFSET);

            mgr.SetComponentData<TerrainEditBounds>(entity, new TerrainEditBounds() { bounds = bounds });
            mgr.SetComponentData<T>(entity, edit);
        }

        public static void CreateEditEntity<T>(EntityCommandBuffer ecb, T edit) where T : unmanaged, IComponentData, IEdit {
            Entity entity = ecb.CreateEntity();

            MinMaxAABB bounds = edit.GetBounds();
            bounds.Expand(BOUNDS_EXPAND_OFFSET);

            ecb.AddComponent<TerrainEdit>(entity);
            ecb.AddComponent<TerrainEditBounds>(entity, new TerrainEditBounds() { bounds = bounds });
            ecb.AddComponent<T>(entity, edit);
        }
    }
}