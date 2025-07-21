using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Occlusion {
    public static class OcclusionUtils {
        public const int RASTERIZE_SCREEN_WIDTH = 128;
        public const int RASTERIZE_SCREEN_HEIGHT = 64;
        public const int DDA_ITERATIONS = 64;
        public const float MIN_DENSITY_THRESHOLD = -2f;
        public const float NEAR_PLANE_DEPTH_OFFSET_FACTOR = 0.1f;
        public const float UV_EXPANSION_OFFSET = 0.01f;

        public static float LinearizeDepthStandard(float depth, float2 nearFarPlanes) {
            float near = nearFarPlanes.x;
            float far = nearFarPlanes.y;
            return (2.0f * near) / (far + near - depth * (far - near));
        }
    }
}