using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    // https://discussions.unity.com/t/bc1361-when-total-static-readonly-array-length-is-greater-than-180/874045/2
    public static class EdgeMaskUtils3 {
        // Quad vertices index offsets based on direction
        // uint3 offset = basePosition + forward - math.uint3(1);
        // offset + VoxelUtils.PERPENDICULAR_OFFSETS[direction * 4 + i]
        // Quad vertices index offsets based on direction
        // uint3 offset = basePosition + forward - math.uint3(1);
        // offset + VoxelUtils.PERPENDICULAR_OFFSETS[direction * 4 + i]
        public static readonly int4[] PERPENDICULAR_OFFSETS_INDEX_OFFSET = new int4[] {
            new int4(
                PosToIndex(new uint3(1, 0, 0) + new uint3(0, 0, 0)),
                PosToIndex(new uint3(1, 0, 0) + new uint3(0, 1, 0)),
                PosToIndex(new uint3(1, 0, 0) + new uint3(0, 1, 1)),
                PosToIndex(new uint3(1, 0, 0) + new uint3(0, 0, 1))
            ),

            new int4(
                PosToIndex(new uint3(0, 1, 0) + new uint3(0, 0, 0)),
                PosToIndex(new uint3(0, 1, 0) + new uint3(0, 0, 1)),
                PosToIndex(new uint3(0, 1, 0) + new uint3(1, 0, 1)),
                PosToIndex(new uint3(0, 1, 0) + new uint3(1, 0, 0))
            ),

            new int4(
                PosToIndex(new uint3(0, 0, 1) + new uint3(0, 0, 0)),
                PosToIndex(new uint3(0, 0, 1) + new uint3(1, 0, 0)),
                PosToIndex(new uint3(0, 0, 1) + new uint3(1, 1, 0)),
                PosToIndex(new uint3(0, 0, 1) + new uint3(0, 1, 0))
            ),
        };

        // Forward direction (as an offset) of each quad
        public static readonly int[] FORWARD_DIRECTION_INDEX_OFFSET = new int[] {
            1, VoxelUtils.SIZE*VoxelUtils.SIZE, VoxelUtils.SIZE
        };

        public static readonly int NEGATIVE_ONE_OFFSET = -(1 + VoxelUtils.SIZE + VoxelUtils.SIZE * VoxelUtils.SIZE);

        private static int PosToIndex(uint3 pos) {
            return (int)(pos.x + pos.y * VoxelUtils.SIZE * VoxelUtils.SIZE + pos.z * VoxelUtils.SIZE);
        }
    }
}