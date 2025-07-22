using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    // Common terrain utility methods
    public static class VoxelUtils {
        // number of "multi" chunks that will get their voxel values computed in the same compute shader dispatch
        public const int MULTI_READBACK_CHUNK_SIZE_RATIO = 4;
        public const int MULTI_READBACK_CHUNK_COUNT = 64;

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


        // Fetch the Voxels with neighbour data fallback, but consider ALL 26 neighbours, not just the ones in the positive axii
        // Solely used for AO, since that needs to fetch data from all the neighbours
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static half FetchDensityNeighbours(int3 position, ref UnsafePtrList<half> voxelDataPtrs) {
            // remap -1,1 to 0,2
            position += new int3(PHYSICAL_CHUNK_SIZE);
            int3 chunkPosition = position / PHYSICAL_CHUNK_SIZE;
            int chunkIndex = PosToIndex((uint3)chunkPosition, 3);
            int voxelIndex = PosToIndex((uint3)Mod(position, PHYSICAL_CHUNK_SIZE), SIZE);

            unsafe {
                half* ptr = voxelDataPtrs[chunkIndex];

                if (ptr != null) {
                    half* offset = (ptr + voxelIndex);
                    return *offset;
                } else {
                    return half.zero;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe half FetchDensity(uint3 position, half* basePtr) {
            int voxelIndex = PosToIndex(position, SIZE);

            unsafe {
                half* ptr = basePtr;

                if (ptr != null) {
                    half* offset = (ptr + voxelIndex);
                    return *offset;
                } else {
                    return half.zero;
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
                all &= CheckPositionInsideMultipleChunks(position + (int3)IndexToPos(i, 2), mask);
            }
            return all;
        }

        // Checks if the given GLOBAL position (could be negative) is valid with the given neighbours
        // Checks if it's a valid position for all 26 neighbours (including the ones in the negative direction)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckPositionInsideMultipleChunks(int3 position, BitField32 mask) {
            int3 temp1 = position + PHYSICAL_CHUNK_SIZE;

            DebugCheckBounds(temp1, PHYSICAL_CHUNK_SIZE * 3);

            int3 chunkPosition = temp1 / SIZE;

            //Debug.Log(chunkPosition);
            int index1 = PosToIndex((uint3)chunkPosition, 3);

            return mask.IsSet(index1);
        }

        // Converts a world space voxel position to a chunk position and a chunk local voxel position
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WorldVoxelPosToChunkSpace(int3 worldSpaceVoxelPos, out int3 chunkPosition, out uint3 chunkSpaceVoxelPos) {
            chunkPosition = (int3)math.floor((float3)worldSpaceVoxelPos / PHYSICAL_CHUNK_SIZE);
            chunkSpaceVoxelPos = Mod(worldSpaceVoxelPos, PHYSICAL_CHUNK_SIZE);
        }

        // Checks if a position is stored inside the volume
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckPositionInsideVolume(int3 position, int size) {
            return math.all(position >= 0 & position < size);
        }

        // Checks if a position is stored inside the chunk
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckPositionInsideChunk(int3 position) {
            return CheckPositionInsideVolume(position, SIZE);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static half SampleDensityInterpolated(float3 position, ref UnsafePtrList<half> neighbours) {
            float3 frac = math.frac(position);
            int3 voxPos = (int3)math.floor(position);

            float d000 = FetchDensityNeighbours(voxPos, ref neighbours);
            float d100 = FetchDensityNeighbours(voxPos + math.int3(1, 0, 0), ref neighbours);
            float d010 = FetchDensityNeighbours(voxPos + math.int3(0, 1, 0), ref neighbours);
            float d110 = FetchDensityNeighbours(voxPos + math.int3(1, 1, 0), ref neighbours);

            float d001 = FetchDensityNeighbours(voxPos + math.int3(0, 0, 1), ref neighbours);
            float d101 = FetchDensityNeighbours(voxPos + math.int3(1, 0, 1), ref neighbours);
            float d011 = FetchDensityNeighbours(voxPos + math.int3(0, 1, 1), ref neighbours);
            float d111 = FetchDensityNeighbours(voxPos + math.int3(1, 1, 1), ref neighbours);

            float4 d0 = new float4(d000, d010, d001, d011);
            float4 d1 = new float4(d100, d110, d101, d111);
            float4 interpX = math.lerp(d0, d1, frac.x);

            float2 m01 = new float2(interpX.x, interpX.y);
            float2 m23 = new float2(interpX.z, interpX.w);
            float2 m = math.lerp(m01, m23, frac.z);
            float mixed6 = math.lerp(m.x, m.y, frac.y);

            return (half)mixed6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe half SampleDensityInterpolated(float3 position, half* basePtr) {
            float3 frac = math.frac(position);
            uint3 voxPos = (uint3)math.floor(position);

            float d000 = FetchDensity(voxPos, basePtr);
            float d100 = FetchDensity(voxPos + math.uint3(1, 0, 0), basePtr);
            float d010 = FetchDensity(voxPos + math.uint3(0, 1, 0), basePtr);
            float d110 = FetchDensity(voxPos + math.uint3(1, 1, 0), basePtr);

            float d001 = FetchDensity(voxPos + math.uint3(0, 0, 1), basePtr);
            float d101 = FetchDensity(voxPos + math.uint3(1, 0, 1), basePtr);
            float d011 = FetchDensity(voxPos + math.uint3(0, 1, 1), basePtr);
            float d111 = FetchDensity(voxPos + math.uint3(1, 1, 1), basePtr);

            float4 d0 = new float4(d000, d010, d001, d011);
            float4 d1 = new float4(d100, d110, d101, d111);
            float4 interpX = math.lerp(d0, d1, frac.x);

            float2 m01 = new float2(interpX.x, interpX.y);
            float2 m23 = new float2(interpX.z, interpX.w);
            float2 m = math.lerp(m01, m23, frac.z);
            float mixed6 = math.lerp(m.x, m.y, frac.y);

            return (half)mixed6;
        }
    }
}