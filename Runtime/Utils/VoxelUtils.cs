using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    // Common terrain utility methods
    public static class VoxelUtils {
        // Current chunk resolution
        public const int SIZE = 64;

        // Total number of voxels in a chunk
        public const int VOLUME = SIZE * SIZE * SIZE;

        // One more voxel just in case... :3
        public const int VOLUME_BIG = (SIZE+1) * (SIZE + 1) * (SIZE + 1);

        // Max possible number of materials supported by the terrain mesh
        public const int MAX_MATERIAL_COUNT = 256;

        // Offsets used for octree generation
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

        // Converts mortonated 2x2x2 index into 3x3x3 array like lookup index
        public static readonly int[] MORTON_INDEX_LOOKUP_NEIGHBOUR_THINGY_MA_JIG_A_BOB = new int[8] {
            13, // Morton 0: (0,0,0)
            14, // Morton 1: (1,0,0)
            22, // Morton 2: (0,1,0)
            23, // Morton 3: (1,1,0)
            16, // Morton 4: (0,0,1)
            17, // Morton 5: (1,0,1)
            25, // Morton 6: (0,1,1)
            26  // Morton 7: (1,1,1)
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

        // Morton -> Non-morton Look up Table for neighbour voxel fetching
        public static readonly int[] MortonNeighbourLookup = new int[] {
            0, 1, 9, 10, 3, 4, 12, 13
        };
        public const bool BLOCKY = false;

        // Convert an index to a 3D position (morton coding)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint3 IndexToPosMorton(int index) {
            return Morton.DecodeMorton32((uint)index);
        }

        // Convert a 3D position into an index (morton coding)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PosToIndexMorton(uint3 position) {
            return (int)Morton.EncodeMorton32(position);
        }

        // Convert an index to a 3D position
        // Order of increments: X, Z, Y
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint3 IndexToPos(int index, uint size) {
            uint index2 = (uint)index;

            // N(ABC) -> N(A) x N(BC)
            uint y = index2 / (size * size);   // x in N(A)
            uint w = index2 % (size * size);  // w in N(BC)

            // N(BC) -> N(B) x N(C)
            uint z = w / size;        // y in N(B)
            uint x = w % size;        // z in N(C)
            return new uint3(x, y, z);
        }

        // Convert an index to a 2D position (morton coding)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2 IndexToPosMorton2D(int index) {
            return Morton.DecodeMorton2D_32((uint)index);
        }

        // Convert a 3D position into an index
        // Order of increments: X, Z, Y
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PosToIndex(uint3 position, uint size) {
            return (int)math.round((position.y * size * size + (position.z * size) + position.x));
        }

        // Convert a 2D position into an index (morton coding)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PosToIndexMorton2D(uint2 position) {
            return (int)Morton.EncodeMorton2D_32(position);
        }

        // Fetches a neighbour's voxel given the given index (mortonated)
        // This will also return the source voxels if index < VOLUME
        // Just like FetchVoxelNeighbours, but using a given index instead. Also means that this can only fetch the positive neighbours
        public static Voxel FetchVoxelNeighbours(int index, ref NativeArray<Voxel> voxels, ref UnsafePtrList<Voxel> neighbours) {
            int mortonChunkIndex = index / VOLUME;

            // Local fetch (same thing as index < Volume)
            if (mortonChunkIndex == 0)
                return voxels[index];

            // This is where shit gets... shit...
            unsafe {
                // Convert the mortonated index to a flat-array index
                int flatChunkIndex = MORTON_INDEX_LOOKUP_NEIGHBOUR_THINGY_MA_JIG_A_BOB[mortonChunkIndex];
                Voxel* ptr = neighbours[flatChunkIndex];

                if (ptr != null) {
                    Voxel* offset = ptr + (index % VOLUME);
                    return *offset;
                } else {
                    Debug.Log("Not good");
                    return Voxel.Empty;
                }
            }
        }

        // Fetch the Voxels with neighbour data fallback, but consider ALL 26 neighbours, not just the ones in the positive axii
        // Solely used for AO, since that needs to fetch data from all the neighbours
        public static Voxel FetchVoxelNeighbours(int3 position, ref NativeArray<Voxel> voxels, ref UnsafePtrList<Voxel> neighbours) {
            // remap -1,1 to 0,2
            position += new int3(SIZE);
            int3 chunkPosition = position / SIZE;
            int chunkIndex = PosToIndex((uint3)chunkPosition, 3);
            int voxelIndex = PosToIndexMorton((uint3)Mod(position, SIZE));

            unsafe {
                Voxel* ptr = neighbours[chunkIndex];

                if (chunkIndex == 13) {
                    ptr = (Voxel*)voxels.GetUnsafeReadOnlyPtr<Voxel>();
                }

                if (ptr != null) {
                    Voxel* offset = (ptr + voxelIndex);
                    return *offset;
                } else {
                    Debug.Log("Not good");
                    //Debug.Log($"{chunkIndex}, {position}, {voxelIndex}");
                    return Voxel.Empty;
                }
            }
        }

        // Check if a 2x2x2 region starting from a specific voxel is accessible
        // Required for vertex job, corner job, quad job. Yk, meshing stuff
        // TODO: PLEASE IMPROVE PERFORMANCE THIS IS HORRID. There's definitely a smarter way to tackle this lol
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckCubicVoxelPosition(int3 position, BitField32 mask) {
            bool all = true;
            for (int i = 0; i < 8; i++) {
                all &= CheckPosition(position + (int3)IndexToPosMorton(i), mask);
            }
            return all;
        }

        // YET ANOTHER EDGE CASE BECAUSE VERTEX JOB REQUIRES ONE MORE FOR NORMAL CALCULATIONS!!!!
        // TODO: if we figure out a way to do hermite data readback we can skip this...
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckCubicVoxelPositionForNormals(int3 position, BitField32 mask) {
            bool all = true;
            for (int i = 0; i < 8; i++) {
                all &= CheckPosition(position + (int3)IndexToPosMorton(i) * 2, mask);
            }
            return all;
        }

        // Checks if the given GLOBAL position (could be negative) is valid with the given neighbours
        // Checks if it's a valid position for all 26 neighbours (including the ones in the negative direction)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckPosition(int3 position, BitField32 mask) {
            int3 temp1 = position + SIZE;
            int3 chunkPosition = temp1 / SIZE;

            int index1 = PosToIndex((uint3)chunkPosition, 3);
            
            return mask.IsSet(index1);

            /*
int bits = math.countbits(math.bitmask(new bool4(greater, false)));

// handle face chunk boundary
// either: x,y,z
// neighbour indices that we have to check:
bool face = bits == 1;

// handle diagonal stuff (only 2 set bits)
// either: xy, zy, xz
bool diagonal = bits == 2;

// handle corner stuff (3 set bits, all)
bool corner = bits == 3;
*/

            /*
            bool3 greater = position >= SIZE - 2;
            bool positive = math.all((greater & positiveMask) == greater);
            bool3 lesser = position < 0;
            bool negative = math.all((lesser & negativeMask) == lesser);
            return positive && negative;
            */

            /*
            bool3 greater = position >= SIZE - 2;
            bool3 lesser = position < 0;
            return !math.any(greater | lesser);
            */
        }

        // Custom modulo operator to discard negative numbers
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint3 Mod(int3 val, int size) {
            int3 r = val % size;
            return (uint3)math.select(r, r + size, r < 0);
        }

        // Calculate the normals at a specific position
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SampleGridNormal(uint3 position, ref NativeArray<Voxel> voxels, ref UnsafePtrList<Voxel> neighbours) {
            float baseVal = FetchVoxelNeighbours(PosToIndexMorton(position), ref voxels, ref neighbours).density;
            float xVal = FetchVoxelNeighbours(PosToIndexMorton(position + math.uint3(1, 0, 0)), ref voxels, ref neighbours).density;
            float yVal = FetchVoxelNeighbours(PosToIndexMorton(position + math.uint3(0, 1, 0)), ref voxels, ref neighbours).density;
            float zVal = FetchVoxelNeighbours(PosToIndexMorton(position + math.uint3(0, 0, 1)), ref voxels, ref neighbours).density;

            return new float3(baseVal - xVal, baseVal - yVal, baseVal - zVal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static half SampleDensityInterpolated(float3 position, ref NativeArray<Voxel> voxels, ref UnsafePtrList<Voxel> neighbours) {
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
        }
    }
}