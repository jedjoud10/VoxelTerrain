using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Occlusion {
    [UpdateBefore(typeof(TerrainOcclusionRasterizeSystem))]
    [UpdateBefore(typeof(TerrainOcclusionApplySystem))]
    public partial struct TerrainOcclusionManagerSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.EntityManager.CreateSingleton<TerrainOcclusionScreenData>(new TerrainOcclusionScreenData {
                rasterizedDdaDepth = new NativeArray<float>(OcclusionUtils.HEIGHT * OcclusionUtils.WIDTH, Allocator.Persistent),
                insideSurfaceVoxels = new NativeArray<bool>(OcclusionUtils.SIZE * OcclusionUtils.SIZE * OcclusionUtils.SIZE, Allocator.Persistent),
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            TerrainOcclusionScreenData data = SystemAPI.GetSingleton<TerrainOcclusionScreenData>();
            data.rasterizedDdaDepth.Dispose();
            data.insideSurfaceVoxels.Dispose();
        }
    }
}
