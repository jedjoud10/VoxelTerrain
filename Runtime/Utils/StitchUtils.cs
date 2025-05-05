using System;
using System.Runtime.CompilerServices;
using Codice.Client.BaseCommands.WkStatus.Printers;
using jedjoud.VoxelTerrain.Meshing;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public static class StitchUtils {
        // Calculate the number of elements needed to store boundary data for a given size
        // Encoding: 3 faces, 3 edges, 1 corner
        public static int CalculateBoundaryLength(int size) {
            // Discrete 2 came in clutch...
            return size * size * 3 - 3 * size + 1;
        }

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
        public static uint3 UnflattenFromFaceRelative(uint2 relative, int dir, uint missing=0) {
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

        // Flatten a 3D position to an edge 1D index using a given direction axis
        // dir order = X,Y,Z
        public static uint FlattenToEdgeRelative(uint3 position, int dir) {
            return position[dir];
        }

        // Unflatten a 1D index to an 3D position using a given direction axis
        // Also fill up the missing coordinates (2 of em) with a specific value (they are the same since edge)
        // dir order = X,Y,Z
        public static uint3 UnflattenFromEdgeRelative(uint relative, int dir, uint missing=0) {
            uint3 val = new uint3(missing);
            val[dir] = relative;
            return val;
        }

        // Convert an index to a 2D position
        public static uint2 IndexToPos2D(int index, int size) {
            return new uint2((uint)(index % size), (uint)(index / size));
        }

        // Convert a 2D position into an index
        public static int PosToIndex2D(uint2 position, int size) {
            return (int)(position.x + position.y * size);
        }

        // Convert a 3D position into an index that we can use for our boundary data (custom packing)
        // Encoding: 3 faces, 3 edges, 1 corner
        public static int PosToBoundaryIndex(uint3 position, int size) {
            return 0;
        }

        // Converts a packed boundary index into a 3D position
        // Encoding: 3 faces, 3 edges, 1 corner
        public static uint3 BoundaryIndexToPos(int index, int size) {
            int face = size * size;
            int edge = size;
            
            if (index < face * 3) {
                // faces
                int faceIndex = (index) / face;
                
                // local 2D index within the face
                int index2D = index % face;
                uint2 faceLocalPos = IndexToPos2D(index2D, size);
                return UnflattenFromFaceRelative(faceLocalPos, faceIndex, missing: (uint)(size-1));
            } else if (index < (face * 3 + edge * 3)) {
                // edges
                int edgeIndex = (index - face * 3) / edge;

                // local 1D index within the edge
                int index1D = index % edge;
                return UnflattenFromEdgeRelative((uint)index1D, edgeIndex, missing: (uint)(size-1));
            } else if (index == (face * 3 + edge * 3)) {
                // corner
                new uint3(size-1);
            }

            throw new Exception();
        }

        // type=0 -> uniform (normal)
        // type=1 -> lotohi (downsample)
        // type=2 -> hitolo (upsample)
        private unsafe static Voxel SamplePlaneUsingType(uint3 paddingPosition, uint type, int dir, ref VoxelStitch.JobData data) {
            uint3 flatPosition = paddingPosition;
            flatPosition[dir] = 0;

            if (type == 0) {
                Voxel* voxels = data.planes[dir].uniform;
                Voxel voxel = *(voxels + VoxelUtils.PosToIndexMorton(flatPosition));
                return voxel;
            } else if (type == 1) {
                uint2 flattened = FlattenToFaceRelative(paddingPosition, dir);
                int mortonOffset = VoxelUtils.PosToIndexMorton2D(flattened / 32);
                Voxel* voxels = data.planes[dir].lod0s[mortonOffset];
                Voxel voxel = *(voxels + VoxelUtils.PosToIndexMorton((flatPosition * 2) % 64));
                return voxel;
            } else if (type == 2) {
                return Voxel.Empty;
            }

            return Voxel.Empty;
        }

        // Fetch a voxel at the given size=65 padding position
        // Requires the neighbour stuff from the stitcher to be calculated and converted to raw pointers
        // Handles downsampling/upsampling of data automatically
        public static Voxel Sample(uint3 paddingPosition, ref VoxelStitch.JobData data) {
            // 1=plane, 2=edge, 3=corner
            bool3 bool3 = paddingPosition == 64;
            int bitmask = math.bitmask(new bool4(bool3, false));
            int bitsSet = math.countbits(bitmask);

            if (bitsSet == 1) {
                // check which axis is set
                int dir = math.tzcnt(bitmask);
                uint type = data.state.GetBits(dir * 2, 2);
                return SamplePlaneUsingType(paddingPosition, type, dir, ref data);
            } else if (bitsSet == 2) {
                // check which axis is NOT set
                int inv = (~bitmask) & 0b111;
                int dir = math.tzcnt(inv);

            } else {
                // corner case

            }

            return new Voxel() {
                density = (half)(-100f),
            };
        }
    }
}