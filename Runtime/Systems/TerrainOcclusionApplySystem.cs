using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;

namespace jedjoud.VoxelTerrain.Occlusion {
    [UpdateBefore(typeof(TerrainVisibilitySystem))]
    public partial struct TerrainOcclusionApplySystem : ISystem {

        [BurstCompile]
        public partial struct OccludeJob : IJobEntity {
            [ReadOnly]
            public TerrainMainCamera camera;
            [ReadOnly]
            public NativeArray<float> screenDepth;

            public float nearPlaneDepthOffsetFactor;
            public float uvExpansionFactor;

            public int width;
            public int height;

            void Execute(in WorldRenderBounds bounds, EnabledRefRW<OccludableTag> occluded) {
                MinMaxAABB aabb = bounds.Value;

                float3 aabbMin = aabb.Min;
                float3 aabbMax = aabb.Max;

                Span<float3> corners = stackalloc float3[8];
                corners[0] = new float3(aabbMin.x, aabbMin.y, aabbMin.z);
                corners[1] = new float3(aabbMin.x, aabbMin.y, aabbMax.z);
                corners[2] = new float3(aabbMin.x, aabbMax.y, aabbMin.z);
                corners[3] = new float3(aabbMin.x, aabbMax.y, aabbMax.z);
                corners[4] = new float3(aabbMax.x, aabbMin.y, aabbMin.z);
                corners[5] = new float3(aabbMax.x, aabbMin.y, aabbMax.z);
                corners[6] = new float3(aabbMax.x, aabbMax.y, aabbMin.z);
                corners[7] = new float3(aabbMax.x, aabbMax.y, aabbMax.z);

                float2 minScreen = new float2(1, 1);
                float2 maxScreen = new float2(0, 0);
                float nearestClipSpaceZVal = 1f;
                for (int i = 0; i < 8; i++) {
                    float4 clipPos = math.mul(camera.projectionMatrix, math.mul(camera.worldToCameraMatrix, new float4(corners[i], 1.0f)));
                    clipPos /= clipPos.w;

                    float2 screenUV = (new float2(clipPos.x, clipPos.y) + 1.0f) * 0.5f;
                    nearestClipSpaceZVal = math.min(OcclusionUtils.LinearizeDepthStandard(clipPos.z, camera.nearFarPlanes), nearestClipSpaceZVal);
                    minScreen = math.min(minScreen, screenUV);
                    maxScreen = math.max(maxScreen, screenUV);
                }

                minScreen = math.saturate(minScreen - uvExpansionFactor);
                maxScreen = math.saturate(maxScreen + uvExpansionFactor);
                occluded.ValueRW = IsAabbOccluded(screenDepth, minScreen, maxScreen, math.saturate(nearestClipSpaceZVal), camera.nearFarPlanes);
            }

            public bool IsAabbOccluded(NativeArray<float> screenDepth, float2 minUV, float2 maxUV, float nearestClipSpaceZVal, float2 nearFarPlanes) {
                float nearPlaneDepthOffset = nearFarPlanes.x * nearPlaneDepthOffsetFactor;

                int2 minPixel = (int2)(minUV * new float2(width - 1, height - 1));
                int2 maxPixel = (int2)(maxUV * new float2(width - 1, height - 1));

                for (int y = minPixel.y; y <= maxPixel.y; y += 1) {
                    for (int x = minPixel.x; x <= maxPixel.x; x += 1) {
                        int index = y * width + x;
                        // 0 -> closest to the camera 
                        // 1 -> furthest from the camera
                        if ((screenDepth[index] + nearPlaneDepthOffset) > nearestClipSpaceZVal) {
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainMainCamera>();
            state.RequireForUpdate<TerrainOcclusionScreenData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            TerrainOcclusionScreenData data = SystemAPI.GetSingleton<TerrainOcclusionScreenData>();
            TerrainOcclusionConfig config = SystemAPI.GetSingleton<TerrainOcclusionConfig>();
            NativeArray<float> screenDepth = data.rasterizedDdaDepth;

            Entity cameraEntity = SystemAPI.GetSingletonEntity<TerrainMainCamera>();
            TerrainMainCamera camera = SystemAPI.GetComponent<TerrainMainCamera>(cameraEntity);

            // enable or disable the ocludee state of ocludable entities
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<WorldRenderBounds, OccludableTag, RenderFilterSettings>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();
            new OccludeJob() {
                camera = camera,
                screenDepth = screenDepth,
                width = config.width,
                height = config.height,
                nearPlaneDepthOffsetFactor = config.nearPlaneDepthOffsetFactor,
                uvExpansionFactor = config.uvExpansionFactor,
            }.ScheduleParallel(query);
        }
    }
}
