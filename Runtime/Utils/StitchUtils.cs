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

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void DebugCheckBounds(uint3 coordinates, int size) {
            if (math.cmax(coordinates) >= size) {
                throw new System.OverflowException(
                    $"An element of coordinates {coordinates} is larger than the maximum {size-1} (size={size})");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void DebugCheckIndex(int index, int size) {
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

        // type=3 -> uniform (normal)
        // type=2 -> lotohi (downsample)
        // type=1 -> hitolo (upsample)
        private unsafe static T SamplePlaneUsingType<T>(uint3 paddingPosition, uint type, int dir, ref VoxelStitch.GenericBoundaryData<T> data, T notFound = default) where T: unmanaged {
            uint3 flatPosition = paddingPosition;
            flatPosition[dir] = 0;

            if (type == 1) {
                // do a bit of simple copying
                T* ptr = data.planes[dir].uniform;
                T val = *(ptr + PosToBoundaryIndex(flatPosition, 64, true));
                return val;
            } else if (type == 2) {
                throw new Exception("We should not upsample!!!!!");
                /*
                // do a bit of downsampling
                uint2 flattened = FlattenToFaceRelative(paddingPosition, dir);
                int offset = VoxelUtils.PosToIndex2D(flattened / 32, 2);
                T* ptr = data.planes[dir].lod0s[offset];
                T val = *(ptr + PosToBoundaryIndex((flatPosition * 2) % 64, 64, true));
                return val;
                */
            } else if (type == 3) {
                // do a bit of upsampling
                uint2 flattened = FlattenToFaceRelative(paddingPosition, dir);
                flattened += data.planes[dir].relativeOffset * 64;
                flatPosition = UnflattenFromFaceRelative(flattened, dir);
                T* ptr = data.planes[dir].lod1;
                Debug.Log(flatPosition / 2);
                T val = *(ptr + PosToBoundaryIndex(flatPosition / 2, 64, true));
                return val;
            }

            throw new Exception("Invalid plane type");
        }

        // type=1 -> uniform (normal)
        // type=2 -> lotohi (downsample)
        // type=3 -> hitolo (upsample)
        private unsafe static T SampleEdgeUsingType<T>(uint3 paddingPosition, uint type, int dir, ref VoxelStitch.GenericBoundaryData<T> data, T notFound = default) where T: unmanaged {
            (uint3 edged, uint axis) = FetchAxisAndKeepOnEdging(paddingPosition[dir], dir);

            if (type == 1) {
                // do a bit of simple copying
                T* ptrs = data.edges[dir].uniform;
                T val = *(ptrs + PosToBoundaryIndex(edged, 64, true));
                return val;
            } else if (type == 2) {
                throw new Exception("We should not upsample!!!!!");
                /*
                // do a bit of downsampling
                uint offset = axis / 32;
                T* ptrs = data.edges[dir].lod0s[(int)offset];
                T val = *(ptrs + PosToBoundaryIndex((edged * 2) % 64, 64, true));
                return val;
                */
            } else if (type == 3) {
                if (data.edges[dir].vanilla) {
                    edged = UnflattenFromEdgeRelative(axis + data.edges[dir].relativeOffsetVanilla * 64, dir);
                    T* ptrs = data.edges[dir].lod1;
                    T val = *(ptrs + PosToBoundaryIndex(edged / 2, 64, true));
                    Debug.Log($"positioned: {edged / 2} valued: {val}");

                    val = *(ptrs + PosToBoundaryIndex(edged / 2 + new uint3(0, 1, 0), 64, true));
                    Debug.Log($"positioned2: plus one, valued: {val}");

                    val = *(ptrs + PosToBoundaryIndex(edged / 2 - new uint3(0, 1, 0), 64, true));
                    Debug.Log($"positioned2: minus one, valued: {val}");

                    return val;
                } else {
                    Debug.Log($"dir={dir}, planeDir={data.edges[dir].nonVanillaPlaneDir}");
                    uint2 offset = data.edges[dir].relativeOffsetNonVanilla * 64;
                    Debug.Log($"offset={offset}");
                    uint3 actOffset = UnflattenFromFaceRelative(offset, data.edges[dir].nonVanillaPlaneDir);
                    Debug.Log($"actOffset={actOffset}");
                    uint3 yetAnotherOffset = UnflattenFromEdgeRelative(axis, dir);
                    Debug.Log($"yetAnotherOffset={yetAnotherOffset}");


                    Debug.Log((actOffset + yetAnotherOffset) / 2);
                    T* ptrs = data.edges[dir].lod1;
                    T val = *(ptrs + PosToBoundaryIndex((actOffset + yetAnotherOffset) / 2, 64, true));
                    return val;
                }

                /*
                // do a bit of upsampling
                uint offset = axis + data.edges[dir].relativeOffset * 64;
                edged = UnflattenFromEdgeRelative(offset, dir);
                
                // given edge direction, offset the edge on the flat space spanned by vectors perpendicular to the direction
                
                
                T* ptrs = data.edges[dir].lod1;
                Debug.Log(edged / 2);
                T val = *(ptrs + PosToBoundaryIndex(edged / 2, 64, true));
                return val;
                */
            }

            throw new Exception("Invalid edge type");
        }

        // type=1 -> uniform (normal)
        // type=2 -> lotohi (downsample)
        // type=3 -> hitolo (upsample)
        private unsafe static T SampleCornerUsingType<T>(uint3 paddingPosition, uint type, ref VoxelStitch.GenericBoundaryData<T> data, T notFound = default) where T: unmanaged {
            return notFound;
            /*
            // Corner piece always at (0,0,0), but that's the last element in our padding array
            T* ptr = null;
            if (type == 1) {
                ptr = data.corner.uniform;
            } else if (type == 2) {
                //ptr = data.corner.lod0;
                throw new Exception("We should not upsample!!!!!");
            } else if (type == 3) {
                return notFound;
                //throw new Exception("Need to implement the different configuration for downsampled corner piece");
                //ptr = data.corner.lod1;
            } else {
                throw new Exception("Invalid corner type");
            }

            return *(ptr + 63 * 63 * 3 + 63 * 3);
            */
        }

        /*
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
                int offset = VoxelUtils.PosToIndex2D(flattened / 32, 2);
                Voxel* voxels = data.planes[dir].lod0s[offset];
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
        public static T Sample<T>(uint3 paddingPosition, ref VoxelStitch.GenericBoundaryData<T> data) {
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
        }
        */

        /*
        // Calculates the type of of the boundary (plane, edge, corner)
        // Also returns the type and extra data (offset) in case it's downsampling
        public static int BoundaryType(uint3 position, BitField32 state, out int direction, out int downsamplingOffset) {
            // 1=plane, 2=edge, 3=corner
            bool3 bool3 = position == 64;
            int bitmask = math.bitmask(new bool4(bool3, false));
            int bitsSet = math.countbits(bitmask);

            if (bitsSet == 1) {
                // check which axis is set
                int dir = math.tzcnt(bitmask);
                uint type = state.GetBits(dir * 2, 2);
                direction = dir;
                downsamplingOffset = -1;
                
                if (type == 1) {
                    // do a bit of simple copying
                    return dir * 4;
                } else if (type == 2) {
                    // do a bit of downsampling
                    uint2 flattened = FlattenToFaceRelative(paddingPosition, dir);
                    int offset = VoxelUtils.PosToIndex2D(flattened / 32, 2);
                    return dir * 4 + offset;
                } else if (type == 3) {
                    // do a bit of upsampling
                    return dir * 4;
                }
            } else if (bitsSet == 2) {
                // check which axis is NOT set
                int inv = (~bitmask) & 0b111;
                int dir = math.tzcnt(inv);
                direction = dir;
                uint type = state.GetBits(dir * 2 + 6, 2);

                if (type == 1) {
                    // do a bit of simple copying
                    return dir * 2 + 12;
                } else if (type == 2) {
                    // do a bit of downsampling
                    (uint3 edged, uint axis) = FetchAxisAndKeepOnEdging(paddingPosition[dir], dir);
                    uint offset = axis / 32;
                    return dir * 2 + (int)offset;
                } else if (type == 3) {
                    // do a bit of upsampling
                    return dir * 2;
                }
            } else {
                direction = -1;
                downsamplingOffset = -1;
                // corner case
                // always at the same index, since there can only be one chunk there anyways...
                return 2;
            }

            return -1;
        }
        */

        // Fetch a thing at the given size=65 padding position
        // Requires the neighbour stuff from the stitcher to be calculated and converted to raw pointers
        // Handles downsampling/upsampling of data automatically
        public static T Sample<T>(uint3 paddingPosition, ref VoxelStitch.GenericBoundaryData<T> data, T notFound = default) where T: unmanaged {
            // 1=plane, 2=edge, 3=corner
            bool3 bool3 = paddingPosition == 64;
            int bitmask = math.bitmask(new bool4(bool3, false));
            int bitsSet = math.countbits(bitmask);

            if (bitsSet == 1) {
                // check which axis is set
                int dir = math.tzcnt(bitmask);
                uint type = data.state.GetBits(dir * 2, 2);

                if (type == 0)
                    return notFound;

                return SamplePlaneUsingType(paddingPosition, type, dir, ref data, notFound);
            } else if (bitsSet == 2) {
                // check which axis is NOT set
                int inv = (~bitmask) & 0b111;
                int dir = math.tzcnt(inv);
                uint type = data.state.GetBits(dir * 2 + 6, 2);

                if (type == 0)
                    return notFound;

                return SampleEdgeUsingType(paddingPosition, type, dir, ref data, notFound);
            } else {
                // corner case
                uint type = data.state.GetBits(12, 2);

                if (type == 0)
                    return notFound;

                return SampleCornerUsingType(paddingPosition, type, ref data, notFound);
            }
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

                if (type == 1) {
                    // do a bit of simple copying
                    return dir * 4;
                } else if (type == 2) {
                    // do a bit of downsampling
                    uint2 flattened = FlattenToFaceRelative(paddingPosition, dir);
                    int offset = VoxelUtils.PosToIndex2D(flattened / 32, 2);
                    return dir * 4 + offset;
                } else if (type == 3) {
                    // do a bit of upsampling
                    return dir * 4;
                }
            } else if (bitsSet == 2) {
                // check which axis is NOT set
                int inv = (~bitmask) & 0b111;
                int dir = math.tzcnt(inv);
                uint type = state.GetBits(dir * 2 + 6, 2);

                if (type == 1) {
                    // do a bit of simple copying
                    return dir * 2 + 12;
                } else if (type == 2) {
                    // do a bit of downsampling
                    (uint3 edged, uint axis) = FetchAxisAndKeepOnEdging(paddingPosition[dir], dir);
                    uint offset = axis / 32;
                    return dir * 2 + (int)offset + 12;
                } else if (type == 3) {
                    // do a bit of upsampling
                    return dir * 2 + 12;
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