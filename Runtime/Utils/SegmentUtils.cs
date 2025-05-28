using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public static class SegmentUtils {
        // each segment is 256m wide in one axis
        public const int WORLD_SEGMENT_SIZE = 256;

        // how many density values a segment theoretically stores in one axis
        public const int SEGMENT_SIZE = 64; 
    }
}