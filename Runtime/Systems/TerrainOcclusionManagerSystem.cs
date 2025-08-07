using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Occlusion {
    [UpdateBefore(typeof(TerrainOcclusionRasterizeSystem))]
    [UpdateBefore(typeof(TerrainOcclusionApplySystem))]
    public partial struct TerrainOcclusionManagerSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainOcclusionConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            TerrainOcclusionConfig config = SystemAPI.GetSingleton<TerrainOcclusionConfig>();
            if (!SystemAPI.HasSingleton<TerrainOcclusionScreenData>()) {
                state.EntityManager.CreateSingleton<TerrainOcclusionScreenData>(new TerrainOcclusionScreenData {
                    rasterizedDdaDepth = new NativeArray<float>(config.width * config.height, Allocator.Persistent),
                    asyncRasterizedDdaDepth = new NativeArray<float>(config.width * config.height, Allocator.Persistent),
                    preRelaxationBits = new NativeArray<uint>(config.volume / 32, Allocator.Persistent),
                    postRelaxationBools = new NativeArray<bool>(config.volume, Allocator.Persistent),
                });
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            if (SystemAPI.TryGetSingleton<TerrainOcclusionScreenData>(out TerrainOcclusionScreenData data)) {
                data.rasterizedDdaDepth.Dispose();
                data.asyncRasterizedDdaDepth.Dispose();
                data.preRelaxationBits.Dispose();
                data.postRelaxationBools.Dispose();
            }
        }
    }
}
