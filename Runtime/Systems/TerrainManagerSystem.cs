using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(TerrainOctreeSystem))]
    public partial struct TerrainManagerSystem : ISystem {
        private NativeHashMap<OctreeNode, Entity> chunks;
        private Entity chunkPrototype;
        private Entity skirtPrototype;


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

            mgr.SetComponentEnabled<TerrainChunkVoxels>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestReadbackTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestMeshingTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestCollisionTag>(chunkPrototype, false);
            mgr.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(chunkPrototype, false);

            skirtPrototype = mgr.CreateEntity();
            mgr.AddComponent<LocalToWorld>(skirtPrototype);
            mgr.AddComponent<TerrainSkirtTag>(skirtPrototype);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            TerrainManagerConfig config = SystemAPI.GetSingleton<TerrainManagerConfig>();
            RefRW<TerrainOctree> _octree = SystemAPI.GetSingletonRW<TerrainOctree>();
            ref TerrainOctree octree = ref _octree.ValueRW;

            if (!octree.pending && octree.handle.IsCompleted && octree.readyToSpawn) {
                foreach (var node in octree.removed) {
                    if (chunks.TryGetValue(node, out var entity)) {
                        TerrainChunk chunk = state.EntityManager.GetComponentData<TerrainChunk>(entity);

                        NativeArray<Entity> skirts = chunk.skirts.ToNativeArray(Allocator.Temp);
                        state.EntityManager.DestroyEntity(skirts);
                        skirts.Dispose();
                        

                        TerrainChunkVoxels voxels = state.EntityManager.GetComponentData<TerrainChunkVoxels>(entity);
                        voxels.inner.Dispose();


                        state.EntityManager.DestroyEntity(entity);
                        chunks.Remove(node);
                    }
                }

                foreach (var node in octree.added) {
                    if (node.childBaseIndex != -1)
                        continue;

                    Entity chunk = state.EntityManager.Instantiate(chunkPrototype);
                    chunks.Add(node, chunk);

                    FixedList64Bytes<Entity> skirts = new FixedList64Bytes<Entity>();
                    float4x4 localToWorld = float4x4.TRS((float3)node.position, quaternion.identity, (float)node.size / 64f);

                    // TODO!!!! SET THIS BACK TO 7!!!!!
                    for (int i = 0; i < 1; i++) {
                        Entity skirt = state.EntityManager.Instantiate(skirtPrototype);
                        state.EntityManager.SetComponentData<LocalToWorld>(skirt, new LocalToWorld() { Value = localToWorld });
                        skirts.Add(skirt);
                    }

                    state.EntityManager.SetComponentData<TerrainChunk>(chunk, new TerrainChunk {
                        node = node,
                        skirts = skirts
                    });
                    state.EntityManager.SetComponentEnabled<TerrainChunkVoxels>(chunk, true);
                    state.EntityManager.SetComponentEnabled<TerrainChunkRequestReadbackTag>(chunk, true);
                    state.EntityManager.SetComponentData<LocalToWorld>(chunk, new LocalToWorld() { Value = localToWorld });


                    state.EntityManager.SetComponentData<TerrainChunkVoxels>(chunk, new TerrainChunkVoxels {
                        inner = new NativeArray<Voxel>(VoxelUtils.VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory)
                    });
                }
                                
                octree.continuous = true;
                octree.readyToSpawn = false;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            chunks.Dispose();

            foreach (var (voxels, entity) in SystemAPI.Query<TerrainChunkVoxels>().WithEntityAccess()) {
                if (voxels.inner.IsCreated) {
                    voxels.inner.Dispose();
                }
            }
        }
    }
}