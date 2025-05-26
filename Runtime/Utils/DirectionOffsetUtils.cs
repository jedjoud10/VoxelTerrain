using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    // https://discussions.unity.com/t/bc1361-when-total-static-readonly-array-length-is-greater-than-180/874045/2
    public static class DirectionOffsetUtils {
        // Forward direction of each quad
        public static readonly uint3[] FORWARD_DIRECTION = new uint3[] {
            new uint3(1, 0, 0),
            new uint3(0, 1, 0),
            new uint3(0, 0, 1),
        };

        // Forward direction of each quad
        public static readonly int3[] FORWARD_DIRECTION_INCLUDING_NEGATIVE = new int3[] {
            new int3(-1, 0, 0),
            new int3(0, -1, 0),
            new int3(0, 0, -1),
            new int3(1, 0, 0),
            new int3(0, 1, 0),
            new int3(0, 0, 1),
        };

        // Quad vertices offsets based on direction
        public static readonly uint3[] PERPENDICULAR_OFFSETS = new uint3[] {
            new uint3(0, 0, 0),
            new uint3(0, 1, 0),
            new uint3(0, 1, 1),
            new uint3(0, 0, 1),

            new uint3(0, 0, 0),
            new uint3(0, 0, 1),
            new uint3(1, 0, 1),
            new uint3(1, 0, 0),

            new uint3(0, 0, 0),
            new uint3(1, 0, 0),
            new uint3(1, 1, 0),
            new uint3(0, 1, 0)
        };
    }
}