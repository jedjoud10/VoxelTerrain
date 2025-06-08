using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    // Common terrain utility methods
    public static class VoxelUtils {
        // number of "octal" chunks that will get their voxel values computed in the same compute shader dispatch
        public const int OCTAL_CHUNK_SIZE_RATIO = 4;
        public const int OCTAL_CHUNK_COUNT = 64;

        // "physical" size of the chunks, how big their entities are
        public const int PHYSICAL_CHUNK_SIZE = 32;

        // "logical" size of the chunks; how many voxels they store in one axis
        // technically this only needs to be 65 for skirts to work, but we also need normals to work so this must be 66
        public const int SIZE = 34;
        public const int FACE = SIZE * SIZE;
        public const int VOLUME = SIZE * SIZE * SIZE;

        // skirts will still spawn on the v=64 boundary though, we just need to add a 2 unit padding to handle literal 2D edge cases
        public const int SKIRT_SIZE = 34;
        public const int SKIRT_FACE = SKIRT_SIZE * SKIRT_SIZE;

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void DebugCheckBounds(int3 coordinates, int size) {
            if (math.cmax(coordinates) >= size || math.cmin(coordinates) < 0) {
                throw new System.OverflowException(
                    $"An element of coordinates {coordinates} is larger than the maximum {size - 1} or less than the minimum 0 (size={size})");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void DebugCheckBounds(uint3 coordinates, int size) {
            if (math.cmax(coordinates) >= size) {
                throw new System.OverflowException(
                    $"An element of coordinates {coordinates} is larger than the maximum {size - 1} (size={size})");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void DebugCheckIndex(int index, int size) {
            if (index >= (size * size * size)) {
                throw new System.OverflowException(
                    $"The given index {index} is larger then the maximum {size * size * size}");
            }

            if (index < 0) {
                throw new System.OverflowException(
                    $"The given index is negative");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void DebugCheckBounds2D(uint2 coordinates, int size) {
            if (math.cmax(coordinates) >= size) {
                throw new System.OverflowException(
                    $"An element of coordinates {coordinates} is larger than the maximum {size - 1} (size={size})");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void DebugCheckIndex2D(int index, int size) {
            if (index >= (size * size)) {
                throw new System.OverflowException(
                    $"The given index {index} is larger then the maximum {size * size}");
            }

            if (index < 0) {
                throw new System.OverflowException(
                    $"The given index is negative");
            }
        }

        // Order of increments: X, Z, Y
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint3 IndexToPos(int index, int size) {
            DebugCheckIndex(index, size);

            // N(ABC) -> N(A) x N(BC)
            int y = index / (size * size);   // x in N(A)
            int w = index % (size * size);  // w in N(BC)

            // N(BC) -> N(B) x N(C)
            int z = w / size;        // y in N(B)
            int x = w % size;        // z in N(C)
            return (uint3)new int3(x, y, z);
        }

        // Order of increments: X, Z, Y
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PosToIndex(uint3 position, int size) {
            DebugCheckBounds(position, size);
            return (int)(position.y * size * size + (position.z * size) + position.x);
        }

        // Order of increments: X, Y
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2 IndexToPos2D(int index, int size) {
            DebugCheckIndex2D(index, size);
            return new uint2((uint)(index % size), (uint)(index / size));
        }

        // Order of increments: X, Y
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PosToIndex2D(uint2 position, int size) {
            DebugCheckBounds2D(position, size);
            return (int)(position.x + position.y * size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint3 Mod(int3 val, int size) {
            int3 r = val % size;
            return (uint3)math.select(r, r + size, r < 0);
        }
    }
}