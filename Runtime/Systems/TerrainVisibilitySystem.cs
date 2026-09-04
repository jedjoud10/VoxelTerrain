using jedjoud.VoxelTerrain.Meshing;
using jedjoud.VoxelTerrain.Occlusion;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    public partial struct TerrainVisibilitySystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainMainCamera>();
        }

        [BurstCompile]
        public partial struct SetOccludableToVisible : IJobEntity {
            void Execute(EnabledRefRW<OccludableTag> occluded) {
                occluded.ValueRW = false;
            }
        }

        [BurstCompile]
        public partial struct MaterialMeshInfoVisibilityJob : IJobEntity {
            void Execute(EnabledRefRO<TerrainDeferredVisible> deferredVisible, EnabledRefRW<MaterialMeshInfo> meshInfo) {
                meshInfo.ValueRW = deferredVisible.ValueRO;
            }
        }

        [BurstCompile]
        public partial struct UserMaterialMeshInfoVisibilityJob : IJobEntity {
            void Execute(EnabledRefRO<OccludableTag> occluded, EnabledRefRW<MaterialMeshInfo> toggle) {
                toggle.ValueRW = !occluded.ValueRO;
            }
        }

        [BurstCompile]
        public partial struct SkirtOcclusionJob : IJobEntity {
            public float3 cameraCenter;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<OccludableTag> lookup;

            void Execute(Entity e, in TerrainSkirtLinkedParent skirtParent, in TerrainSkirt skirt, in LocalToWorld localToWorld) {
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

            // if terrain occlusion culling is disable, set the occludable state of all entities to "not occluded"
            if (!SystemAPI.HasSingleton<TerrainOcclusionConfig>()) {
                EntityQuery resetQuery = SystemAPI.QueryBuilder().WithAll<OccludableTag>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();
                new SetOccludableToVisible() { }.ScheduleParallel(resetQuery);
            }

            // if the chunks are occluded, then their skirts are occluded as well
            // also checks if the skirt should even be visible from the camera
            EntityQuery skirtQuery = SystemAPI.QueryBuilder().WithAll<TerrainSkirtLinkedParent, TerrainSkirt, LocalToWorld>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();
            new SkirtOcclusionJob() { lookup = SystemAPI.GetComponentLookup<OccludableTag>(), cameraCenter = cameraCenter }.ScheduleParallel(skirtQuery);

            // enable/disable the materialmeshinfo of deferred visible stuff (terrain chunks & skirts)
            EntityQuery terrainDeferredVisibilityQuery = SystemAPI.QueryBuilder().WithAll<TerrainDeferredVisible, MaterialMeshInfo>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();
            new MaterialMeshInfoVisibilityJob { }.ScheduleParallel(terrainDeferredVisibilityQuery);
            
            // hide occluded entities like props and other thingies
            // this DOES break shadows but since props are small and close this shouldn't be much of a problem
            EntityQuery userEntities = SystemAPI.QueryBuilder().WithAll<OccludableTag, UserOccludableTag, MaterialMeshInfo>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();
            new UserMaterialMeshInfoVisibilityJob().ScheduleParallel(userEntities);

            // change the shadow filtering mode of the occluded / non occluded terrain thingies
            state.CompleteDependency();
            var tmp = TerrainMeshingSystem.renderMeshDescription.FilterSettings;
            EntityQuery nonOccludedTerrainThingsQuery = SystemAPI.QueryBuilder().WithAll<TerrainDeferredVisible, RenderFilterSettings>().WithDisabled<OccludableTag>().Build();
            state.EntityManager.SetSharedComponent(nonOccludedTerrainThingsQuery, tmp);
            EntityQuery occludedTerrainThingsQuery = SystemAPI.QueryBuilder().WithAll<TerrainDeferredVisible, RenderFilterSettings>().WithAll<OccludableTag>().Build();
            tmp.ShadowCastingMode = ShadowCastingMode.ShadowsOnly;
            state.EntityManager.SetSharedComponent(occludedTerrainThingsQuery, tmp);
        }
    }
}