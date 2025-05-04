using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public static class StitchUtils {
        // Flatten a 3D position to a face 2D position using a given direction axis
        // dir order = X,Y,Z
        public static uint2 FlattenToFaceRelative(uint3 position, int dir) {
            if (dir == 0) {
                return position.yz;
            } else if (dir == 1) {
                return position.xz;
            } else if (dir == 2) {
                return position.xy;
            }

            // never should happen
            throw new Exception();
        }

        // Flatten a 3D position to an edge 1D index using a given direction axis
        // dir order = X,Y,Z
        public static int FlattenToEdge(uint3 position, int dir) {
            return (int)position[dir];
        }
    }
}