using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain {
    public partial struct TerrainVisibilitySystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainMainCamera>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            Entity mainCamera = SystemAPI.GetSingletonEntity<TerrainMainCamera>();
            LocalToWorld worldTransform = SystemAPI.GetComponent<LocalToWorld>(mainCamera);
            float3 cameraCenter = worldTransform.Position;
            float chunkSize = VoxelUtils.PHYSICAL_CHUNK_SIZE;

            // hide occluded chunks/skirts or those that are not visible due to their deferred visibility
            foreach (var (deferredVisible, occluded, toggle) in SystemAPI.Query<EnabledRefRO<TerrainDeferredVisible>, EnabledRefRO<TerrainCurrentlyOccludedTag>, EnabledRefRW<MaterialMeshInfo>>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)) {
                toggle.ValueRW = deferredVisible.ValueRO && !occluded.ValueRO;
            }

            // if the chunks are occluded, then their skirts are occluded as well
            foreach (var (occluded, chunk) in SystemAPI.Query<EnabledRefRO<TerrainCurrentlyOccludedTag>, TerrainChunk>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)) {
                if (chunk.skirts.IsEmpty)
                    continue;

                for (int i = 0; i < 6; i++) {
                    Entity skirt = chunk.skirts[i];
                    if (SystemAPI.Exists(skirt))
                        SystemAPI.SetComponentEnabled<TerrainCurrentlyOccludedTag>(skirt, occluded.ValueRO);
                }
            }

            // custom culling for skirts, goes over what is visible up to this point
            foreach (var (localToWorld, skirt, toggle) in SystemAPI.Query<LocalToWorld, TerrainSkirt, EnabledRefRW<MaterialMeshInfo>>().WithAll<TerrainDeferredVisible>()) {
                float3 skirtCenter = localToWorld.Position + localToWorld.Value.c0.w * chunkSize * 0.5f;
                float3 skirtDirection = DirectionOffsetUtils.FORWARD_DIRECTION_INCLUDING_NEGATIVE[(int)skirt.direction];

                float3 skirtCenterToCamera = math.normalize(cameraCenter - skirtCenter);
                float centerToCameraDot = math.dot(skirtCenterToCamera, skirtDirection);
                bool frontFaceVisible = centerToCameraDot > 0f;

                toggle.ValueRW = frontFaceVisible;
            }
        }
    }
}