using Unity.Collections;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Occlusion {
    public struct TerrainOcclusionScreenData : IComponentData {
        public NativeArray<float> rasterizedDdaDepth;
        public NativeArray<uint> preRelaxationBits;
        public NativeArray<bool> postRelaxationBools;
    }
}