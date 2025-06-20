using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace jedjoud.VoxelTerrain.Meshing {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainMeshingSystem))]
    public partial struct TerrainColliderSystem : ISystem {
        struct PendingBakeRequest {
            public JobHandle dep;
            public Entity entity;
            public NativeReference<BlobAssetReference<Collider>> colliderRef;

            public void Dispose() {
                dep.Complete();
                colliderRef.Dispose();
            }
        }

        private NativeList<PendingBakeRequest> pending;

        [BurstCompile(CompileSynchronously = true)]
        struct BakingJob : IJob {
            [ReadOnly]
            public TerrainChunkMesh mesh;
            public NativeReference<BlobAssetReference<Collider>> colliderRef;

            public void Execute() {
                NativeArray<float3> vertices = mesh.vertices;
                NativeArray<int3> triangles = mesh.mainMeshIndices.Reinterpret<int3>(sizeof(int));
                var material = Material.Default;
                material.Friction = 0.95f;
                colliderRef.Value = MeshCollider.Create(vertices, triangles, CollisionFilter.Default, material);
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            pending = new NativeList<PendingBakeRequest>(Allocator.Persistent);
        }

        [BurstCompile]
        public  void OnUpdate(ref SystemState state) {
            TryCompleteOldBatches(ref state);
            TryFetchNewBatch(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            foreach (var baking in pending) {
                baking.dep.Complete();
                baking.Dispose();
            }

            pending.Dispose();
        }

        private void TryCompleteOldBatches(ref SystemState state) {
            for (int i = pending.Length - 1; i >= 0; i--) {
                if (pending[i].dep.IsCompleted) {
                    pending[i].dep.Complete();
                    Entity entity = pending[i].entity;
                    BlobAssetReference<Collider> collider = pending[i].colliderRef.Value;

                    if (state.EntityManager.HasComponent<PhysicsCollider>(entity)) {
                        state.EntityManager.GetComponentData<PhysicsCollider>(entity).Value.Dispose();
                    }

                    state.EntityManager.AddSharedComponent<PhysicsWorldIndex>(entity, new PhysicsWorldIndex { Value = 0 });
                    state.EntityManager.AddComponent<PhysicsCollider>(entity);
                    state.EntityManager.SetComponentData<PhysicsCollider>(entity, new PhysicsCollider() { Value = collider });

                    pending[i].Dispose();
                    pending.RemoveAt(i);
                }
            }
        }

        private void TryFetchNewBatch(ref SystemState state) {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunkRequestCollisionTag, TerrainChunkMesh>().Build();
            if (query.IsEmpty)
                return;

            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++) {
                NativeReference<BlobAssetReference<Collider>> colliderRef = new NativeReference<BlobAssetReference<Collider>>(Allocator.Persistent);
                ref TerrainChunkMesh mesh = ref SystemAPI.GetComponentRW<TerrainChunkMesh>(entities[i]).ValueRW;

                BakingJob bake = new BakingJob {
                    mesh = mesh,
                    colliderRef = colliderRef
                };

                JobHandle handle = bake.Schedule();
                mesh.accessJobHandle = JobHandle.CombineDependencies(mesh.accessJobHandle, handle);

                pending.Add(new PendingBakeRequest {
                    dep = handle,
                    colliderRef = colliderRef,
                    entity = entities[i],
                });
            }

            foreach (var entity in entities) {
                state.EntityManager.SetComponentEnabled<TerrainChunkRequestCollisionTag>(entity, false);
            }
        }
    }
}