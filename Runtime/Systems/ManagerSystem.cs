using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(OctreeSystem))]
    public partial struct ManagerSystem : ISystem {
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
            mgr.AddComponent<TerrainChunkRequestCollisionTag>(chunkPrototype);
            mgr.AddComponent<TerrainChunkVoxelsReadyTag>(chunkPrototype);
            mgr.AddComponent<TerrainChunkMesh>(chunkPrototype);
            mgr.AddComponent<TerrainChunkEndOfPipeTag>(chunkPrototype);
            mgr.AddComponent<Prefab>(chunkPrototype);

            mgr.SetComponentEnabled<TerrainChunkMesh>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestCollisionTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestMeshingTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkEndOfPipeTag>(chunkPrototype, false);

            // initial starting conditions: readback from GPU
            mgr.SetComponentEnabled<TerrainChunkVoxels>(chunkPrototype, true);
            mgr.SetComponentEnabled<TerrainChunkRequestReadbackTag>(chunkPrototype, true);
            
            skirtPrototype = mgr.CreateEntity();
            mgr.AddComponent<LocalToWorld>(skirtPrototype);
            mgr.AddComponent<TerrainSkirt>(skirtPrototype);
            mgr.AddComponent<TerrainSkirtVisibleTag>(skirtPrototype);
            mgr.AddComponent<Prefab>(skirtPrototype);

            mgr.SetComponentEnabled<TerrainSkirtVisibleTag>(skirtPrototype, false);

            state.EntityManager.CreateSingleton<TerrainReadySystems>();
            state.EntityManager.CreateSingletonBuffer<TerrainUnregisterMeshBuffer>();

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
                });
            }

            ref TerrainManager manager = ref SystemAPI.GetSingletonRW<TerrainManager>().ValueRW;

            ref TerrainReadySystems ready = ref SystemAPI.GetSingletonRW<TerrainReadySystems>().ValueRW;
            ready.manager = chunksToShow.IsEmpty && chunksToDestroy.IsEmpty;

            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();
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

                    FixedList64Bytes<Entity> skirts = new FixedList64Bytes<Entity>();
                    float4x4 localToWorld = float4x4.TRS((float3)node.position, quaternion.identity, (float)node.size / VoxelUtils.PHYSICAL_CHUNK_SIZE);

                    for (int i = 0; i < 6; i++) {
                        Entity skirt = state.EntityManager.Instantiate(skirtPrototype);
                        state.EntityManager.SetComponentData<TerrainSkirt>(skirt, new TerrainSkirt() { direction = (byte)i });
                        state.EntityManager.SetComponentData<LocalToWorld>(skirt, new LocalToWorld() { Value = localToWorld });
                        skirts.Add(skirt);
                    }

                    state.EntityManager.SetComponentData<TerrainChunk>(entity, new TerrainChunk {
                        node = node,
                        skirts = skirts,
                        generateCollisions = node.depth == octreeConfig.maxDepth,
                    });

                    state.EntityManager.SetComponentData<LocalToWorld>(entity, new LocalToWorld() { Value = localToWorld });

                    state.EntityManager.SetComponentData<TerrainChunkVoxels>(entity, new TerrainChunkVoxels {
                        inner = new NativeArray<Voxel>(VoxelUtils.VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                        asyncWriteJob = default,
                        asyncReadJob = default,
                    });

                    chunksToShow.Add(entity);
                }

                foreach (var node in octree.nodes) {
                    if (node.childBaseIndex != -1)
                        continue;

                    if (manager.chunks.TryGetValue(node, out var entity)) {
                        RefRW<TerrainChunk> _chunk = SystemAPI.GetComponentRW<TerrainChunk>(entity);
                        ref TerrainChunk chunk = ref _chunk.ValueRW;
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
                        SystemAPI.SetComponentEnabled<MaterialMeshInfo>(entity, true);
                    }
                }

                NativeList<TerrainUnregisterMeshBuffer> temp = new NativeList<TerrainUnregisterMeshBuffer>(Allocator.Temp);
                foreach (var entity in chunksToDestroy) {
                    TerrainChunk chunk = state.EntityManager.GetComponentData<TerrainChunk>(entity);

                    NativeArray<Entity> skirts = chunk.skirts.ToNativeArray(Allocator.Temp);
                    state.EntityManager.DestroyEntity(skirts);
                    skirts.Dispose();

                    if (state.EntityManager.IsComponentEnabled<TerrainChunkVoxels>(entity)) {
                        TerrainChunkVoxels voxels = state.EntityManager.GetComponentData<TerrainChunkVoxels>(entity);
                        voxels.asyncWriteJob.Complete();
                        voxels.asyncReadJob.Complete();
                        voxels.inner.Dispose();
                    }

                    if (state.EntityManager.IsComponentEnabled<TerrainChunkMesh>(entity)) {
                        TerrainChunkMesh mesh = state.EntityManager.GetComponentData<TerrainChunkMesh>(entity);
                        mesh.vertices.Dispose();
                        mesh.indices.Dispose();
                        temp.Add(new TerrainUnregisterMeshBuffer { meshId = mesh.meshId });
                    }

                    if (state.EntityManager.HasComponent<PhysicsCollider>(entity)) {
                        PhysicsCollider collider = state.EntityManager.GetComponentData<PhysicsCollider>(entity);
                        collider.Value.Dispose();
                    }

                    state.EntityManager.DestroyEntity(entity);
                    manager.chunks.Remove(chunk.node);
                }


                DynamicBuffer<TerrainUnregisterMeshBuffer> unregisterBuffer = SystemAPI.GetSingletonBuffer<TerrainUnregisterMeshBuffer>();
                unregisterBuffer.AddRange(temp.AsArray());

                {
                    foreach (var (chunk, entity) in SystemAPI.Query<TerrainChunk>().WithEntityAccess()) {
                        for (int i = 0; i < 6; i++) {
                            SystemAPI.SetComponentEnabled<TerrainSkirtVisibleTag>(chunk.skirts[i], BitUtils.IsBitSet(chunk.skirtMask, i));
                        }
                    }
                }

                chunksToShow.Clear();
                chunksToDestroy.Clear();
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            SystemAPI.GetSingleton<TerrainManager>().chunks.Dispose();
            chunksToShow.Dispose();
            chunksToDestroy.Dispose();

            NativeList<TerrainUnregisterMeshBuffer> temp = new NativeList<TerrainUnregisterMeshBuffer>(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<TerrainChunk>().WithEntityAccess()) {
                if (state.EntityManager.IsComponentEnabled<TerrainChunkVoxels>(entity)) {
                    TerrainChunkVoxels voxels = state.EntityManager.GetComponentData<TerrainChunkVoxels>(entity);
                    voxels.asyncWriteJob.Complete();
                    voxels.asyncReadJob.Complete();
                    voxels.inner.Dispose();
                }

                if (state.EntityManager.IsComponentEnabled<TerrainChunkMesh>(entity)) {
                    TerrainChunkMesh mesh = state.EntityManager.GetComponentData<TerrainChunkMesh>(entity);
                    mesh.vertices.Dispose();
                    mesh.indices.Dispose();
                    temp.Add(new TerrainUnregisterMeshBuffer { meshId = mesh.meshId });
                }

                if (state.EntityManager.HasComponent<PhysicsCollider>(entity)) {
                    PhysicsCollider collider = state.EntityManager.GetComponentData<PhysicsCollider>(entity);
                    collider.Value.Dispose();
                }
            }

            DynamicBuffer<TerrainUnregisterMeshBuffer> unregisterBuffer = SystemAPI.GetSingletonBuffer<TerrainUnregisterMeshBuffer>();
            unregisterBuffer.AddRange(temp.AsArray());
        }
    }
}