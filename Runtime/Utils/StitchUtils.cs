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

        // goonology 101. today we will learn how to edge...
        public static (uint3, uint) FetchAxisAndKeepOnEdging(uint3 position, int dir) {
            if (dir == 0) {
                position.yz = 0;
                return (position, position.x);
            } else if (dir == 1) {
                position.xz = 0;
                return (position, position.y);
            } else if (dir == 2) {
                position.xy = 0;
                return (position, position.z);
            }

            // never should happen
            throw new Exception();
        }

        // Flatten a 3D position to an edge 1D index using a given direction axis
        // dir order = X,Y,Z
        public static int FlattenToEdgeRelative(uint3 position, int dir) {
            return (int)position[dir];
        }

        // Unflatten a 1D index to an 3D position using a given direction axis
        // Also fill up the missing coordinates (2 of em) with a specific value (they are the same since edge)
        // dir order = X,Y,Z
        public static uint3 UnflattenFromEdgeRelative(uint relative, int dir, uint missing=0) {
            uint3 val = new uint3(missing);
            val[dir] = relative;
            return val;
        }

        // Check if a position lies on a boundary
        // Assumes positive boundary
        public static bool LiesOnBoundary(uint3 position, int size) {
            return math.any(position == new uint3(size - 1));
        }

        // Convert a 3D position into an index that we can use for our boundary data (custom packing)
        // Encoding: 3 faces, 3 edges, 1 corner
        public static int PosToBoundaryIndex(uint3 position, int size, bool negative=false) {
            if (math.any(position >= (new uint3(size)))) {
                throw new Exception("WHAT THE FUCK????");
            }

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
                int edgeLocalIndex = FlattenToEdgeRelative(position, dir) - math.select(0, 1, negative);
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

        // type=0 -> uniform (normal)
        // type=1 -> lotohi (downsample)
        // type=2 -> hitolo (upsample)
        private unsafe static Voxel SamplePlaneUsingType(uint3 paddingPosition, uint type, int dir, ref VoxelStitch.GenericBoundaryData<Voxel> data) {
            uint3 flatPosition = paddingPosition;
            flatPosition[dir] = 0;

            if (type == 0) {
                // do a bit of simple copying
                Voxel* voxels = data.planes[dir].uniform;
                Voxel voxel = *(voxels + PosToBoundaryIndex(flatPosition, 64, true));
                return voxel;
            } else if (type == 1) {
                // do a bit of downsampling
                uint2 flattened = FlattenToFaceRelative(paddingPosition, dir);
                int mortonOffset = VoxelUtils.PosToIndex2D(flattened / 32, 2);
                Voxel* voxels = data.planes[dir].lod0s[mortonOffset];
                Voxel voxel = *(voxels + PosToBoundaryIndex((flatPosition * 2) % 64, 64, true));
                return voxel;
            } else if (type == 2) {
                // do a bit of upsampling
                uint2 flattened = FlattenToFaceRelative(paddingPosition, dir);
                flattened += data.planes[dir].relativeOffset * 64;
                flatPosition = UnflattenFromFaceRelative(flattened, dir);
                Voxel* voxels = data.planes[dir].lod1;
                Voxel voxel = *(voxels + PosToBoundaryIndex(flatPosition / 2, 64, true));
                return voxel;
            }

            return Voxel.Empty;
        }

        // type=0 -> uniform (normal)
        // type=1 -> lotohi (downsample)
        // type=2 -> hitolo (upsample)
        private unsafe static Voxel SampleEdgeUsingType(uint3 paddingPosition, uint type, int dir, ref VoxelStitch.GenericBoundaryData<Voxel> data) {
            (uint3 edged, uint axis) = FetchAxisAndKeepOnEdging(paddingPosition[dir], dir);

            if (type == 0) {
                // do a bit of simple copying
                Voxel* voxels = data.edges[dir].uniform;
                Voxel voxel = *(voxels + PosToBoundaryIndex(edged, 64, true));
                return voxel;
            } else if (type == 1) {
                // do a bit of downsampling
                uint offset = axis / 32;
                Voxel* voxels = data.edges[dir].lod0s[(int)offset];
                Voxel voxel = *(voxels + PosToBoundaryIndex((edged * 2) % 64, 64, true));
                return voxel;
            } else if (type == 2) {
                // do a bit of upsampling
                uint offset = axis + data.edges[dir].relativeOffset * 64;
                edged = UnflattenFromEdgeRelative(offset, dir);
                Voxel* voxels = data.edges[dir].lod1;
                Voxel voxel = *(voxels + PosToBoundaryIndex(edged / 2, 64, true));
                return voxel;
            }

            return Voxel.Empty;
        }

        // type=0 -> uniform (normal)
        // type=1 -> lotohi (downsample)
        // type=2 -> hitolo (upsample)
        private unsafe static Voxel SampleCornerUsingType(uint3 paddingPosition, uint type, ref VoxelStitch.GenericBoundaryData<Voxel> data) {

            // Corner piece always at (0,0,0), but that's the last element in our padding array
            Voxel* voxels = null;
            if (type == 0) {
                // do a bit of simple copying
                voxels = data.corner.uniform;
            } else if (type == 1) {
                voxels = data.corner.lod0;
            } else if (type == 2) {
                voxels = data.corner.lod1;
            } else {
                throw new Exception("I LOVE NULL POINTERS!!!!");
            }

            return *(voxels + 63*63 * 3 + 63*3);
        }

        // Fetch a voxel at the given size=65 padding position
        // Requires the neighbour stuff from the stitcher to be calculated and converted to raw pointers
        // Handles downsampling/upsampling of data automatically
        public static Voxel Sample(uint3 paddingPosition, ref VoxelStitch.GenericBoundaryData<Voxel> data) {
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
                uint type = data.state.GetBits(dir * 2 + 6, 2);
                return SampleEdgeUsingType(paddingPosition, type, dir, ref data);
            } else {
                // corner case
                uint type = data.state.GetBits(12, 2);
                return SampleCornerUsingType(paddingPosition, type, ref data);
            }

            return new Voxel() {
                density = (half)(-100f),
            };
        }

        // Fetch unpacked chunk index that we will use to index the offsets array (unpacked)
        public static int FetchUnpackedNeighbourIndex(uint3 paddingPosition, BitField32 state) {
            // 1=plane, 2=edge, 3=corner
            bool3 bool3 = paddingPosition == 64;
            int bitmask = math.bitmask(new bool4(bool3, false));
            int bitsSet = math.countbits(bitmask);

            if (bitsSet == 1) {
                // check which axis is set
                int dir = math.tzcnt(bitmask);
                uint type = state.GetBits(dir * 2, 2);

                if (type == 0) {
                    // do a bit of simple copying
                    return dir * 4;
                } else if (type == 1) {
                    // do a bit of downsampling
                    uint2 flattened = FlattenToFaceRelative(paddingPosition, dir);
                    int mortonOffset = VoxelUtils.PosToIndex2D(flattened / 32, 2);
                    return dir * 4 + mortonOffset;
                } else if (type == 2) {
                    // do a bit of upsampling
                    return dir * 4;
                }
            } else if (bitsSet == 2) {
                // check which axis is NOT set
                int inv = (~bitmask) & 0b111;
                int dir = math.tzcnt(inv);
                uint type = state.GetBits(dir * 2 + 6, 2);

                if (type == 0) {
                    // do a bit of simple copying
                    return dir * 2 + 12;
                } else if (type == 1) {
                    // do a bit of downsampling
                    (uint3 edged, uint axis) = FetchAxisAndKeepOnEdging(paddingPosition[dir], dir);
                    uint offset = axis / 32;
                    return dir * 2 + (int)offset;
                } else if (type == 2) {
                    // do a bit of upsampling
                    return dir * 2;
                }
            } else {
                // corner case
                // always at the same index, since there can only be one chunk there anyways...
                return 18;
            }

            return -1;
        }
    }
}