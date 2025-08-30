using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static jedjoud.VoxelTerrain.BatchUtils;

namespace jedjoud.VoxelTerrain.Occlusion {
    [UpdateBefore(typeof(TerrainVisibilitySystem))]
    public partial struct TerrainOcclusionRasterizeSystem : ISystem {
        private JobHandle asyncVoxelizationJobHandle;
        private NativeHashMap<int3, int> chunkPositionsLookup;
        private UnsafePtrList<half> chunkDensityPtrs;
        private float3 cameraPositionDuringVoxelization;
        private float3 tmpThingMajig;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainMainCamera>();
            state.RequireForUpdate<TerrainOcclusionScreenData>();
            state.RequireForUpdate<TerrainOcclusionConfig>();

            chunkPositionsLookup = new NativeHashMap<int3, int>(0, Allocator.Persistent);
            chunkDensityPtrs = new UnsafePtrList<half>(0, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            TerrainOcclusionScreenData data = SystemAPI.GetSingleton<TerrainOcclusionScreenData>();
            TerrainOcclusionConfig config = SystemAPI.GetSingleton<TerrainOcclusionConfig>();
            Entity cameraEntity = SystemAPI.GetSingletonEntity<TerrainMainCamera>();
            TerrainMainCamera camera = SystemAPI.GetComponent<TerrainMainCamera>(cameraEntity);
            float3 cameraPosition = camera.position;


            if (asyncVoxelizationJobHandle.Equals(default) || asyncVoxelizationJobHandle.IsCompleted) {
                asyncVoxelizationJobHandle.Complete();
                chunkPositionsLookup.Clear();
                chunkDensityPtrs.Clear();
                data.copiedPostRelaxationBools.CopyFrom(data.postRelaxationBools);
                cameraPositionDuringVoxelization = tmpThingMajig;

                foreach (var (chunk, _voxels) in SystemAPI.Query<TerrainChunk, RefRW<TerrainChunkVoxels>>().WithAll<TerrainChunkVoxelsReadyTag>()) {
                    ref TerrainChunkVoxels voxels = ref _voxels.ValueRW;

                    if (chunk.node.atMaxDepth && voxels.asyncWriteJobHandle.IsCompleted) {
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


                tmpThingMajig = cameraPosition;
                JobHandle voxelizeHandle = new VoxelizeJob() {
                    preRelaxationBits = data.preRelaxationBits,
                    cameraPosition = cameraPosition,
                    chunkDensityPtrs = chunkDensityPtrs,
                    chunkPositionsLookup = chunkPositionsLookup,

                    size = config.size,
                    volume = config.volume,
                }.Schedule(config.volume / 32, OCCLUSION_VOXELIZE_BATCH / 32);

                JobHandle relaxHandle = new RelaxJob {
                    postRelaxationBools = data.postRelaxationBools,
                    preRelaxationBits = data.preRelaxationBits,
                    size = config.size,
                }.Schedule(config.volume, OCCLUSION_VOXELIZE_BATCH, voxelizeHandle);

                asyncVoxelizationJobHandle = relaxHandle;
            }

            JobHandle rasterizeJobHandle = new RasterizeJob() {
                proj = camera.projectionMatrix,
                view = camera.worldToCameraMatrix,
                invProj = math.inverse(camera.projectionMatrix),
                invView = math.inverse(camera.worldToCameraMatrix),
                insideSurfaceVoxels = data.copiedPostRelaxationBools,
                screenDepth = data.rasterizedDdaDepth,
                nearFarPlanes = camera.nearFarPlanes,
                cameraPosition = cameraPosition,
                cameraPositionDuringVoxelization = cameraPositionDuringVoxelization,


                size = config.size,
                height = config.height,
                width = config.width,
            }.Schedule(config.height * config.width, OCCLUSION_RASTERIZE_BATCH);
            rasterizeJobHandle.Complete();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            asyncVoxelizationJobHandle.Complete();
            chunkPositionsLookup.Dispose();
            chunkDensityPtrs.Dispose();
        }
    }
}
