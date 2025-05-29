using jedjoud.VoxelTerrain.Segments;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public static class SegmentUtils {
        // each segment is 256m wide in one axis
        public const int PHYSICAL_SEGMENT_SIZE = 256;

        // how many density values a segment theoretically stores in one axis
        public const int SEGMENT_SIZE = 32; 

        public static float3 GetWorldPosition(TerrainSegment segment) {
            return segment.position * PHYSICAL_SEGMENT_SIZE;
        }
    }
}