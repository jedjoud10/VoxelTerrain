using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Meshing {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(TerrainMeshingSystem))]
    public partial class TerrainUnregisterMeshSystem : SystemBase {
        private EntitiesGraphicsSystem graphics;

        protected override void OnCreate() {
            graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        }
        protected override void OnUpdate() {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<UnregisterMeshCleanup, TerrainChunkMeshReady>().WithAbsent<MaterialMeshInfo>().WithAbsent<LocalToWorld>().Build();

            EntityCommandBuffer buffer = new EntityCommandBuffer(Allocator.Temp);

            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            NativeArray<UnregisterMeshCleanup> cleanup = query.ToComponentDataArray<UnregisterMeshCleanup>(Allocator.Temp);

            buffer.RemoveComponent<UnregisterMeshCleanup>(entities);
            buffer.DestroyEntity(entities);

            foreach (UnregisterMeshCleanup info in cleanup) {
                graphics.UnregisterMesh(info.meshId);
            }

            buffer.Playback(EntityManager);
            buffer.Dispose();

            entities.Dispose();
            cleanup.Dispose();
        }
    }
}