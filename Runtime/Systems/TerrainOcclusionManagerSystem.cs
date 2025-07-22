using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Occlusion {
    [UpdateBefore(typeof(TerrainOcclusionRasterizeSystem))]
    [UpdateBefore(typeof(TerrainOcclusionApplySystem))]
    public partial struct TerrainOcclusionManagerSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.EntityManager.CreateSingleton<TerrainOcclusionScreenData>(new TerrainOcclusionScreenData {
                rasterizedDdaDepth = new NativeArray<float>(OcclusionUtils.RASTERIZE_SCREEN_HEIGHT * OcclusionUtils.RASTERIZE_SCREEN_WIDTH, Allocator.Persistent)
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            SystemAPI.GetSingleton<TerrainOcclusionScreenData>().rasterizedDdaDepth.Dispose();
        }
    }
}
