using System;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public static class SkirtUtils {
        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void DebugCheckDirIndex(int dir) {
            if (dir < 0 || dir > 2) {
                throw new System.OverflowException(
                    $"Direction index {dir} is not in the valid direction range [0, 3)");
            }
        }


        // Flatten a 3D position to a face 2D position using a given direction axis
        // dir order = X,Y,Z
        public static uint2 FlattenToFaceRelative(uint3 position, int dir) {
            DebugCheckDirIndex(dir);

            if (dir == 0) {
                return position.yz;
            } else if (dir == 1) {
                return position.xz;
            } else if (dir == 2) {
                return position.xy;
            }

            // should never happen
            throw new Exception();
        }

        public static int2 FlattenToFaceRelative(int3 position, int dir) {
            DebugCheckDirIndex(dir);

            if (dir == 0) {
                return position.yz;
            } else if (dir == 1) {
                return position.xz;
            } else if (dir == 2) {
                return position.xy;
            }

            // should never happen
            throw new Exception();
        }

        // Unflatten a 2D face local position into 3D using a direction
        // Also fill up the missing coordinate with a specific value
        // dir order = X,Y,Z
        public static uint3 UnflattenFromFaceRelative(uint2 relative, int dir, uint missing = 0) {
            DebugCheckDirIndex(dir);

            if (dir == 0) {
                return new uint3(missing, relative.x, relative.y);
            } else if (dir == 1) {
                return new uint3(relative.x, missing, relative.y);
            } else if (dir == 2) {
                return new uint3(relative.x, relative.y, missing);
            }

            // should never happen
            throw new Exception();
        }

        public static float3 UnflattenFromFaceRelative(float2 relative, int dir, float missing = 0) {
            DebugCheckDirIndex(dir);

            if (dir == 0) {
                return new float3(missing, relative.x, relative.y);
            } else if (dir == 1) {
                return new float3(relative.x, missing, relative.y);
            } else if (dir == 2) {
                return new float3(relative.x, relative.y, missing);
            }

            // should never happen
            throw new Exception();
        }

        public static int3 UnflattenFromFaceRelative(int2 relative, int dir, int missing = 0) {
            DebugCheckDirIndex(dir);

            if (dir == 0) {
                return new int3(missing, relative.x, relative.y);
            } else if (dir == 1) {
                return new int3(relative.x, missing, relative.y);
            } else if (dir == 2) {
                return new int3(relative.x, relative.y, missing);
            }

            // should never happen
            throw new Exception();
        }

        // Get the direction of an edge within a face relative space
        // Converts 2D direction to 3D basically
        public static int GetEdgeDirFaceRelative(bool2 mask, int faceNormal) {
            DebugCheckDirIndex(faceNormal);

            // No two bools can be set, otherwise that means that this is a CORNER
            BitUtils.DebugCheckOnlyOneBitMask(mask);

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

            // should never happen
            throw new Exception();
        }
    }
}