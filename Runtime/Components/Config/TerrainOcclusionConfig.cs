using Unity.Entities;

namespace jedjoud.VoxelTerrain.Occlusion {
    public struct TerrainOcclusionConfig : IComponentData {
        public int width;
        public int height;
        public int size;
        public int volume;
        public float nearPlaneDepthOffsetFactor;
        public float uvExpansionFactor;
    }
}