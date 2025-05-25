using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainMeshingSystem))]
    public partial struct TerrainColliderSystem : ISystem {
        struct PendingBatchBakeRequest {
            public JobHandle dep;
            public NativeArray<Entity> entities;
            public NativeArray<BlobAssetReference<Collider>> colliders;
        }

        private NativeList<PendingBatchBakeRequest> batches;

        [BurstCompile(CompileSynchronously = true)]
        struct BakingJob : IJobParallelFor {
            [ReadOnly]
            public UnsafeList<TerrainChunkMeshReady> meshes;
            [WriteOnly]
            public NativeArray<BlobAssetReference<Collider>> colliders;

            public void Execute(int i) {
                NativeArray<float3> vertices = meshes[i].vertices;
                NativeArray<int3> triangles = meshes[i].indices.Reinterpret<int3>(sizeof(int));
                BlobAssetReference<Collider> collider = MeshCollider.Create(vertices, triangles, CollisionFilter.Default, Material.Default);
                colliders[i] = collider;
            }
        }


        public void OnCreate(ref SystemState state) {
            batches = new NativeList<PendingBatchBakeRequest>(Allocator.Persistent);
        }

        [BurstCompile]
        public  void OnUpdate(ref SystemState state) {
            TryCompleteOldBatches(ref state);
            TryFetchNewBatch(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            batches.Dispose();
        }

        private void TryCompleteOldBatches(ref SystemState state) {
            for (int i = batches.Length - 1; i >= 0; i--) {
                if (batches[i].dep.IsCompleted) {
                    batches[i].dep.Complete();
                    NativeArray<Entity> entities = batches[i].entities;
                    NativeArray<BlobAssetReference<Collider>> colliders = batches[i].colliders;

                    for (int e = 0; e < entities.Length; e++) {
                        state.EntityManager.AddSharedComponent<PhysicsWorldIndex>(entities[e], new PhysicsWorldIndex { Value = 0 });
                        state.EntityManager.AddComponent<PhysicsCollider>(entities[e]);
                        state.EntityManager.SetComponentData<PhysicsCollider>(entities[e], new PhysicsCollider() { Value = colliders[e] });
                    }

                    entities.Dispose();
                    colliders.Dispose();

                    batches.RemoveAt(i);
                }
            }
        }

        private void TryFetchNewBatch(ref SystemState state) {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunkRequestCollisionTag, TerrainChunkMeshReady>().Build();

            if (query.CalculateEntityCount() == 0)
                return;
            

            // we deallocate these later when we complete the jobs
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Persistent);
            NativeArray<BlobAssetReference<Collider>> colliders = new NativeArray<BlobAssetReference<Collider>>(entities.Length, Allocator.Persistent);

            // temp allocation so we don't need to dispose of it
            // (we can't anyways, since we can't dispose nested containers in jobs, even the fucking deferred dispose ones)
            NativeArray<TerrainChunkMeshReady> meshes = query.ToComponentDataArray<TerrainChunkMeshReady>(Allocator.Temp);
            UnsafeList<TerrainChunkMeshReady> what = new UnsafeList<TerrainChunkMeshReady>(entities.Length, Allocator.TempJob);
            what.Resize(entities.Length);
            what.CopyFrom(meshes);


            BakingJob bake = new BakingJob {
                meshes = what,
                colliders = colliders,
            };

            JobHandle handle = bake.Schedule(entities.Length, 1);

            what.Dispose(handle);

            batches.Add(new PendingBatchBakeRequest {
                dep = handle,
                colliders = colliders,
                entities = entities
            });

            state.EntityManager.SetComponentEnabled<TerrainChunkRequestCollisionTag>(query, false);
        }
    }
}