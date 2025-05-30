using jedjoud.VoxelTerrain.Segments;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public static class SegmentUtils {
        // each segment is 256m wide in one axis
        public const int PHYSICAL_SEGMENT_SIZE = 256;

        // 34 since we need 1 padding voxel to run the edge checks on the boundary and 1 more padding for finite diff normals 
        public const int SEGMENT_SIZE_PADDED = 34;
        public const int SEGMENT_SIZE = 32;
    }
}