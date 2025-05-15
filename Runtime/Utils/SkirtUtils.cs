using System;
using Unity.Collections;
using Unity.Mathematics;

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

        public static int2 FlattenToFaceRelative(int3 position, int dir) {

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

        public static float3 UnflattenFromFaceRelative(float2 relative, int dir, float missing = 0) {
            if (dir == 0) {
                return new float3(missing, relative.x, relative.y);
            } else if (dir == 1) {
                return new float3(relative.x, missing, relative.y);
            } else if (dir == 2) {
                return new float3(relative.x, relative.y, missing);
            }

            // never should happen
            throw new Exception();
        }

        public static int3 UnflattenFromFaceRelative(int2 relative, int dir, int missing = 0) {
            if (dir == 0) {
                return new int3(missing, relative.x, relative.y);
            } else if (dir == 1) {
                return new int3(relative.x, missing, relative.y);
            } else if (dir == 2) {
                return new int3(relative.x, relative.y, missing);
            }

            // never should happen
            throw new Exception();
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void DebugCheckOnlyOneBitMask(bool2 mask) {
            if (CountTrue(mask) != 1) {
                throw new System.Exception(
                    $"There must exactly be one bool set in the bool2 mask");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void DebugCheckOnlyOneBitMask(bool3 mask) {
            if (CountTrue(mask) != 1) {
                throw new System.Exception(
                    $"There must exactly be one bool set in the bool3 mask");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void DebugCheckOnlyOneBitMask(bool4 mask) {
            if (CountTrue(mask) != 1) {
                throw new System.Exception(
                    $"There must exactly be one bool set in the bool4 mask");
            }
        }

        // Get the direction of an edge within a face relative space
        // Converts 2D direction to 3D basically
        public static int GetEdgeDirFaceRelative(bool2 mask, int faceNormal) {
            // No two bools can be set, otherwise that means that this is a CORNER
            DebugCheckOnlyOneBitMask(mask);

            // Need to pick the "other" value that isn't at a boundary
            mask = !mask;

            // X AXIS
            if (faceNormal == 0) {

                if (mask.x) {
                    // Y
                    return 1;
                } else if (mask.y) {
                    // Z
                    return 2;
                }

            // Y AXIS
            } else if (faceNormal == 1) {
                if (mask.x) {
                    // X
                    return 0;
                } else if (mask.y) {
                    // Z
                    return 2;
                }

            // Z AXIS
            } else if (faceNormal == 2) {
                if (mask.x) {
                    // X
                    return 0;
                } else if (mask.y) {
                    // Y
                    return 1;
                }
            }

            // never should happen
            throw new Exception();
        }

        /*
        // Convert a 3D direction to a 2D plane relative direction
        public static int ConvertDir3Dto2D(int dir, int faceNormal) {


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

        static readonly int3[] DEDUPE_TRIS_THING = new int3[] {
            new int3(0, 2, 3), // x/y, discard y
            new int3(0, 1, 3), // x/z, discard z
            new int3(0, 1, 2), // x/w, discard w
            new int3(0, 1, 3), // y/z, discard z
            new int3(0, 1, 2), // y/w, discard w
            new int3(0, 1, 2), // z/w, discard w
        };

        static readonly int3[] IGNORE_SPECIFIC_VALUE_TRI = new int3[] {
            new int3(1, 2, 3), // discard x
            new int3(0, 2, 3), // discard y
            new int3(0, 1, 3), // discard z
            new int3(0, 1, 2), // discard w
        };

        public static int CountTrue(bool2 b3) {
            return math.countbits(math.bitmask(new bool4(b3, false, false)));
        }

        public static int CountTrue(bool3 b3) {
            return math.countbits(math.bitmask(new bool4(b3, false)));
        }

        public static int CountTrue(bool4 b4) {
            return math.countbits(math.bitmask(b4));
        }

        public static int FindTrueIndex(bool4 b4) {
            DebugCheckOnlyOneBitMask(b4);
            return math.tzcnt(math.bitmask(b4));
        }

        public static int FindTrueIndex(bool3 b3) {
            DebugCheckOnlyOneBitMask(b3);
            return math.tzcnt(math.bitmask(new bool4(b3, false)));
        }

        // Create quads / triangles based on the given vertex index data in the "v" parameter
        public static bool TryAddQuadsOrTris(bool flip, int4 v, ref NativeCounter.Concurrent triangleCounter, ref NativeArray<int> indices) {
            // Ts gpt-ed kek
            int dupeType = 0;
            dupeType |= math.select(0, 1, v.x == v.y);
            dupeType |= math.select(0, 2, v.x == v.z);
            dupeType |= math.select(0, 4, v.x == v.w);
            dupeType |= math.select(0, 8, v.y == v.z && v.x != v.y);
            dupeType |= math.select(0, 16, v.y == v.w && v.x != v.y && v.z != v.y);
            dupeType |= math.select(0, 32, v.z == v.w && v.x != v.z && v.y != v.z);

            // Means that there are more than 2 duplicate verts, not possible?
            if (math.countbits(dupeType) > 1) {
                return false;
            }

            // If there's only a SINGLE invalid index, then considering it to an extra duplicate one (and create a triangle for the valid ones instead)
            bool4 b4 = v == int.MaxValue;
            if (CountTrue(b4) == 1) {
                int3 remapper = IGNORE_SPECIFIC_VALUE_TRI[FindTrueIndex(b4)];
                int3 uniques = new int3(v[remapper[0]], v[remapper[1]], v[remapper[2]]);
                int triIndex = triangleCounter.Add(1) * 3;
                indices[triIndex + (flip ? 0 : 2)] = uniques[0];
                indices[triIndex + 1] = uniques[1];
                indices[triIndex + (flip ? 2 : 0)] = uniques[2];
                return true;
            }

            if (dupeType == 0) {
                if (math.cmax(v) == int.MaxValue | math.cmin(v) < 0) {
                    return false;
                }

                int triIndex = triangleCounter.Add(2) * 3;

                // Set the first tri
                indices[triIndex + (flip ? 0 : 2)] = v.x;
                indices[triIndex + 1] = v.y;
                indices[triIndex + (flip ? 2 : 0)] = v.z;

                // Set the second tri
                indices[triIndex + (flip ? 3 : 5)] = v.z;
                indices[triIndex + 4] = v.w;
                indices[triIndex + (flip ? 5 : 3)] = v.x;
            } else {
                int config = math.tzcnt(dupeType);
                int3 remapper = DEDUPE_TRIS_THING[config];
                int3 uniques = new int3(v[remapper[0]], v[remapper[1]], v[remapper[2]]);

                if (math.cmax(uniques) == int.MaxValue | math.cmin(v) < 0) {
                    return false;
                }

                int triIndex = triangleCounter.Add(1) * 3;
                indices[triIndex + (flip ? 0 : 2)] = uniques[0];
                indices[triIndex + 1] = uniques[1];
                indices[triIndex + (flip ? 2 : 0)] = uniques[2];
            }

            return true;
        }
    }
}