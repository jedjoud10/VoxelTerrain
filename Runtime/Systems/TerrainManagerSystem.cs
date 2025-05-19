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
    [UpdateBefore(typeof(TerrainOctreeJobSystem))]
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
            mgr.AddComponent<TerrainChunkTag>(prototype);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            TerrainManagerConfig config = SystemAPI.GetSingleton<TerrainManagerConfig>();
            RefRW<TerrainOctree> _octree = SystemAPI.GetSingletonRW<TerrainOctree>();
            ref TerrainOctree octree = ref _octree.ValueRW;

            if (!octree.pending && octree.handle.IsCompleted) {
                foreach (var node in octree.removed) {
                    if (chunks.TryGetValue(node, out var entity)) {
                        state.EntityManager.DestroyEntity(entity);
                        chunks.Remove(node);
                    }
                }

                foreach (var node in octree.added) {
                    if (node.childBaseIndex != -1)
                        continue;

                    Entity chunk = state.EntityManager.Instantiate(prototype);
                    chunks.Add(node, chunk);
                }

                // UnityEngine.Debug.Log($"A:{octree.added.Length} R:{octree.removed.Length}");
                
                octree.continuous = true;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            chunks.Dispose();
        }
    }
}