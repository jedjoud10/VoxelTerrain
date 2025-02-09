using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    // Common terrain utility methods
    public static class VoxelUtils {
        // Scaling value applied to the vertices
        public static float VertexScaling => (float)Size / ((float)Size - 3.0F);

        // Voxel scaling size
        public static int VoxelSizeReduction { get; set; }

        // Used for parallelism control for the CPU side meshing and editing
        public static int SchedulingInnerloopBatchCount { get; set; } = 16;

        // Scaling factor when using voxel size reduction
        // Doesn't actually represent the actual size of the voxel (since we do some scaling anyways)
        public static float VoxelSizeFactor => 1F / Mathf.Pow(2F, VoxelSizeReduction);

        // Current chunk resolution
        public static int Size = 64;

        // Total number of voxels in a chunk
        public static int Volume => Size * Size * Size;

        // Should we calculate per vertex normals
        public static bool PerVertexNormals { get; set; }

        // Should we calculate per vertex density and ambient occlusion?
        public static bool PerVertexUvs { get; set; }

        // Should we enable smoothing when meshing?
        public static bool Smoothing { get; set; }

        // Full control over how the ambient occlusion is calculated
        public static float AmbientOcclusionOffset { get; set; }
        public static float AmbientOcclusionPower { get; set; }
        public static float AmbientOcclusionSpread { get; set; }
        public static float AmbientOcclusionGlobalOffset { get; set; }

        // Max possible number of materials supported by the terrain mesh
        public const int MAX_MATERIAL_COUNT = 256;

        // Offsets used for octree generation
        public static readonly int3[] OctreeChildOffset = {
        new int3(0, 0, 0),
        new int3(0, 0, 1),
        new int3(1, 0, 0),
        new int3(1, 0, 1),
        new int3(0, 1, 0),
        new int3(0, 1, 1),
        new int3(1, 1, 0),
        new int3(1, 1, 1),
    };

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

        // Custom modulo operator to discard negative numbers
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint3 Mod(int3 val, int size) {
            int3 r = val % size;
            return (uint3)math.select(r, r + size, r < 0);
        }

        // Custom modulo operator to discard negative numbers
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Mod(float3 val, float size) {
            float3 r = val % size;
            return math.select(r, r + size, r < 0);
        }

        // Convert an index to a 3D position (morton coding)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint3 IndexToPos(int index) {
            return Morton.DecodeMorton32((uint)index);
        }

        // Convert a 3D position into an index (morton coding)
        [return: AssumeRange(0u, 262144)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PosToIndex(uint3 position) {
            return (int)Morton.EncodeMorton32(position);
        }

        // Sampled the voxel grid using trilinear filtering
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static half SampleGridInterpolated(float3 position, ref NativeArray<Voxel> voxels, int size) {
            float3 frac = math.frac(position);
            uint3 voxPos = (uint3)math.floor(position);
            voxPos = math.min(voxPos, math.uint3(size - 2));
            voxPos = math.max(voxPos, math.uint3(0));

            float d000 = voxels[PosToIndex(voxPos)].density;
            float d100 = voxels[PosToIndex(voxPos + math.uint3(1, 0, 0))].density;
            float d010 = voxels[PosToIndex(voxPos + math.uint3(0, 1, 0))].density;
            float d110 = voxels[PosToIndex(voxPos + math.uint3(0, 0, 1))].density;

            float d001 = voxels[PosToIndex(voxPos + math.uint3(0, 0, 1))].density;
            float d101 = voxels[PosToIndex(voxPos + math.uint3(1, 0, 1))].density;
            float d011 = voxels[PosToIndex(voxPos + math.uint3(0, 1, 1))].density;
            float d111 = voxels[PosToIndex(voxPos + math.uint3(1, 1, 1))].density;

            float mixed0 = math.lerp(d000, d100, frac.x);
            float mixed1 = math.lerp(d010, d110, frac.x);
            float mixed2 = math.lerp(d001, d101, frac.x);
            float mixed3 = math.lerp(d011, d111, frac.x);

            float mixed4 = math.lerp(mixed0, mixed2, frac.z);
            float mixed5 = math.lerp(mixed1, mixed3, frac.z);

            float mixed6 = math.lerp(mixed4, mixed5, frac.y);

            return (half)mixed6;
        }

        // Calculate the normals at a specific position
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SampleGridNormal(uint3 position, ref NativeArray<Voxel> voxels, int size) {
            position = math.min(position, math.uint3(size - 2));

            float baseVal = voxels[PosToIndex(position)].density;
            float xVal = voxels[PosToIndex(position + math.uint3(1, 0, 0))].density;
            float yVal = voxels[PosToIndex(position + math.uint3(0, 1, 0))].density;
            float zVal = voxels[PosToIndex(position + math.uint3(0, 0, 1))].density;

            return new float3(baseVal - xVal, baseVal - yVal, baseVal - zVal);
        }
    }
}