using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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
        public partial struct MaterialMeshInfoVisibilityJob : IJobEntity {
            void Execute(EnabledRefRO<TerrainDeferredVisible> deferredVisible, EnabledRefRO<TerrainCurrentlyOccludedTag> occluded, EnabledRefRW<MaterialMeshInfo> toggle) {
                toggle.ValueRW = deferredVisible.ValueRO && !occluded.ValueRO;
            }
        }

        [BurstCompile]
        public partial struct SkirtOcclusionJob : IJobEntity {
            public float3 cameraCenter;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<TerrainCurrentlyOccludedTag> lookup;

            void Execute(Entity e, TerrainSkirtLinkedParent skirtParent, TerrainSkirt skirt, LocalToWorld localToWorld) {
                float3 skirtCenter = localToWorld.Position + localToWorld.Value.c0.w * VoxelUtils.PHYSICAL_CHUNK_SIZE * 0.5f;
                float3 skirtDirection = DirectionOffsetUtils.FORWARD_DIRECTION_INCLUDING_NEGATIVE[(int)skirt.direction];

                float3 skirtCenterToCamera = math.normalize(cameraCenter - skirtCenter);
                float centerToCameraDot = math.dot(skirtCenterToCamera, skirtDirection);
                bool frontFaceVisible = centerToCameraDot > 0f;
                bool visibleByCamera = frontFaceVisible;

                bool parentIsOccluded = lookup.IsComponentEnabled(skirtParent.chunkParent);
                bool skirtIsOccluded = parentIsOccluded || !visibleByCamera;

                lookup.SetComponentEnabled(e, skirtIsOccluded);
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            Entity mainCamera = SystemAPI.GetSingletonEntity<TerrainMainCamera>();
            LocalToWorld worldTransform = SystemAPI.GetComponent<LocalToWorld>(mainCamera);
            float3 cameraCenter = worldTransform.Position;

            // if the chunks are occluded, then their skirts are occluded as well
            // also checks if the skirt should even be visible from the camera
            EntityQuery query2 = SystemAPI.QueryBuilder().WithAll<TerrainSkirtLinkedParent, TerrainSkirt, LocalToWorld>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();
            new SkirtOcclusionJob() { lookup = SystemAPI.GetComponentLookup<TerrainCurrentlyOccludedTag>(), cameraCenter = cameraCenter }.ScheduleParallel(query2);
            
            // hide occluded chunks/skirts or those that are not visible due to their deferred visibility
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainDeferredVisible, TerrainCurrentlyOccludedTag, MaterialMeshInfo>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();
            new MaterialMeshInfoVisibilityJob().ScheduleParallel(query);
        }
    }
}