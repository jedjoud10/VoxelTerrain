using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Occlusion {
    [UpdateBefore(typeof(TerrainVisibilitySystem))]
    public partial struct TerrainOcclusionSystem : ISystem {
        [BurstCompile(CompileSynchronously = true)]
        private struct RasterizeJob : IJobParallelFor {
            [ReadOnly]
            public NativeHashMap<int3, int> chunkPositionsLookup;
            public UnsafePtrList<half> chunkDensityPtrs;
            public float4x4 proj;
            public float4x4 view;
            public float4x4 invProj;
            public float4x4 invView;
            public float3 cameraPosition;
            public float2 nearFarPlanes;

            [WriteOnly]
            public NativeArray<float> screenDepth;

            public void Execute(int index) {
                screenDepth[index] = 1f;

                int x = index % OcclusionUtils.RASTERIZE_SCREEN_WIDTH;
                int y = index / OcclusionUtils.RASTERIZE_SCREEN_WIDTH;
                float2 uvs = new float2(x,y) / new float2(OcclusionUtils.RASTERIZE_SCREEN_WIDTH-1, OcclusionUtils.RASTERIZE_SCREEN_HEIGHT-1);
                float4 clip = new float4(uvs * 2f - 1f, 1f, 1f);
                float4 rayView = math.mul(invProj, clip);
                rayView /= rayView.w;
                float3 rayDir = math.normalize(math.mul(invView, new float4(rayView.xyz, 0)).xyz);

                float3 rayPos = cameraPosition + 0.5f;

                float3 invDir = math.rcp(rayDir);
                float3 dirSign = math.sign(rayDir);
                
                float3 flooredPos = math.floor(rayPos);
                float3 sideDist = flooredPos - rayPos + 0.5f + 0.5f * dirSign;

                for (int i = 0; i < OcclusionUtils.DDA_ITERATIONS; i++) {
                    int3 voxelPos = (int3)flooredPos;
                    VoxelUtils.WorldVoxelPosToChunkSpace(voxelPos, out int3 chunkPosition, out uint3 localVoxelPos);

                    if (chunkPositionsLookup.TryGetValue(chunkPosition, out int chunkIndexLookup)) {
                        int voxelIndex = VoxelUtils.PosToIndex(localVoxelPos, VoxelUtils.SIZE);
                        
                        unsafe {
                            half* ptr = chunkDensityPtrs[chunkIndexLookup];
                            half density = *(ptr + voxelIndex);

                            float3 test = (flooredPos - rayPos + 0.5f - 0.5f * dirSign) * invDir;
                            float max = math.cmax(test);
                            float3 world = rayPos + rayDir * max;

                            if (density < 0) {
                                float4 clipPos = math.mul(proj, math.mul(view, new float4(world, 1.0f)));
                                clipPos /= clipPos.w;
                                screenDepth[index] = math.saturate(OcclusionUtils.LinearizeDepthStandard(clipPos.z, nearFarPlanes));
                                break;
                            }
                        }
                    }

                    float3 reconst = sideDist * invDir;
                    float3 eqs = math.select(0f, 1f, new float3(math.cmin(reconst)) == reconst);
                    sideDist += dirSign * eqs;
                    flooredPos += dirSign * eqs;
                }
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainMainCamera>();
            state.EntityManager.AddComponent<TerrainOcclusionScreenData>(state.SystemHandle);
            state.EntityManager.SetComponentData<TerrainOcclusionScreenData>(state.SystemHandle, new TerrainOcclusionScreenData {
                rasterizedDdaDepth = new NativeArray<float>(OcclusionUtils.RASTERIZE_SCREEN_HEIGHT * OcclusionUtils.RASTERIZE_SCREEN_WIDTH, Allocator.Persistent)
            });

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            TerrainOcclusionScreenData data = state.EntityManager.GetComponentData<TerrainOcclusionScreenData>(state.SystemHandle);
            NativeArray<float> screenDepth = data.rasterizedDdaDepth;

            Entity cameraEntity = SystemAPI.GetSingletonEntity<TerrainMainCamera>();
            TerrainMainCamera camera = SystemAPI.GetComponent<TerrainMainCamera>(cameraEntity);
            float3 cameraPosition = SystemAPI.GetComponent<LocalToWorld>(cameraEntity).Position;


            NativeHashMap<int3, int> chunkPositionsLookup = new NativeHashMap<int3, int>(0, Allocator.TempJob);
            UnsafePtrList<half> chunkDensityPtrs = new UnsafePtrList<half>(0, Allocator.TempJob);

            foreach (var (chunk, _voxels) in SystemAPI.Query<TerrainChunk, RefRW<TerrainChunkVoxels>>()) {
                ref TerrainChunkVoxels voxels = ref _voxels.ValueRW;
                
                if (chunk.node.atMaxDepth) {
                    chunkPositionsLookup.Add(chunk.node.position / VoxelUtils.PHYSICAL_CHUNK_SIZE, chunkDensityPtrs.Length);

                    if (voxels.asyncWriteJobHandle != default) {
                        voxels.asyncWriteJobHandle.Complete();
                        voxels.asyncWriteJobHandle = default;
                    }
                    unsafe {
                        chunkDensityPtrs.Add(voxels.data.densities.GetUnsafeReadOnlyPtr());
                    }
                }
            }

            RasterizeJob job = new RasterizeJob() {
                proj = camera.projectionMatrix,
                view = camera.worldToCamera,
                invProj = math.inverse(camera.projectionMatrix),
                invView = math.inverse(camera.worldToCamera),
                chunkDensityPtrs = chunkDensityPtrs,
                chunkPositionsLookup = chunkPositionsLookup,
                screenDepth = screenDepth,
                nearFarPlanes = camera.nearFarPlanes,
                cameraPosition = cameraPosition
            };

            JobHandle handle = job.Schedule(OcclusionUtils.RASTERIZE_SCREEN_HEIGHT * OcclusionUtils.RASTERIZE_SCREEN_WIDTH, 4);
            handle.Complete();

            chunkPositionsLookup.Dispose();
            chunkDensityPtrs.Dispose();

            foreach (var (chunk, bounds, occluded) in SystemAPI.Query<TerrainChunk, WorldRenderBounds, EnabledRefRW<TerrainCurrentlyOccludedTag>>().WithAll<TerrainDeferredVisible>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)) {
                MinMaxAABB aabb = bounds.Value;

                float3 aabbMin = aabb.Min;
                float3 aabbMax = aabb.Max;

                NativeArray<float3> corners = new NativeArray<float3>(8, Allocator.Temp);
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
                    float4 clipPos = math.mul(camera.projectionMatrix, math.mul(camera.worldToCamera, new float4(corners[i], 1.0f)));
                    clipPos /= clipPos.w;
                    nearestClipSpaceZVal = math.min(OcclusionUtils.LinearizeDepthStandard(clipPos.z, camera.nearFarPlanes), nearestClipSpaceZVal);
                    float2 screenUV = (new float2(clipPos.x, clipPos.y) + 1.0f) * 0.5f;

                    minScreen = math.min(minScreen, screenUV);
                    maxScreen = math.max(maxScreen, screenUV);
                }

                minScreen = math.saturate(minScreen - OcclusionUtils.UV_EXPANSION_OFFSET);
                maxScreen = math.saturate(maxScreen + OcclusionUtils.UV_EXPANSION_OFFSET);
                occluded.ValueRW = IsChunkOccluded(screenDepth, minScreen, maxScreen, math.saturate(nearestClipSpaceZVal), camera.nearFarPlanes.x * OcclusionUtils.NEAR_PLANE_DEPTH_OFFSET_FACTOR);
            }
        }

        private static bool IsChunkOccluded(NativeArray<float> screenDepth, float2 minUV, float2 maxUV, float nearestClipSpaceZVal, float nearPlaneDepthOffset) {
            int2 minPixel = (int2)(minUV * new float2(OcclusionUtils.RASTERIZE_SCREEN_WIDTH - 1, OcclusionUtils.RASTERIZE_SCREEN_HEIGHT - 1));
            int2 maxPixel = (int2)(maxUV * new float2(OcclusionUtils.RASTERIZE_SCREEN_WIDTH - 1, OcclusionUtils.RASTERIZE_SCREEN_HEIGHT - 1));

            for (int y = minPixel.y; y <= maxPixel.y; y += 1) {
                for (int x = minPixel.x; x <= maxPixel.x; x += 1) {
                    int index = y * OcclusionUtils.RASTERIZE_SCREEN_WIDTH + x;
                    // 0 -> closest to the camera 
                    // 1 -> furthest from the camera
                    if ((screenDepth[index] + nearPlaneDepthOffset) > nearestClipSpaceZVal) { 
                        return false;
                    }
                }
            }
            return true;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            state.EntityManager.GetComponentData<TerrainOcclusionScreenData>(state.SystemHandle).rasterizedDdaDepth.Dispose();
        }
    }
}
