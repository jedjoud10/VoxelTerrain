using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public static class EdgePositionUtils {
        // Positions of the first vertex in edges
        public static readonly uint3[] EDGE_POSITIONS_0 = new uint3[] {
            new uint3(0, 0, 0),
            new uint3(1, 0, 0),
            new uint3(1, 1, 0),
            new uint3(0, 1, 0),
            new uint3(0, 0, 1),
            new uint3(1, 0, 1),
            new uint3(1, 1, 1),
            new uint3(0, 1, 1),
            new uint3(0, 0, 0),
            new uint3(1, 0, 0),
            new uint3(1, 1, 0),
            new uint3(0, 1, 0),
        };

        // Positions of the second vertex in edges
        public static readonly uint3[] EDGE_POSITIONS_1 = new uint3[] {
            new uint3(1, 0, 0),
            new uint3(1, 1, 0),
            new uint3(0, 1, 0),
            new uint3(0, 0, 0),
            new uint3(1, 0, 1),
            new uint3(1, 1, 1),
            new uint3(0, 1, 1),
            new uint3(0, 0, 1),
            new uint3(0, 0, 1),
            new uint3(1, 0, 1),
            new uint3(1, 1, 1),
            new uint3(0, 1, 1),
        };
    }
}