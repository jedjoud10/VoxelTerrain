using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup), OrderLast = true)]
    public partial struct TerrainSkirtSystem : ISystem {

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainSkirtTag, LocalToWorld, MaterialMeshInfo>().Build();
            state.RequireForUpdate(query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            Entity entity = SystemAPI.GetSingletonEntity<TerrainOctreeLoader>();
            TerrainOctreeLoader loader = SystemAPI.GetComponent<TerrainOctreeLoader>(entity);
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(entity);
            float3 loaderCenter = transform.Position;
            float chunkSize = 64f;

            foreach (var (localToWorld, skirt, skirtEntity) in SystemAPI.Query<LocalToWorld, TerrainSkirtTag>().WithPresent<MaterialMeshInfo>().WithAll<TerrainSkirtVisForceTag>().WithEntityAccess()) {
                bool enabled = true;

                if (skirt.direction != byte.MaxValue) {
                    int direction = (int)skirt.direction;
                    float3 skirtCenter = localToWorld.Position + localToWorld.Value.c0.w * chunkSize * 0.5f;
                    float3 skirtDirection = DirectionOffsetUtils.FORWARD_DIRECTION_INCLUDING_NEGATIVE[direction];

                    float3 skirtCenterToPlayer = math.normalize(loaderCenter - skirtCenter);
                    
                    float dot = math.dot(skirtCenterToPlayer, skirtDirection);

                    enabled = dot > 0f;
                }

                SystemAPI.SetComponentEnabled<MaterialMeshInfo>(skirtEntity, enabled);
            }

            foreach (var (_, skirtEntity) in SystemAPI.Query<TerrainSkirtTag>().WithPresent<MaterialMeshInfo>().WithDisabled<TerrainSkirtVisForceTag>().WithEntityAccess()) {
                SystemAPI.SetComponentEnabled<MaterialMeshInfo>(skirtEntity, false);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }
    }
}