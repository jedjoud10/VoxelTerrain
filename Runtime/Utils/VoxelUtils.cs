using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    // Common terrain utility methods
    public static class VoxelUtils {
        // Ermm.. what the sigma?
        public const bool BLOCKY = true;

        // Offsets used for octree generation
        // Also mortonated!!!
        public static readonly int3[] OCTREE_CHILD_OFFSETS = {
            new int3(0, 0, 0),
            new int3(1, 0, 0),
            new int3(0, 1, 0),
            new int3(1, 1, 0),
            new int3(0, 0, 1),
            new int3(1, 0, 1),
            new int3(0, 1, 1),
            new int3(1, 1, 1),
        };

        // First 8 index elements of a 3D morton encoded index
        // 0,0,0 => 0
        // 1,0,0 => 1
        // 0,1,0 => 2
        // 1,1,0 => 3
        // 0,0,1 => 4
        // 1,0,1 => 5
        // 0,1,1 => 6
        // 1,1,1 => 7


        // Stolen from https://gist.github.com/dwilliamson/c041e3454a713e58baf6e4f8e5fffecd
        public static readonly ushort[] EdgeMasks = new ushort[] {
            0x0, 0x109, 0x203, 0x30a, 0x80c, 0x905, 0xa0f, 0xb06,
            0x406, 0x50f, 0x605, 0x70c, 0xc0a, 0xd03, 0xe09, 0xf00,
            0x190, 0x99, 0x393, 0x29a, 0x99c, 0x895, 0xb9f, 0xa96,
            0x596, 0x49f, 0x795, 0x69c, 0xd9a, 0xc93, 0xf99, 0xe90,
            0x230, 0x339, 0x33, 0x13a, 0xa3c, 0xb35, 0x83f, 0x936,
            0x636, 0x73f, 0x435, 0x53c, 0xe3a, 0xf33, 0xc39, 0xd30,
            0x3a0, 0x2a9, 0x1a3, 0xaa, 0xbac, 0xaa5, 0x9af, 0x8a6,
            0x7a6, 0x6af, 0x5a5, 0x4ac, 0xfaa, 0xea3, 0xda9, 0xca0,
            0x8c0, 0x9c9, 0xac3, 0xbca, 0xcc, 0x1c5, 0x2cf, 0x3c6,
            0xcc6, 0xdcf, 0xec5, 0xfcc, 0x4ca, 0x5c3, 0x6c9, 0x7c0,
            0x950, 0x859, 0xb53, 0xa5a, 0x15c, 0x55, 0x35f, 0x256,
            0xd56, 0xc5f, 0xf55, 0xe5c, 0x55a, 0x453, 0x759, 0x650,
            0xaf0, 0xbf9, 0x8f3, 0x9fa, 0x2fc, 0x3f5, 0xff, 0x1f6,
            0xef6, 0xfff, 0xcf5, 0xdfc, 0x6fa, 0x7f3, 0x4f9, 0x5f0,
            0xb60, 0xa69, 0x963, 0x86a, 0x36c, 0x265, 0x16f, 0x66,
            0xf66, 0xe6f, 0xd65, 0xc6c, 0x76a, 0x663, 0x569, 0x460,
            0x460, 0x569, 0x663, 0x76a, 0xc6c, 0xd65, 0xe6f, 0xf66,
            0x66, 0x16f, 0x265, 0x36c, 0x86a, 0x963, 0xa69, 0xb60,
            0x5f0, 0x4f9, 0x7f3, 0x6fa, 0xdfc, 0xcf5, 0xfff, 0xef6,
            0x1f6, 0xff, 0x3f5, 0x2fc, 0x9fa, 0x8f3, 0xbf9, 0xaf0,
            0x650, 0x759, 0x453, 0x55a, 0xe5c, 0xf55, 0xc5f, 0xd56,
            0x256, 0x35f, 0x55, 0x15c, 0xa5a, 0xb53, 0x859, 0x950,
            0x7c0, 0x6c9, 0x5c3, 0x4ca, 0xfcc, 0xec5, 0xdcf, 0xcc6,
            0x3c6, 0x2cf, 0x1c5, 0xcc, 0xbca, 0xac3, 0x9c9, 0x8c0,
            0xca0, 0xda9, 0xea3, 0xfaa, 0x4ac, 0x5a5, 0x6af, 0x7a6,
            0x8a6, 0x9af, 0xaa5, 0xbac, 0xaa, 0x1a3, 0x2a9, 0x3a0,
            0xd30, 0xc39, 0xf33, 0xe3a, 0x53c, 0x435, 0x73f, 0x636,
            0x936, 0x83f, 0xb35, 0xa3c, 0x13a, 0x33, 0x339, 0x230,
            0xe90, 0xf99, 0xc93, 0xd9a, 0x69c, 0x795, 0x49f, 0x596,
            0xa96, 0xb9f, 0x895, 0x99c, 0x29a, 0x393, 0x99, 0x190,
            0xf00, 0xe09, 0xd03, 0xc0a, 0x70c, 0x605, 0x50f, 0x406,
            0xb06, 0xa0f, 0x905, 0x80c, 0x30a, 0x203, 0x109, 0x0,
        };

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void DebugCheckBounds(uint3 coordinates, int size) {
            if (math.cmax(coordinates) >= size) {
                throw new System.OverflowException(
                    $"An element of coordinates {coordinates} is larger then the maximum {size}");
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
                    $"An element of coordinates {coordinates} is larger then the maximum {size}");
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

        // Convert an index to a 3D position
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

        // Convert a 3D position into an index
        // Order of increments: X, Z, Y
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PosToIndex(uint3 position, int size) {
            DebugCheckBounds(position, size);
            return (int)(position.y * size * size + (position.z * size) + position.x);
        }

        // Convert an index to a 2D position
        // Order of increments: X, Y
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2 IndexToPos2D(int index, int size) {
            DebugCheckIndex2D(index, size);
            return new uint2((uint)(index % size), (uint)(index / size));
        }

        // Convert a 2D position into an index
        // Order of increments: X, Y
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PosToIndex2D(uint2 position, int size) {
            DebugCheckBounds2D(position, size);
            return (int)(position.x + position.y * size);
        }

        // Custom modulo operator to discard negative numbers
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint3 Mod(int3 val, int size) {
            int3 r = val % size;
            return (uint3)math.select(r, r + size, r < 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static half SampleDensityInterpolated(float3 position, ref NativeArray<Voxel> voxels, ref UnsafePtrList<Voxel> neighbours) {
            return (half)0f;
            /*
            float3 frac = math.frac(position);
            int3 voxPos = (int3)math.floor(position);

            float d000 = FetchVoxelNeighbours(voxPos, ref voxels, ref neighbours).density;
            float d100 = FetchVoxelNeighbours(voxPos + math.int3(1, 0, 0), ref voxels, ref neighbours).density;
            float d010 = FetchVoxelNeighbours(voxPos + math.int3(0, 1, 0), ref voxels, ref neighbours).density;
            float d110 = FetchVoxelNeighbours(voxPos + math.int3(0, 0, 1), ref voxels, ref neighbours).density;

            float d001 = FetchVoxelNeighbours(voxPos + math.int3(0, 0, 1), ref voxels, ref neighbours).density;
            float d101 = FetchVoxelNeighbours(voxPos + math.int3(1, 0, 1), ref voxels, ref neighbours).density;
            float d011 = FetchVoxelNeighbours(voxPos + math.int3(0, 1, 1), ref voxels, ref neighbours).density;
            float d111 = FetchVoxelNeighbours(voxPos + math.int3(1, 1, 1), ref voxels, ref neighbours).density;

            float mixed0 = math.lerp(d000, d100, frac.x);
            float mixed1 = math.lerp(d010, d110, frac.x);
            float mixed2 = math.lerp(d001, d101, frac.x);
            float mixed3 = math.lerp(d011, d111, frac.x);

            float mixed4 = math.lerp(mixed0, mixed2, frac.z);
            float mixed5 = math.lerp(mixed1, mixed3, frac.z);

            float mixed6 = math.lerp(mixed4, mixed5, frac.y);

            return (half)mixed6;
            */
        }
    }
}