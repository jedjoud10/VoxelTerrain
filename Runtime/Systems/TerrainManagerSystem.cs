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
        private Entity prototype;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainManagerConfig>();
            state.RequireForUpdate<TerrainOctree>();
            chunks = new NativeHashMap<OctreeNode, Entity>(0, Allocator.Persistent);

            EntityManager mgr = state.EntityManager;
            prototype = mgr.CreateEntity();
            mgr.AddComponent<LocalToWorld>(prototype);
            mgr.AddComponent<TerrainChunk>(prototype);

            mgr.AddComponent<TerrainChunkVoxels>(prototype);
            mgr.AddComponent<TerrainChunkRequestReadbackTag>(prototype);
            mgr.AddComponent<TerrainChunkRequestMeshingTag>(prototype);
            mgr.AddComponent<TerrainChunkRequestCollisionTag>(prototype);
            mgr.AddComponent<TerrainChunkVoxelsReadyTag>(prototype);

            mgr.SetComponentEnabled<TerrainChunkVoxels>(prototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestReadbackTag>(prototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestMeshingTag>(prototype, false);
            mgr.SetComponentEnabled<TerrainChunkRequestCollisionTag>(prototype, false);
            mgr.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(prototype, false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            TerrainManagerConfig config = SystemAPI.GetSingleton<TerrainManagerConfig>();
            RefRW<TerrainOctree> _octree = SystemAPI.GetSingletonRW<TerrainOctree>();
            ref TerrainOctree octree = ref _octree.ValueRW;

            if (!octree.pending && octree.handle.IsCompleted && octree.readyToSpawn) {
                foreach (var node in octree.removed) {
                    if (chunks.TryGetValue(node, out var entity)) {
                        TerrainChunkVoxels voxels = state.EntityManager.GetComponentData<TerrainChunkVoxels>(entity);
                        voxels.inner.Dispose();


                        state.EntityManager.DestroyEntity(entity);
                        chunks.Remove(node);
                    }
                }

                foreach (var node in octree.added) {
                    if (node.childBaseIndex != -1)
                        continue;

                    Entity chunk = state.EntityManager.Instantiate(prototype);
                    chunks.Add(node, chunk);

                    state.EntityManager.SetComponentData<TerrainChunk>(chunk, new TerrainChunk {
                        node = node,
                    });
                    state.EntityManager.SetComponentEnabled<TerrainChunkVoxels>(chunk, true);
                    state.EntityManager.SetComponentEnabled<TerrainChunkRequestReadbackTag>(chunk, true);

                    float4x4 localToWorld = float4x4.TRS((float3)node.position, quaternion.identity, (float)node.size / 64f);
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