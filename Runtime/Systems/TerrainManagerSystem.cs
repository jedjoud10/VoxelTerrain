using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainOctreeSystem))]
    public partial struct TerrainManagerSystem : ISystem {
        private Entity chunkPrototype;
        private Entity skirtPrototype;

        private NativeList<Entity> chunksToShow;
        private NativeList<Entity> chunksToDestroy;
        private int nextEndOfPipeCount;


        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainManagerConfig>();
            state.RequireForUpdate<TerrainOctree>();

            EntityManager mgr = state.EntityManager;
            chunkPrototype = mgr.CreateEntity();
            mgr.AddComponent<LocalToWorld>(chunkPrototype);
            mgr.AddComponent<TerrainChunk>(chunkPrototype);

            mgr.AddComponent<TerrainChunkVoxels>(chunkPrototype);
            
            mgr.AddComponent<TerrainChunkRequestReadbackTag>(chunkPrototype);
            mgr.AddComponent<TerrainChunkRequestMeshingTag>(chunkPrototype);
            mgr.AddComponent<TerrainChunkRequestLightingTag>(chunkPrototype);
            mgr.AddComponent<TerrainChunkRequestCollisionTag>(chunkPrototype);

            mgr.AddComponent<TerrainChunkVoxelsReadyTag>(chunkPrototype);
            mgr.AddComponent<TerrainChunkMesh>(chunkPrototype);
            mgr.AddComponent<TerrainChunkEndOfPipeTag>(chunkPrototype);
            mgr.AddComponent<OccludableTag>(chunkPrototype);
            mgr.AddComponent<TerrainDeferredVisible>(chunkPrototype);
            mgr.AddComponent<Prefab>(chunkPrototype);

            mgr.SetComponentEnabled<TerrainChunkMesh>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestCollisionTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestMeshingTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkEndOfPipeTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<OccludableTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainDeferredVisible>(chunkPrototype, false);

            // initial starting conditions: readback from GPU, then do meshing, then do collisions and lighting
            mgr.SetComponentEnabled<TerrainChunkVoxels>(chunkPrototype, true);
            mgr.SetComponentEnabled<TerrainChunkRequestReadbackTag>(chunkPrototype, true);
            mgr.SetComponentEnabled<TerrainChunkRequestMeshingTag>(chunkPrototype, true);
            mgr.SetComponentEnabled<TerrainChunkRequestCollisionTag>(chunkPrototype, true);
            mgr.SetComponentEnabled<TerrainChunkRequestLightingTag>(chunkPrototype, true);

            skirtPrototype = mgr.CreateEntity();
            mgr.AddComponent<LocalToWorld>(skirtPrototype);
            mgr.AddComponent<TerrainSkirt>(skirtPrototype);
            mgr.AddComponent<TerrainDeferredVisible>(skirtPrototype);
            mgr.AddComponent<OccludableTag>(skirtPrototype);
            mgr.AddComponent<Prefab>(skirtPrototype);
            mgr.AddComponent<TerrainSkirtLinkedParent>(skirtPrototype);

            mgr.SetComponentEnabled<OccludableTag>(skirtPrototype, false);
            mgr.SetComponentEnabled<TerrainDeferredVisible>(skirtPrototype, false);

            state.EntityManager.CreateSingleton<TerrainReadySystems>();
            state.EntityManager.CreateSingletonBuffer<TerrainUnregisterMeshBuffer>();
            state.EntityManager.CreateSingleton<TerrainShouldUpdate>(new TerrainShouldUpdate { octree = true, segments = true });

            chunksToShow = new NativeList<Entity>(Allocator.Persistent);
            chunksToDestroy = new NativeList<Entity>(Allocator.Persistent);
            nextEndOfPipeCount = -1;
        }

        public static byte CalculateEnabledSkirtMask(BitField32 inputMask) {
            byte outputMask = 0;

            for (int i = 0; i < 27; i++) {
                uint3 offset = VoxelUtils.IndexToPos(i, 3);

                if (!inputMask.IsSet(i)) {
                    byte backing = 0;

                    // Negative axii
                    BitUtils.SetBit(ref backing, 0, offset.x == 0);
                    BitUtils.SetBit(ref backing, 1, offset.y == 0);
                    BitUtils.SetBit(ref backing, 2, offset.z == 0);

                    // Positive axii
                    BitUtils.SetBit(ref backing, 3, offset.x == 2);
                    BitUtils.SetBit(ref backing, 4, offset.y == 2);
                    BitUtils.SetBit(ref backing, 5, offset.z == 2);

                    // We can't write to the outputMask directly since we want this to consider ALL skirts, not simply the ones in the last iteration of the loop
                    outputMask |= backing;
                }
            }

            return outputMask;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            if (!SystemAPI.HasSingleton<TerrainManager>()) {
                state.EntityManager.CreateSingleton<TerrainManager>(new TerrainManager {
                    chunks = new NativeHashMap<OctreeNode, Entity>(0, Allocator.Persistent),
                    skirtPrototype = skirtPrototype
                });
            }

            ref TerrainManager manager = ref SystemAPI.GetSingletonRW<TerrainManager>().ValueRW;

            ref TerrainReadySystems ready = ref SystemAPI.GetSingletonRW<TerrainReadySystems>().ValueRW;
            ready.manager = chunksToShow.IsEmpty && chunksToDestroy.IsEmpty;

            ref TerrainOctree octree = ref SystemAPI.GetSingletonRW<TerrainOctree>().ValueRW;

            if (!octree.pending && octree.handle.IsCompleted && octree.readyToSpawn) {
                nextEndOfPipeCount = SystemAPI.QueryBuilder().WithAll<TerrainChunk>().WithAbsent<Prefab>().Build().CalculateEntityCount();
                
                foreach (var node in octree.removed) {
                    if (manager.chunks.TryGetValue(node, out var entity)) {
                        chunksToDestroy.Add(entity);
                    }
                }

                foreach (var node in octree.added) {
                    if (node.childBaseIndex != -1)
                        continue;

                    Entity entity = state.EntityManager.Instantiate(chunkPrototype);
                    manager.chunks.Add(node, entity);
                    nextEndOfPipeCount++;

                    float4x4 localToWorld = float4x4.TRS((float3)node.position, quaternion.identity, (float)node.size / VoxelUtils.PHYSICAL_CHUNK_SIZE);


                    state.EntityManager.SetComponentData<TerrainChunk>(entity, new TerrainChunk {
                        node = node,
                        skirts = new FixedList64Bytes<Entity>(),
                    });


                    state.EntityManager.SetComponentData<TerrainChunkRequestReadbackTag>(entity, new TerrainChunkRequestReadbackTag {
                        skipMeshingIfEmpty = true,
                    });

                    state.EntityManager.SetComponentData<TerrainChunkRequestMeshingTag>(entity, new TerrainChunkRequestMeshingTag {
                        deferredVisibility = true,
                    });

                    state.EntityManager.SetComponentEnabled<TerrainChunkRequestCollisionTag>(entity, node.atMaxDepth);

                    state.EntityManager.SetComponentData<LocalToWorld>(entity, new LocalToWorld() { Value = localToWorld });

                    state.EntityManager.SetComponentData<TerrainChunkVoxels>(entity, new TerrainChunkVoxels {
                        data = new VoxelData(Allocator.Persistent),
                        asyncWriteJobHandle = default,
                        asyncReadJobHandle = default,
                    });

                    chunksToShow.Add(entity);
                }

                foreach (var node in octree.nodes) {
                    if (node.childBaseIndex != -1)
                        continue;

                    if (manager.chunks.TryGetValue(node, out var entity)) {
                        RefRW<TerrainChunk> _chunk = SystemAPI.GetComponentRW<TerrainChunk>(entity);
                        ref TerrainChunk chunk = ref _chunk.ValueRW;

                        BitField32 lastNeighbourMask = chunk.neighbourMask;
                        BitField32 newNeighbourMask = octree.neighbourMasks[node.index];
                        if (lastNeighbourMask.Value != newNeighbourMask.Value) {
                            SystemAPI.SetComponentEnabled<TerrainChunkRequestLightingTag>(entity, true);
                        }

                        chunk.neighbourMask = octree.neighbourMasks[node.index];
                        chunk.skirtMask = CalculateEnabledSkirtMask(chunk.neighbourMask);
                    }
                }



                octree.continuous = true;
                octree.readyToSpawn = false;
            }

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunkEndOfPipeTag>().WithAbsent<Prefab>().Build();

            if (query.CalculateEntityCount() == nextEndOfPipeCount) {
                nextEndOfPipeCount = -1;


                foreach (var entity in chunksToShow) {
                    if (SystemAPI.HasComponent<MaterialMeshInfo>(entity)) {
                        SystemAPI.SetComponentEnabled<TerrainDeferredVisible>(entity, true);
                    }
                }

                NativeList<BatchMeshID> temp = new NativeList<BatchMeshID>(Allocator.Temp);
                foreach (var entity in chunksToDestroy) {
                    TerrainChunk chunk = state.EntityManager.GetComponentData<TerrainChunk>(entity);

                    if (chunk.skirts.Length > 0) {
                        for (int skirtIndex = 0; skirtIndex < 6; skirtIndex++) {
                            Entity skirtEntity = chunk.skirts[skirtIndex];

                            if (state.EntityManager.HasComponent<MaterialMeshInfo>(skirtEntity)) {
                                MaterialMeshInfo matMeshInfo = state.EntityManager.GetComponentData<MaterialMeshInfo>(skirtEntity);
                                temp.Add(matMeshInfo.MeshID);
                            }

                            state.EntityManager.DestroyEntity(skirtEntity);
                        }
                    }

                    if (state.EntityManager.IsComponentEnabled<TerrainChunkVoxels>(entity)) {
                        TerrainChunkVoxels voxels = state.EntityManager.GetComponentData<TerrainChunkVoxels>(entity);
                        voxels.asyncWriteJobHandle.Complete();
                        voxels.asyncReadJobHandle.Complete();
                        voxels.data.Dispose();
                        state.EntityManager.SetComponentEnabled<TerrainChunkVoxels>(entity, false);
                    }

                    if (state.EntityManager.IsComponentEnabled<TerrainChunkMesh>(entity)) {
                        TerrainChunkMesh mesh = state.EntityManager.GetComponentData<TerrainChunkMesh>(entity);
                        mesh.Dispose();
                    }

                    if (state.EntityManager.HasComponent<MaterialMeshInfo>(entity)) {
                        MaterialMeshInfo matMeshInfo = state.EntityManager.GetComponentData<MaterialMeshInfo>(entity);
                        temp.Add(matMeshInfo.MeshID);
                    }

                    if (state.EntityManager.HasComponent<PhysicsCollider>(entity)) {
                        PhysicsCollider collider = state.EntityManager.GetComponentData<PhysicsCollider>(entity);
                        collider.Value.Dispose();
                    }

                    state.EntityManager.DestroyEntity(entity);
                    manager.chunks.Remove(chunk.node);
                }


                DynamicBuffer<TerrainUnregisterMeshBuffer> unregisterBuffer = SystemAPI.GetSingletonBuffer<TerrainUnregisterMeshBuffer>();

                foreach (var item in temp) {
                    unregisterBuffer.Add(new TerrainUnregisterMeshBuffer { meshId = item });
                }

                chunksToShow.Clear();
                chunksToDestroy.Clear();
            }


            {
                foreach (var (chunk, visible, entity) in SystemAPI.Query<TerrainChunk, EnabledRefRO<TerrainDeferredVisible>>().WithEntityAccess()) {
                    if (chunk.skirts.Length > 0) {
                        for (int i = 0; i < 6; i++) {
                            SystemAPI.SetComponentEnabled<TerrainDeferredVisible>(chunk.skirts[i], BitUtils.IsBitSet(chunk.skirtMask, i) && visible.ValueRO);
                        }
                    }
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            if (SystemAPI.TryGetSingleton<TerrainManager>(out var mgr)) {
                mgr.chunks.Dispose();
            }

            chunksToShow.Dispose();
            chunksToDestroy.Dispose();

            foreach (var (_, entity) in SystemAPI.Query<TerrainChunk>().WithEntityAccess()) {
                if (state.EntityManager.IsComponentEnabled<TerrainChunkVoxels>(entity)) {
                    TerrainChunkVoxels voxels = state.EntityManager.GetComponentData<TerrainChunkVoxels>(entity);
                    voxels.asyncWriteJobHandle.Complete();
                    voxels.asyncReadJobHandle.Complete();
                    voxels.data.Dispose();
                    state.EntityManager.SetComponentEnabled<TerrainChunkVoxels>(entity, false);
                }

                if (state.EntityManager.IsComponentEnabled<TerrainChunkMesh>(entity)) {
                    TerrainChunkMesh mesh = state.EntityManager.GetComponentData<TerrainChunkMesh>(entity);
                    mesh.Dispose();
                    state.EntityManager.SetComponentEnabled<TerrainChunkMesh>(entity, false);
                }

                if (state.EntityManager.HasComponent<PhysicsCollider>(entity)) {
                    PhysicsCollider collider = state.EntityManager.GetComponentData<PhysicsCollider>(entity);
                    collider.Value.Dispose();
                }
            }
        }
    }
}