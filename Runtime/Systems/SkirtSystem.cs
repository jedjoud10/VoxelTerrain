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
            state.RequireForUpdate<TerrainMainCamera>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            Entity mainCamera = SystemAPI.GetSingletonEntity<TerrainMainCamera>();
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(mainCamera);
            float3 cameraCenter = transform.Position;
            float3 cameraForward = transform.Forward();
            float chunkSize = VoxelUtils.PHYSICAL_CHUNK_SIZE;

            foreach (var (localToWorld, skirt, skirtEntity) in SystemAPI.Query<LocalToWorld, TerrainSkirt>().WithPresent<MaterialMeshInfo>().WithAll<TerrainSkirtVisibleTag>().WithEntityAccess()) {
                float3 skirtCenter = localToWorld.Position + localToWorld.Value.c0.w * chunkSize * 0.5f;
                float3 skirtDirection = DirectionOffsetUtils.FORWARD_DIRECTION_INCLUDING_NEGATIVE[(int)skirt.direction];

                float3 skirtCenterToCamera = math.normalize(cameraCenter - skirtCenter);
                float centerToCameraDot = math.dot(skirtCenterToCamera, skirtDirection);
                bool frontFaceVisible = centerToCameraDot > 0f;

                /*
                float skirtNormalToCameraForwardDot = math.dot(skirtDirection, cameraForward);
                bool visibleByCamera = skirtNormalToCameraForwardDot < 0f;
                */

                SystemAPI.SetComponentEnabled<MaterialMeshInfo>(skirtEntity, frontFaceVisible);
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