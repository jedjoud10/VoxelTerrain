using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static jedjoud.VoxelTerrain.VoxelUtils;

namespace jedjoud.VoxelTerrain {
    // https://discussions.unity.com/t/bc1361-when-total-static-readonly-array-length-is-greater-than-180/874045/2
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