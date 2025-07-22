using Unity.Collections;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Occlusion {
    public static class OcclusionUtils {
        public const int WIDTH = 64;
        public const int HEIGHT = 64;
        public const int DDA_ITERATIONS = 64;
        public const float NEAR_PLANE_DEPTH_OFFSET_FACTOR = 0.005f;
        public const float UV_EXPANSION_OFFSET = 0.02f;

        public static float LinearizeDepthStandard(float depth, float2 nearFarPlanes) {
            float near = nearFarPlanes.x;
            float far = nearFarPlanes.y;
            return (2.0f * near) / (far + near - depth * (far - near));
        }
    }
}