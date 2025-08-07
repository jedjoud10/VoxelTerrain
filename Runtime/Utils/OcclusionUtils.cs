using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Occlusion {
    public static class OcclusionUtils {
        public static float LinearizeDepthStandard(float depth, float2 nearFarPlanes) {
            float near = nearFarPlanes.x;
            float far = nearFarPlanes.y;
            return (2.0f * near) / (far + near - depth * (far - near));
        }
    }
}