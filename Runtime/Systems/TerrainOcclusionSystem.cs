using Codice.CM.Common;
using jedjoud.VoxelTerrain.Meshing;
using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain {
    [UpdateBefore(typeof(TerrainVisibilitySystem))]
    public partial struct TerrainOcclusionSystem : ISystem {
        public const int RASTERIZE_SCREEN_WIDTH = 128;
        public const int RASTERIZE_SCREEN_HEIGHT = 64;
        const int ITERATIONS = 64;
        
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

            [WriteOnly]
            public NativeArray<float> screenDepth;

            public void Execute(int index) {
                screenDepth[index] = 0f;

                int x = index % RASTERIZE_SCREEN_WIDTH;
                int y = index / RASTERIZE_SCREEN_WIDTH;
                float2 uvs = new float2(x,y) / new float2(RASTERIZE_SCREEN_WIDTH,RASTERIZE_SCREEN_HEIGHT);
                float4 clip = new float4(uvs * 2f - 1f, 1f, 1f);
                float4 rayView = math.mul(invProj, clip);
                rayView /= rayView.w;
                float3 direction = math.normalize(math.mul(invView, new float4(rayView.xyz, 0)).xyz);

                float3 rayPos = cameraPosition;

                float3 invDir = 1 / direction;
                float3 dirSign = math.sign(direction);
                
                float3 flooredPos = math.floor(rayPos);
                float3 sideDist = flooredPos - rayPos + 0.5f + 0.5f * dirSign;

                for (int i = 0; i < ITERATIONS; i++) {
                    int3 voxelPos = (int3)flooredPos;
                    VoxelUtils.WorldVoxelPosToChunkSpace(voxelPos, out int3 chunkPosition, out uint3 localVoxelPos);

                    if (chunkPositionsLookup.TryGetValue(chunkPosition, out int chunkIndexLookup)) {
                        int voxelIndex = VoxelUtils.PosToIndex(localVoxelPos, VoxelUtils.SIZE);
                        
                        unsafe {
                            half* ptr = chunkDensityPtrs[chunkIndexLookup];
                            half density = *(ptr + voxelIndex);

                            if (density < -2f) {


                                screenDepth[index] = 1f;
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
                rasterizedDdaDepth = new NativeArray<float>(RASTERIZE_SCREEN_HEIGHT * RASTERIZE_SCREEN_WIDTH, Allocator.Persistent)
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

            foreach (var (chunk, voxels) in SystemAPI.Query<TerrainChunk, TerrainChunkVoxels>().WithAll<TerrainDeferredVisible>()) {
                if (chunk.node.atMaxDepth) {
                    chunkPositionsLookup.Add(chunk.node.position / VoxelUtils.PHYSICAL_CHUNK_SIZE, chunkDensityPtrs.Length);
                    voxels.asyncWriteJobHandle.Complete();
                    voxels.asyncReadJobHandle.Complete();
                    unsafe {
                        chunkDensityPtrs.Add(voxels.data.densities.GetUnsafeReadOnlyPtr());
                    }
                }
            }

            RasterizeJob job = new RasterizeJob() {
                view = camera.worldToCamera,
                proj = camera.projectionMatrix,
                invView = math.inverse(camera.worldToCamera),
                invProj = math.inverse(camera.projectionMatrix),
                chunkDensityPtrs = chunkDensityPtrs,
                chunkPositionsLookup = chunkPositionsLookup,
                screenDepth = screenDepth,
                cameraPosition = cameraPosition
            };

            JobHandle handle = job.Schedule(RASTERIZE_SCREEN_HEIGHT * RASTERIZE_SCREEN_WIDTH, 4);
            handle.Complete();

            chunkPositionsLookup.Dispose();
            chunkDensityPtrs.Dispose();

            foreach (var (chunk, bounds, occluded) in SystemAPI.Query<TerrainChunk, RenderBounds, EnabledRefRW<TerrainCurrentlyOccludedTag>>().WithAll<TerrainDeferredVisible>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)) {
                MinMaxAABB aabb = bounds.Value;

                // Given chunk AABB (min/max in world space)
                float3 aabbMin = aabb.Min;
                float3 aabbMax = aabb.Max;

                // Get all 8 corners of the AABB
                float3[] corners = new float3[8] {
                    new float3(aabbMin.x, aabbMin.y, aabbMin.z),
                    new float3(aabbMin.x, aabbMin.y, aabbMax.z),
                    new float3(aabbMin.x, aabbMax.y, aabbMin.z),
                    new float3(aabbMin.x, aabbMax.y, aabbMax.z),
                    new float3(aabbMax.x, aabbMin.y, aabbMin.z),
                    new float3(aabbMax.x, aabbMin.y, aabbMax.z),
                    new float3(aabbMax.x, aabbMax.y, aabbMin.z),
                    new float3(aabbMax.x, aabbMax.y, aabbMax.z)
                };

                // Project to screen space (UV coords [0,1])
                float2 minScreen = new float2(1, 1);
                float2 maxScreen = new float2(0, 0);
                for (int i = 0; i < 8; i++) {
                    float4 clipPos = math.mul(camera.projectionMatrix, math.mul(camera.worldToCamera, new float4(corners[i], 1.0f)));
                    clipPos /= clipPos.w; // Perspective divide
                    float2 screenUV = (new float2(clipPos.x, clipPos.y) + 1.0f) * 0.5f;

                    minScreen = math.min(minScreen, screenUV);
                    maxScreen = math.max(maxScreen, screenUV);
                }

                occluded.ValueRW = IsChunkOccluded(screenDepth, minScreen, maxScreen);
            }
        }

        private static bool IsChunkOccluded(NativeArray<float> screenDepth, float2 minUV, float2 maxUV) {
            // Convert UVs to pixel coords
            int2 minPixel = new int2(
                (int)(minUV.x * RASTERIZE_SCREEN_WIDTH),
                (int)(minUV.y * RASTERIZE_SCREEN_HEIGHT)
            );
            int2 maxPixel = new int2(
                (int)(maxUV.x * RASTERIZE_SCREEN_WIDTH),
                (int)(maxUV.y * RASTERIZE_SCREEN_HEIGHT)
            );
            minPixel.x = math.clamp(minPixel.x, 0, RASTERIZE_SCREEN_WIDTH);
            maxPixel.x = math.clamp(maxPixel.x, 0, RASTERIZE_SCREEN_WIDTH);
            minPixel.y = math.clamp(minPixel.y, 0, RASTERIZE_SCREEN_HEIGHT);
            maxPixel.y = math.clamp(maxPixel.y, 0, RASTERIZE_SCREEN_HEIGHT);

            // Check every Nth pixel (for performance)
            const int step = 4; // Adjust based on performance needs
            for (int y = minPixel.y; y <= maxPixel.y; y += step) {
                for (int x = minPixel.x; x <= maxPixel.x; x += step) {
                    int index = y * RASTERIZE_SCREEN_WIDTH + x;
                    if (screenDepth[index] < 0.99f) { // Not fully occluded
                        return false;
                    }
                }
            }
            return true; // All sampled pixels were occluded
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            state.EntityManager.GetComponentData<TerrainOcclusionScreenData>(state.SystemHandle).rasterizedDdaDepth.Dispose();
        }
    }
}
