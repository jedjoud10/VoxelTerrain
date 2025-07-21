using Unity.Collections;
using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public struct TerrainOcclusionScreenData : IComponentData {
        public NativeArray<float> rasterizedDdaDepth;
    }
}