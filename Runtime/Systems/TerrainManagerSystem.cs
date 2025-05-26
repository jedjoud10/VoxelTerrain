using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(TerrainOctreeSystem))]
    public partial struct TerrainManagerSystem : ISystem {
        private NativeHashMap<OctreeNode, Entity> chunks;
        private Entity chunkPrototype;
        private Entity skirtPrototype;

        private NativeList<Entity> chunksToShow;
        private NativeList<Entity> chunksToDestroy;
        private int nextEndOfPipeCounts;


        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainManagerConfig>();
            state.RequireForUpdate<TerrainOctree>();
            chunks = new NativeHashMap<OctreeNode, Entity>(0, Allocator.Persistent);

            EntityManager mgr = state.EntityManager;
            chunkPrototype = mgr.CreateEntity();
            mgr.AddComponent<LocalToWorld>(chunkPrototype);
            mgr.AddComponent<TerrainChunk>(chunkPrototype);

            mgr.AddComponent<TerrainChunkVoxels>(chunkPrototype);
            mgr.AddComponent<TerrainChunkRequestReadbackTag>(chunkPrototype);
            mgr.AddComponent<TerrainChunkRequestMeshingTag>(chunkPrototype);
            mgr.AddComponent<TerrainChunkRequestCollisionTag>(chunkPrototype);
            mgr.AddComponent<TerrainChunkVoxelsReadyTag>(chunkPrototype);
            mgr.AddComponent<TerrainChunkMeshReady>(chunkPrototype);
            mgr.AddComponent<TerrainChunkEndOfPipeTag>(chunkPrototype);
            mgr.AddComponent<Prefab>(chunkPrototype);


            mgr.SetComponentEnabled<TerrainChunkVoxels>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestReadbackTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestMeshingTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestCollisionTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkEndOfPipeTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkMeshReady>(chunkPrototype, false);

            skirtPrototype = mgr.CreateEntity();
            mgr.AddComponent<LocalToWorld>(skirtPrototype);
            mgr.AddComponent<TerrainSkirtTag>(skirtPrototype);
            mgr.AddComponent<TerrainSkirtVisForceTag>(skirtPrototype);
            mgr.AddComponent<Prefab>(skirtPrototype);

            state.EntityManager.CreateSingleton<TerrainReadySystems>();

            chunksToShow = new NativeList<Entity>(Allocator.Persistent);
            chunksToDestroy = new NativeList<Entity>(Allocator.Persistent);
            nextEndOfPipeCounts = -1;
        }

        static void WriteOffsetDirAsMask(ref BitField32 skirtMask, uint3 offset) {
            BitField32 intern = new BitField32(0);

            // Negative axii
            intern.SetBits(0, offset.x == 0);
            intern.SetBits(1, offset.y == 0);
            intern.SetBits(2, offset.z == 0);

            // Positive axii
            intern.SetBits(3, offset.x == 2);
            intern.SetBits(4, offset.y == 2);
            intern.SetBits(5, offset.z == 2);

            skirtMask.Value |= intern.Value;
        }

        public static BitField32 CalculateEnabledSkirtMask(BitField32 inputMask) {
            BitField32 skirtMask = new BitField32(0);

            for (int i = 0; i < 27; i++) {
                uint3 offset = VoxelUtils.IndexToPos(i, 3);

                if (!inputMask.IsSet(i)) {
                    WriteOffsetDirAsMask(ref skirtMask, offset);
                }
            }

            return skirtMask;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            RefRW<TerrainReadySystems> _ready = SystemAPI.GetSingletonRW<TerrainReadySystems>();
            _ready.ValueRW.manager = chunksToShow.IsEmpty && chunksToDestroy.IsEmpty;

            RefRW<TerrainManagerConfig> managerConfig = SystemAPI.GetSingletonRW<TerrainManagerConfig>();
            managerConfig.ValueRW.chunkPrototype = chunkPrototype;
            managerConfig.ValueRW.skirtPrototype = skirtPrototype;


            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();
            RefRW<TerrainOctree> _octree = SystemAPI.GetSingletonRW<TerrainOctree>();
            ref TerrainOctree octree = ref _octree.ValueRW;


            if (!octree.pending && octree.handle.IsCompleted && octree.readyToSpawn) {
                nextEndOfPipeCounts = SystemAPI.QueryBuilder().WithAll<TerrainChunk>().WithAbsent<Prefab>().Build().CalculateEntityCount();

                foreach (var node in octree.removed) {
                    if (chunks.TryGetValue(node, out var entity)) {
                        chunksToDestroy.Add(entity);
                    }
                }

                foreach (var node in octree.added) {
                    if (node.childBaseIndex != -1)
                        continue;

                    Entity entity = state.EntityManager.Instantiate(chunkPrototype);
                    chunks.Add(node, entity);
                    nextEndOfPipeCounts++;

                    FixedList64Bytes<Entity> skirts = new FixedList64Bytes<Entity>();
                    float4x4 localToWorld = float4x4.TRS((float3)node.position, quaternion.identity, (float)node.size / 64f);

                    for (int i = 0; i < 6; i++) {
                        Entity skirt = state.EntityManager.Instantiate(skirtPrototype);
                        state.EntityManager.SetComponentData<TerrainSkirtTag>(skirt, new TerrainSkirtTag() { direction = (byte)i });
                        state.EntityManager.SetComponentData<LocalToWorld>(skirt, new LocalToWorld() { Value = localToWorld });
                        skirts.Add(skirt);
                    }

                    state.EntityManager.SetComponentData<TerrainChunk>(entity, new TerrainChunk {
                        node = node,
                        skirts = skirts,
                        generateCollisions = node.depth == octreeConfig.maxDepth,
                    });
                    state.EntityManager.SetComponentEnabled<TerrainChunkVoxels>(entity, true);
                    state.EntityManager.SetComponentEnabled<TerrainChunkRequestReadbackTag>(entity, true);
                    state.EntityManager.SetComponentData<LocalToWorld>(entity, new LocalToWorld() { Value = localToWorld });


                    state.EntityManager.SetComponentData<TerrainChunkVoxels>(entity, new TerrainChunkVoxels {
                        inner = new NativeArray<Voxel>(VoxelUtils.VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory)
                    });

                    chunksToShow.Add(entity);
                }

                foreach (var node in octree.nodes) {
                    if (node.childBaseIndex != -1)
                        continue;

                    if (chunks.TryGetValue(node, out var entity)) {
                        RefRW<TerrainChunk> _chunk = SystemAPI.GetComponentRW<TerrainChunk>(entity);
                        ref TerrainChunk chunk = ref _chunk.ValueRW;
                        BitField32 neighbourMask = octree.neighbourMasks[node.index];
                        BitField32 skirtMask = CalculateEnabledSkirtMask(neighbourMask);

                        for (int i = 0; i < 6; i++) {
                            SystemAPI.SetComponentEnabled<TerrainSkirtVisForceTag>(chunk.skirts[i], skirtMask.IsSet(i));
                        }
                    }
                }



                octree.continuous = true;
                octree.readyToSpawn = false;
            }

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunkEndOfPipeTag>().WithAbsent<Prefab>().Build();

            if (query.CalculateEntityCount() == nextEndOfPipeCounts) {
                nextEndOfPipeCounts = -1;
                chunksToShow.Clear();

                foreach (var entity in chunksToDestroy) {
                    TerrainChunk chunk = state.EntityManager.GetComponentData<TerrainChunk>(entity);

                    NativeArray<Entity> skirts = chunk.skirts.ToNativeArray(Allocator.Temp);
                    state.EntityManager.DestroyEntity(skirts);
                    skirts.Dispose();

                    TerrainChunkVoxels voxels = state.EntityManager.GetComponentData<TerrainChunkVoxels>(entity);
                    voxels.inner.Dispose();

                    if (state.EntityManager.HasChunkComponent<TerrainChunkMeshReady>(entity)) {
                        TerrainChunkMeshReady mesh = state.EntityManager.GetComponentData<TerrainChunkMeshReady>(entity);
                        mesh.vertices.Dispose();
                        mesh.indices.Dispose();
                    }

                    state.EntityManager.DestroyEntity(entity);
                    chunks.Remove(chunk.node);
                }

                chunksToDestroy.Clear();
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            chunks.Dispose();

            chunksToShow.Dispose();
            chunksToDestroy.Dispose();

            foreach (var voxels in SystemAPI.Query<TerrainChunkVoxels>()) {
                if (voxels.inner.IsCreated) {
                    voxels.inner.Dispose();
                }
            }

            foreach (var mesh in SystemAPI.Query<TerrainChunkMeshReady>()) {
                if (mesh.vertices.IsCreated) {
                    mesh.vertices.Dispose();
                    mesh.indices.Dispose();
                }
            }
        }
    }
}