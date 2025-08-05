using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static jedjoud.VoxelTerrain.BatchUtils;

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

            Entity cameraEntity = SystemAPI.GetSingletonEntity<TerrainMainCamera>();
            TerrainMainCamera camera = SystemAPI.GetComponent<TerrainMainCamera>(cameraEntity);
            float3 cameraPosition = SystemAPI.GetComponent<LocalToWorld>(cameraEntity).Position;


            NativeHashMap<int3, int> chunkPositionsLookup = new NativeHashMap<int3, int>(0, Allocator.TempJob);
            UnsafePtrList<half> chunkDensityPtrs = new UnsafePtrList<half>(0, Allocator.TempJob);

            foreach (var (chunk, _voxels) in SystemAPI.Query<TerrainChunk, RefRW<TerrainChunkVoxels>>()) {
                ref TerrainChunkVoxels voxels = ref _voxels.ValueRW;
                
                if (chunk.node.atMaxDepth) {
                    chunkPositionsLookup.Add(chunk.node.position / VoxelUtils.PHYSICAL_CHUNK_SIZE, chunkDensityPtrs.Length);

                    if (!voxels.asyncWriteJobHandle.Equals(default)) {
                        voxels.asyncWriteJobHandle.Complete();
                        voxels.asyncWriteJobHandle = default;
                    }
                    unsafe {
                        chunkDensityPtrs.Add(voxels.data.densities.GetUnsafeReadOnlyPtr());
                    }
                }
            }

            JobHandle voxelizeHandle = new VoxelizeJob() {
                preRelaxationBits = data.preRelaxationBits,
                cameraPosition = cameraPosition,
                chunkDensityPtrs = chunkDensityPtrs,
                chunkPositionsLookup = chunkPositionsLookup,
            }.Schedule(OcclusionUtils.VOLUME / 32, OCCLUSION_VOXELIZE_BATCH / 32);

            JobHandle relaxHandle = new RelaxJob {
                postRelaxationBools = data.postRelaxationBools,
                preRelaxationBits = data.preRelaxationBits
            }.Schedule(OcclusionUtils.VOLUME, OCCLUSION_VOXELIZE_BATCH, voxelizeHandle);

            JobHandle rasterizeHandle = new RasterizeJob() {
                proj = camera.projectionMatrix,
                view = camera.worldToCameraMatrix,
                invProj = math.inverse(camera.projectionMatrix),
                invView = math.inverse(camera.worldToCameraMatrix),
                insideSurfaceVoxels = data.postRelaxationBools,
                screenDepth = data.rasterizedDdaDepth,
                nearFarPlanes = camera.nearFarPlanes,
                cameraPosition = cameraPosition
            }.Schedule(OcclusionUtils.HEIGHT * OcclusionUtils.WIDTH, OCCLUSION_RASTERIZE_BATCH, relaxHandle);
            
            rasterizeHandle.Complete();

            chunkPositionsLookup.Dispose();
            chunkDensityPtrs.Dispose();
        }
    }
}
