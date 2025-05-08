using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Plastic.Antlr3.Runtime;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    // NOTE!!!!!
    // There are cases where the stitching fails and leaves some gaps behind
    // I know this happens and I think I know why this occurs but I can't manage to replicate it again

    // for now, assume uniformity
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, OptimizeFor = OptimizeFor.Performance)]
    public unsafe struct StitchQuadJob : IJobParallelFor {
        public NativeArray<float4> debugData;
        
        // Source boundary voxels
        [ReadOnly]
        public NativeArray<Voxel> srcBoundaryVoxels;

        // Source boundary indices
        [ReadOnly]
        public NativeArray<int> srcBoundaryIndices;

        // Neighbouring boundary voxels
        [ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public VoxelStitch.GenericBoundaryData<Voxel> neighbourVoxels;

        // Neighbouring boundary vertex indices
        [ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public VoxelStitch.GenericBoundaryData<int> neighbourIndices;

        // Index offsets since we merged all the vertices in a contiguous manner
        [ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public NativeArray<int> indexOffsets;

        // Triangles that we generated
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> triangles;
        [ReadOnly]
        static readonly uint3[] quadForwardDirection = new uint3[3]
        {
            new uint3(1, 0, 0),
            new uint3(0, 1, 0),
            new uint3(0, 0, 1),
        };

        // Quad vertices offsets based on direction
        [ReadOnly]
        static readonly uint3[] quadPerpendicularOffsets = new uint3[12]
        {
            new uint3(0, 0, 0),
            new uint3(0, 1, 0),
            new uint3(0, 1, 1),
            new uint3(0, 0, 1),

            new uint3(0, 0, 0),
            new uint3(0, 0, 1),
            new uint3(1, 0, 1),
            new uint3(1, 0, 0),

            new uint3(0, 0, 0),
            new uint3(1, 0, 0),
            new uint3(1, 1, 0),
            new uint3(0, 1, 0)
        };
        
        // Quad Counter
        [WriteOnly]
        public Unsafe.NativeCounter.Concurrent counter;

        // Fetches the vertex index of a vertex in a specific position
        // If this crosses the v=65 boundary, use the neighbouring chunks' negative boundary indices instead
        private int GetVertexIndex(uint3 position) {
            /*
            if (neighbourIndices.state.Value != 14325) {
                return int.MaxValue;
            }
            */

            //Debug.Log($"GetVertexIndex, pos = {position}, state = {neighbourIndices.state.Value}");
            if (math.any(position >= 64)) {
                int unpackedNeighbourIndex = StitchUtils.FetchUnpackedNeighbourIndex(position, neighbourIndices.state);
                
                // totally fine!
                if (unpackedNeighbourIndex == -1) {
                    return int.MaxValue;
                }
                
                int indexOffset = indexOffsets[unpackedNeighbourIndex];

                // weird chunk thing
                if (indexOffset == -1) {
                    throw new Exception("Index offset was not set! Means that the stitcher believes there's a chunk here when there really isn't...");
                }
            

                int index = -1;
                unsafe {
                    index = FetchIndexMultiResolution(position);
                }

                // it's fine if this happens, just means that we didn't find proper neighbours (happens for the chunks on the map edge)
                if (index == -1) {
                    return int.MaxValue;
                }

                // COULD HAPPEN, AND IT IS VERY BAD IF IT DOES!!!!
                // means that we are thinking we "think" there's a valid vertex there, but it isn't actually there!
                if (index == int.MaxValue || index < 0) {
                    Debug.LogError("notto good at allu");
                    return int.MaxValue;
                }

                //Debug.Log($"NEIGHBOUR!! unpackedNeighbourIndex = {unpackedNeighbourIndex}, indexOffset = {indexOffset}, index = {index}");
                return index + indexOffset;
            } else {
                //Debug.Log($"SOURCE!!!");
                return srcBoundaryIndices[StitchUtils.PosToBoundaryIndex(position, 64)];
            }
        }

        static readonly int3[] DEDUPE_TRIS_THING = new int3[] {
            new int3(0, 2, 3), // x/y
            new int3(0, 1, 3), // x/z
            new int3(0, 1, 2), // x/w
            new int3(0, 1, 3), // y/z
            new int3(0, 1, 2), // y/w
            new int3(0, 1, 2), // z/w
        };

        // Check and edge and check if we must generate a quad in it's forward facing direction
        void CheckEdge(uint3 basePosition, int index) {
            uint3 forward = quadForwardDirection[index];

            // When we implement multi-res, swap between the two sets here
            // Either sample from pos-boundary of self, or neg-boundary of neighbours
            Voxel startVoxel = srcBoundaryVoxels[StitchUtils.PosToBoundaryIndex(basePosition, 65)];
            Voxel endVoxel = srcBoundaryVoxels[StitchUtils.PosToBoundaryIndex(basePosition + forward, 65)];

            if (startVoxel.density > 0 == endVoxel.density > 0)
                return;

            int bitsset = math.countbits(math.bitmask(new bool4(basePosition == new uint3(64), false)));
            float data = bitsset == 2 ? 0.2f : 0f;
            float3 pos = (float3)basePosition + 0.5f * (float3)forward;
            debugData[StitchUtils.PosToBoundaryIndex(basePosition, 65)] = new float4(pos, 0.0f);

            /*
            if (bitsset != 2)
                return;
            */

            bool flip = (endVoxel.density > 0.0);

            uint3 offset = basePosition + forward - 1;

            int vertex0 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4]);
            int vertex1 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4 + 1]);
            int vertex2 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4 + 2]);
            int vertex3 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4 + 3]);
            int4 v = new int4(vertex0, vertex1, vertex2, vertex3);

            // Don't make a quad if the vertices are invalid
            if ((vertex0 | vertex1 | vertex2 | vertex3) == int.MaxValue) {
                //Debug.LogError($"what: {what}");
                debugData[StitchUtils.PosToBoundaryIndex(basePosition, 65)] = new float4(pos, 0.2f);
                return;
            }

            // Ts gpt-ed kek
            int dupeType = 0;
            dupeType |= math.select(0, 1, v.x == v.y);
            dupeType |= math.select(0, 2, v.x == v.z);
            dupeType |= math.select(0, 4, v.x == v.w);
            dupeType |= math.select(0, 8, v.y == v.z && v.x != v.y);
            dupeType |= math.select(0, 16, v.y == v.w && v.x != v.y && v.z != v.y);
            dupeType |= math.select(0, 32, v.z == v.w && v.x != v.z && v.y != v.z);

            // means that there are more than 2 duplicate verts, not possible?
            if (math.countbits(dupeType) > 1) {
                return;
            }

            if (dupeType == 0) {
                int triIndex = counter.Add(6);

                // Set the first tri
                triangles[triIndex + (flip ? 0 : 2)] = vertex0;
                triangles[triIndex + 1] = vertex1;
                triangles[triIndex + (flip ? 2 : 0)] = vertex2;

                // Set the second tri
                triangles[triIndex + (flip ? 3 : 5)] = vertex2;
                triangles[triIndex + 4] = vertex3;
                triangles[triIndex + (flip ? 5 : 3)] = vertex0;
            } else {
                int config = math.tzcnt(dupeType);
                int triIndex = counter.Add(3);
                int3 remapper = DEDUPE_TRIS_THING[config];
                triangles[triIndex + (flip ? 0 : 2)] = v[remapper[0]];
                triangles[triIndex + 1] = v[remapper[1]];
                triangles[triIndex + (flip ? 2 : 0)] = v[remapper[2]];
            }


            /*
            if (count == 4) {
                int triIndex = counter.Add(6);

                // Set the first tri
                triangles[triIndex + (flip ? 0 : 2)] = vertex0;
                triangles[triIndex + 1] = vertex1;
                triangles[triIndex + (flip ? 2 : 0)] = vertex2;

                // Set the second tri
                triangles[triIndex + (flip ? 3 : 5)] = vertex2;
                triangles[triIndex + 4] = vertex3;
                triangles[triIndex + (flip ? 5 : 3)] = vertex0;
            } else if (count == 3) {
                int triIndex = counter.Add(3);

                triangles[triIndex + (flip ? 0 : 2)] = vertex0;
                triangles[triIndex + 1] = vertex1;
                triangles[triIndex + (flip ? 2 : 0)] = vertex2;
            } else {
                // What...
            }
            */

            /*
            int4 what = new int4(vertex0, vertex1, vertex2, vertex3);
            if (math.any(what > 500000 | what < 0)) {
                Debug.LogError($"what: {what}");
                debugData[StitchUtils.PosToBoundaryIndex(basePosition, 65)] = new float4(pos, 0.2f);
            }
            */


        }

        // Excuted for each cell within the grid
        public void Execute(int index) {
            // When we implement multi-res, swap between the two sets here
            // Either sample from pos-boundary of self, or neg-boundary of neighbours
            uint3 position = StitchUtils.BoundaryIndexToPos(index, 65);
            
            for (int i = 0; i < 3; i++) {
                // need this to create tris that might be on the v=0 boundary
                bool skipnation = math.any(position < (1 - quadForwardDirection[i]));

                // need this to create tris that might be on the v=63 boundary
                bool skipnation2 = position[i] > 63;

                if (skipnation || skipnation2)
                    continue;

                CheckEdge(position, i);
            }
        }

        // type=1 -> uniform (normal)
        // type=2 -> lotohi (upsample)
        // type=3 -> hitolo (downsample)
        private unsafe int SamplePlaneUsingType(uint3 paddingPosition, uint type, int dir) {
            uint3 flatPosition = paddingPosition;
            flatPosition[dir] = 0;

            if (type == 1) {
                // do a bit of simple copying
                int* ptr = neighbourIndices.planes[dir].uniform;
                int val = *(ptr + StitchUtils.PosToBoundaryIndex(flatPosition, 64, true));
                return val;
            } else if (type == 2) {
                throw new Exception("We should not upsample!!!!!");
            } else if (type == 3) {
                // do a bit of upsampling
                uint2 flattened = StitchUtils.FlattenToFaceRelative(paddingPosition, dir);
                flattened += neighbourIndices.planes[dir].relativeOffset * 64;
                flatPosition = StitchUtils.UnflattenFromFaceRelative(flattened, dir);
                int* ptr = neighbourIndices.planes[dir].lod1;
                //Debug.Log(flatPosition / 2);
                int val = *(ptr + StitchUtils.PosToBoundaryIndex(flatPosition / 2, 64, true));
                return val;
            }

            throw new Exception("Invalid plane type");
        }

        // type=1 -> uniform (normal)
        // type=2 -> lotohi (upsample)
        // type=3 -> hitolo (downsample)
        private unsafe int SampleEdgeUsingType(uint3 paddingPosition, uint type, int dir) {
            (uint3 edged, uint axis) = StitchUtils.FetchAxisAndKeepOnEdging(paddingPosition[dir], dir);

            int3 basis = new int3(0);
            basis[dir] = 1;

            if (type == 1) {
                // do a bit of simple copying
                int* ptrs = neighbourIndices.edges[dir].uniform;
                int val = *(ptrs + StitchUtils.PosToBoundaryIndex(edged, 64, true));
                return val;
            } else if (type == 2) {
                throw new Exception("We should not upsample!!!!!");
            } else if (type == 3) {
                if (neighbourIndices.edges[dir].vanilla) {
                    edged = StitchUtils.UnflattenFromEdgeRelative(axis + neighbourIndices.edges[dir].relativeOffsetVanilla * 64, dir);
                    int* ptrs = neighbourIndices.edges[dir].lod1;

                    int3 downsampled = (int3)edged / 2;

                    int val = *(ptrs + StitchUtils.PosToBoundaryIndex(edged / 2, 64, true));
                    //Debug.Log($"positioned: {edged / 2} valued: {val}");

                    if (val != int.MaxValue) {
                        return val;
                    } else if (StitchUtils.LiesOnBoundary(downsampled + basis, 64)) {
                        // fallback 1
                        val = *(ptrs + StitchUtils.PosToBoundaryIndex(+new uint3(0, 1, 0), 64, true));
                        //Debug.Log($"positioned2: plus one, valued: {val}");
                        return val;
                    } else if (StitchUtils.LiesOnBoundary(downsampled - basis, 64)) {
                        // fallback 2
                        val = *(ptrs + StitchUtils.PosToBoundaryIndex((uint3)(downsampled - basis), 64, true));
                        //Debug.Log($"positioned2: minus one, valued: {val}");
                        return val;
                    }

                    //Debug.LogError("sorry bro...");
                    return int.MaxValue;

                } else {
                    //Debug.Log($"dir={dir}, planeDir={neighbourIndices.edges[dir].nonVanillaPlaneDir}");
                    uint2 offset = neighbourIndices.edges[dir].relativeOffsetNonVanilla * 64;
                    //Debug.Log($"offset={offset}");
                    uint3 actOffset = StitchUtils.UnflattenFromFaceRelative(offset, neighbourIndices.edges[dir].nonVanillaPlaneDir);
                    //Debug.Log($"actOffset={actOffset}");
                    uint3 yetAnotherOffset = StitchUtils.UnflattenFromEdgeRelative(axis, dir);
                    //Debug.Log($"yetAnotherOffset={yetAnotherOffset}");


                    //Debug.Log((actOffset + yetAnotherOffset) / 2);
                    int* ptrs = neighbourIndices.edges[dir].lod1;
                    int val = *(ptrs + StitchUtils.PosToBoundaryIndex((actOffset + yetAnotherOffset) / 2, 64, true));
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
        // type=2 -> lotohi (upsample)
        // type=3 -> hitolo (downsample)
        private unsafe int SampleCornerUsingType(uint3 paddingPosition, uint type) {
            return -1;
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

        // Fetch a vertex index (neighbour chunk relative) at the given size=65 padding position
        // Handles downsampling of data automatically. Upsampling is a no-no since we should just run the stuff in the other direction for that
        public unsafe int FetchIndexMultiResolution(uint3 paddingPosition) {
            // 1=plane, 2=edge, 3=corner
            bool3 bool3 = paddingPosition == 64;
            int bitmask = math.bitmask(new bool4(bool3, false));
            int bitsSet = math.countbits(bitmask);

            if (bitsSet == 1) {
                // check which axis is set
                int dir = math.tzcnt(bitmask);
                uint type = neighbourIndices.state.GetBits(dir * 2, 2);

                if (type == 0)
                    return -1;

                return SamplePlaneUsingType(paddingPosition, type, dir);
            } else if (bitsSet == 2) {
                // check which axis is NOT set
                int inv = (~bitmask) & 0b111;
                int dir = math.tzcnt(inv);
                uint type = neighbourIndices.state.GetBits(dir * 2 + 6, 2);

                if (type == 0)
                    return -1;

                return SampleEdgeUsingType(paddingPosition, type, dir);
            } else {
                // corner case
                uint type = neighbourIndices.state.GetBits(12, 2);

                if (type == 0)
                    return -1;

                return SampleCornerUsingType(paddingPosition, type);
            }
        }
    }
}