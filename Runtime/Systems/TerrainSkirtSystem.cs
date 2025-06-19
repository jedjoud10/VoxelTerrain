using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup), OrderLast = true)]
    public partial struct TerrainSkirtSystem : ISystem {

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainSkirt, LocalToWorld, MaterialMeshInfo>().Build();
            state.RequireForUpdate(query);
            state.RequireForUpdate<TerrainMainCamera>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            Entity mainCamera = SystemAPI.GetSingletonEntity<TerrainMainCamera>();
            LocalToWorld worldTransform = SystemAPI.GetComponent<LocalToWorld>(mainCamera);
            float3 cameraCenter = worldTransform.Position;
            float3 cameraForward = worldTransform.Forward;
            float chunkSize = VoxelUtils.PHYSICAL_CHUNK_SIZE;

            foreach (var (localToWorld, skirt, toggle) in SystemAPI.Query<LocalToWorld, TerrainSkirt, EnabledRefRW<MaterialMeshInfo>>().WithPresent<MaterialMeshInfo>().WithAll<TerrainSkirtVisibleTag>()) {
                float3 skirtCenter = localToWorld.Position + localToWorld.Value.c0.w * chunkSize * 0.5f;
                float3 skirtDirection = DirectionOffsetUtils.FORWARD_DIRECTION_INCLUDING_NEGATIVE[(int)skirt.direction];

                float3 skirtCenterToCamera = math.normalize(cameraCenter - skirtCenter);
                float centerToCameraDot = math.dot(skirtCenterToCamera, skirtDirection);
                bool frontFaceVisible = centerToCameraDot > 0f;

                /*
                float skirtNormalToCameraForwardDot = math.dot(skirtDirection, cameraForward);
                bool visibleByCamera = skirtNormalToCameraForwardDot < 0f;
                */

                toggle.ValueRW = frontFaceVisible;
            }

            foreach (var toggle in SystemAPI.Query<EnabledRefRW<MaterialMeshInfo>>().WithPresent<MaterialMeshInfo>().WithDisabled<TerrainSkirtVisibleTag>()) {
                toggle.ValueRW = false;
            }
        }
    }
}