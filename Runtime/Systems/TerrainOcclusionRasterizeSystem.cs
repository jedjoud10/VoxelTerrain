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
    public partial struct TerrainOcclusionRasterizeSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainMainCamera>();
            state.RequireForUpdate<TerrainOcclusionScreenData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            TerrainOcclusionScreenData data = SystemAPI.GetSingleton<TerrainOcclusionScreenData>();
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

            JobHandle handle = job.Schedule(OcclusionUtils.HEIGHT * OcclusionUtils.WIDTH, 4);
            handle.Complete();

            chunkPositionsLookup.Dispose();
            chunkDensityPtrs.Dispose();
        }
    }
}
