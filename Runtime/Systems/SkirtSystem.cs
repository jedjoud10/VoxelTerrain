using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup), OrderLast = true)]
    public partial struct SkirtSystem : ISystem {

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainSkirt, LocalToWorld, MaterialMeshInfo>().Build();
            state.RequireForUpdate(query);
            state.RequireForUpdate<TerrainLoader>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            Entity entity = SystemAPI.GetSingletonEntity<TerrainLoader>();
            TerrainLoader loader = SystemAPI.GetComponent<TerrainLoader>(entity);
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(entity);
            float3 loaderCenter = transform.Position;
            float chunkSize = VoxelUtils.PHYSICAL_CHUNK_SIZE;

            foreach (var (localToWorld, skirt, skirtEntity) in SystemAPI.Query<LocalToWorld, TerrainSkirt>().WithPresent<MaterialMeshInfo>().WithAll<TerrainSkirtVisibleTag>().WithEntityAccess()) {
                float3 skirtCenter = localToWorld.Position + localToWorld.Value.c0.w * chunkSize * 0.5f;
                float3 skirtDirection = DirectionOffsetUtils.FORWARD_DIRECTION_INCLUDING_NEGATIVE[(int)skirt.direction];

                float3 skirtCenterToPlayer = math.normalize(loaderCenter - skirtCenter);

                float dot = math.dot(skirtCenterToPlayer, skirtDirection);

                bool enabled = dot > 0f;

                SystemAPI.SetComponentEnabled<MaterialMeshInfo>(skirtEntity, enabled);
            }

            foreach (var (_, skirtEntity) in SystemAPI.Query<TerrainSkirt>().WithPresent<MaterialMeshInfo>().WithDisabled<TerrainSkirtVisibleTag>().WithEntityAccess()) {
                SystemAPI.SetComponentEnabled<MaterialMeshInfo>(skirtEntity, false);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }
    }
}