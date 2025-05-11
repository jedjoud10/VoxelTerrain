using System;
using System.Runtime.CompilerServices;
using Codice.Client.BaseCommands.WkStatus.Printers;
using jedjoud.VoxelTerrain.Meshing;
using jedjoud.VoxelTerrain.Unsafe;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public static class SkirtUtils {
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
        
        // Unflatten a 2D face local position into 3D using a direction
        // Also fill up the missing coordinate with a specific value
        // dir order = X,Y,Z
        public static uint3 UnflattenFromFaceRelative(uint2 relative, int dir, uint missing = 0) {
            if (dir == 0) {
                return new uint3(missing, relative.x, relative.y);
            } else if (dir == 1) {
                return new uint3(relative.x, missing, relative.y);
            } else if (dir == 2) {
                return new uint3(relative.x, relative.y, missing);
            }

            // never should happen
            throw new Exception();
        }

        /*
        // Convert a 3D direction to a 2D plane relative direction
        public static int ConvertDir3Dto2D(int dir, int faceNormal) {

            if (faceNormal == 0) {
                if (dir == 1) {
                    return 0;
                } else if (dir == 2) {
                    return 1;
                }
            } else if (faceNormal == 1) {
                if (dir == 0) {
                    return 0;
                } else if (dir == 2) {
                    return 1;
                }
            } else if (faceNormal == 2) {
                if (dir == 0) {
                    return 0;
                } else if (dir == 1) {
                    return 1;
                }
            }

            // never should happen
            throw new Exception();
        }





        // Check if a position lies on a boundary
        public static bool LiesOnBoundary(int3 position, int size, bool negative=false) {
            int target = math.select(size - 1, 0, negative);
            return math.all(position >= 0) && math.all(position < size) && math.any(position == target);
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void DebugCheckMustBeOnBoundary(uint3 coordinates, int size, bool negative=false) {
            if (!LiesOnBoundary((int3)coordinates, size, negative)) {
                throw new System.Exception(
                    $"The given coordinates {coordinates} do not exist on the boundary of size {size}");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void DebugCheckBounds(uint3 coordinates, int size) {
            if (math.cmax(coordinates) >= size) {
                throw new System.OverflowException(
                    $"An element of coordinates {coordinates} is larger than the maximum {size-1} (size={size})");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void DebugCheckIndex(int index, int size) {
            if (index >= (size * size * 3 - size * 3 + 1)) {
                throw new System.OverflowException(
                    $"The given index {index} is larger than the maximum {size * size * 3 - size * 3 + 1}");
            }

            if (index < 0) {
                throw new System.OverflowException(
                    $"The given index is negative");
            }
        }

        // Convert a 3D position into an index that we can use for our boundary data (custom packing)
        // Encoding: 3 faces, 3 edges, 1 corner
        public static int PosToBoundaryIndex(uint3 position, int size, bool negative=false) {
            DebugCheckBounds(position, size);
            DebugCheckMustBeOnBoundary(position, size, negative);

            int face = (size - 1) * (size - 1);
            int edge = (size - 1);

            // 1=plane, 2=edge, 3=corner
            bool3 bool3 = math.select(new uint3(size - 1), uint3.zero, negative) == position;
            int bitmask = math.bitmask(new bool4(bool3, false));
            int bitsSet = math.countbits(bitmask);

            if (bitsSet == 1) {
                // check which axis is set
                int dir = math.tzcnt(bitmask);
                uint2 flattened = (uint2)((int2)FlattenToFaceRelative(position, dir) - math.select(int2.zero, 1, negative));
                int faceLocalIndex = VoxelUtils.PosToIndex2D(flattened, size-1);
                return faceLocalIndex + face * dir;
            } else if (bitsSet == 2) {
                // check which axis is NOT set
                int inv = (~bitmask) & 0b111;
                int dir = math.tzcnt(inv);
                int edgeLocalIndex = (int)FlattenToEdgeRelative(position, dir) - math.select(0, 1, negative);
                return edgeLocalIndex + edge * dir + face*3;
            } else {
                // corner case
                return face * 3 + edge * 3;
            }
        }

        // Converts a packed boundary index into a 3D position
        // Encoding: 3 faces, 3 edges, 1 corner
        // Assumes we are dealing with the positive boundary, but it is toggable
        public static uint3 BoundaryIndexToPos(int index, int size, bool negative=false) {
            DebugCheckIndex(index, size);

            int face = (size-1) * (size-1);
            int edge = (size-1);
            
            if (index < face * 3) {
                // faces
                int faceIndex = (index) / face;
                
                // local 2D index within the face
                int index2D = index % face;
                uint2 faceLocalPos = VoxelUtils.IndexToPos2D(index2D, size-1);
                return UnflattenFromFaceRelative(faceLocalPos + math.select(uint2.zero, 1, negative), faceIndex, math.select((uint)(size - 1), 0, negative));
            } else if (index < (face * 3 + edge * 3)) {
                // edges
                int edgeIndex = (index - face * 3) / edge;

                // local 1D index within the edge
                int index1D = index % edge;
                return UnflattenFromEdgeRelative((uint)index1D + (uint)math.select(0, 1, negative), edgeIndex, math.select((uint)(size - 1), 0, negative));
            } else if (index == (face * 3 + edge * 3)) {
                // corner
                return math.select(new uint3(size - 1), 0, negative);
            }

            throw new Exception();
        }
        */
    }
}