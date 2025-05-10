using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Plastic.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, OptimizeFor = OptimizeFor.Performance)]
    public unsafe struct StitchQuadLoToHiJob : IJobParallelFor {
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
        public NativeArray<int> indices;

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

        [WriteOnly]
        public Unsafe.NativeCounter.Concurrent indexCounter;
        public NativeList<float4>.ParallelWriter debugData;
        [ReadOnly]
        public NativeArray<float3> vertices;

        // Fetches the vertex index of a vertex in a specific position
        // If this crosses the v=130 boundary, use the neighbouring chunks' negative boundary indices instead
        private int GetVertexIndex(uint3 hiPos) {
            //Debug.Log($"GetVertexIndex, pos = {position}, state = {neighbourIndices.state.Value}");
            if (math.any(hiPos >= 129)) {
                int unpackedNeighbourIndex = StitchUtils.FetchUnpackedNeighbourIndex(hiPos / 2, neighbourIndices.state);

                // totally fine!
                if (unpackedNeighbourIndex == -1) {
                    //Debug.Log("at0");
                    return int.MaxValue;
                }

                int indexOffset = indexOffsets[unpackedNeighbourIndex];

                // weird chunk thing
                if (indexOffset == -1) {
                    throw new Exception("Index offset was not set! Means that the stitcher believes there's a chunk here when there really isn't...");
                }


                int index = -1;
                unsafe {
                    if (TryFindThings(hiPos, ref neighbourIndices, out int val, true)) {
                        index = val;
                    } else {
                        index = -1;
                    }
                }

                // it's fine if this happens, just means that we didn't find proper neighbours (happens for the chunks on the map edge)
                if (index == -1) {
                    //Debug.Log("at1");
                    return int.MaxValue;
                }

                // COULD HAPPEN, AND IT IS VERY BAD IF IT DOES!!!!
                // means that we are thinking we "think" there's a valid vertex there, but it isn't actually there!
                // 12:22 AM note: There is actually a very specific literal-edge case that occurs that is unfortunately tricked by this. I haven't thought of a way of getting around it yet
                // I thought that maybe we can just create an extra vertex by averaging out the requesting vertices' position (since it's always a quad or a tri), but unfortunately the edge case
                // pops up as a tri with only 2 valid verts (last one is the missing one) meaning that we can't really "average" out the vertex by requesters. idk how this would work across chunks too.
                // needs something more robust and deterministic (maybe voxel value vertex generator DURING stitching as fallback)
                if (index == int.MaxValue || index < 0) {
                    //Debug.Log("at2");
                    return int.MaxValue;
                }

                //Debug.Log($"NEIGHBOUR!! unpackedNeighbourIndex = {unpackedNeighbourIndex}, indexOffset = {indexOffset}, index = {index}");
                return index + indexOffset;
            } else {
                // since src is at LOD1 we must downsample hiPos
                uint3 clamped = math.clamp(hiPos, 0, 127);
                int sourceIndex = srcBoundaryIndices[StitchUtils.PosToBoundaryIndex((clamped) / 2, 64)];
                //Debug.Log($"SOURCE!!! {sourceIndex}");
                return sourceIndex;
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

        // Check and edge and check if we must generate a quad/tri in its forward facing direction
        void CheckEdge(uint3 loPos, uint3 hiPos, int index, int index2) {
            uint3 forward = quadForwardDirection[index];

            // check neighbour's negative boundary voxels using hiPos
            // need to convert hiPos (0 - 130) to chunk local (0 - 65)
            bool flip = true;
            //Debug.Log("caller");

            
            if (TryFindThings(hiPos, ref neighbourVoxels, out Voxel startVoxel, false) && TryFindThings(hiPos + forward, ref neighbourVoxels, out Voxel endVoxel, false)) {
                //Debug.Log($"bloated, {startVoxel.density}, {endVoxel.density}");
                flip = endVoxel.density > 0.0;

                if (startVoxel.density > 0 == endVoxel.density > 0)
                    return;
                
            } else {
                return;
            }

            //float3 pos = ((float3)hiPos / 2.0f) + 0.5f * (float3)forward;
            //debugData[index2] = new float4(pos, 100.0f);

            uint3 offset = hiPos + forward - 1;
            int vertex0 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4]);
            int vertex1 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4 + 1]);
            int vertex2 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4 + 2]);
            int vertex3 = GetVertexIndex(offset + quadPerpendicularOffsets[index * 4 + 3]);
            int4 v = new int4(vertex0, vertex1, vertex2, vertex3);

            
            if (math.cmax(v) == int.MaxValue) {
                debugData.AddNoResize(new float4((float3)hiPos / 2f, index + 1));
                for (int i = 0; i < 4; i++) {
                    if (v[i] != int.MaxValue) {
                        int idx = v[i];
                        debugData.AddNoResize(new float4(vertices[idx], -1f));
                    }
                }
            }

            StitchUtils.AddQuadsOrTris(flip, v, ref indexCounter, ref indices);
        }

        private static bool TryFindThings<T>(uint3 hiPos, ref VoxelStitch.GenericBoundaryData<T> data, out T val, bool samplingIndices) where T: unmanaged {
            val = default(T);
            //Debug.Log($"coords: {hiPos}");
            //hiPos = math.clamp(hiPos, 0, 127);

            int neighbouringSampleSize = 0;

            if (samplingIndices) {
                // sampling indices
                neighbouringSampleSize = 64;
            } else {
                // sampling voxels
                neighbouringSampleSize = 65;
            }

            if (StitchUtils.TryFindBoundaryInfo(hiPos, data.state, 128, out StitchUtils.BoundaryInfo info)) {
                if (info.type == StitchUtils.BoundaryType.Plane) {
                    var lod0Neighbours = data.planes[info.direction].lod0s;

                    uint2 flattenToFaceHi = StitchUtils.FlattenToFaceRelative(hiPos, info.direction);
                    int offset = VoxelUtils.PosToIndex2D(flattenToFaceHi / 64, 2);
                    //Debug.Log(offset);
                    T* voxels = data.planes[info.direction].lod0s[offset];

                    uint3 unflattened = StitchUtils.UnflattenFromFaceRelative(flattenToFaceHi % 64, info.direction);
                    T voxel = *(voxels + StitchUtils.PosToBoundaryIndex(unflattened, neighbouringSampleSize, true));
                    val = voxel;
                    return true;
                } else if (info.type == StitchUtils.BoundaryType.Edge) {
                    var lod0Neighbours = data.edges[info.direction].lod0s;

                    uint flattenToEdgeHi = StitchUtils.FlattenToEdgeRelative(hiPos, info.direction);
                    uint offset = flattenToEdgeHi / 64;

                    if (lod0Neighbours.Length == 0) {
                        Debug.Log(hiPos);
                    }

                    Debug.Log(lod0Neighbours.Length);
                    T* voxels = lod0Neighbours[(int)offset];

                    uint3 unflattened = StitchUtils.UnflattenFromEdgeRelative(flattenToEdgeHi % 64, info.direction);
                    T voxel = *(voxels + StitchUtils.PosToBoundaryIndex(unflattened, neighbouringSampleSize, true));
                    val = voxel;
                    return true;
                } else {
                    return false;
                }
            }

            return false;
        }

        // Executed StitchUtils.CalculateBoundaryLength(65*2) times
        public void Execute(int index) {            
            uint3 hiPos = StitchUtils.BoundaryIndexToPos(index, 130);

            // hold up I'm downsampled
            uint3 loPos = hiPos / 2;

            // if we are not dealing with LoToHi (the intended use case) just quit early
            // TODO: can probably optimize this with IJobParallelForBatch since we do planes first then edges then corner
            if (StitchUtils.TryFindBoundaryInfo(hiPos, neighbourIndices.state, 128, out StitchUtils.BoundaryInfo info)) {
                if (info.mode != StitchUtils.StitchingMode.LoToHi) {
                    return;
                }

                if (info.type == StitchUtils.BoundaryType.Edge) {
                    Debug.Log(neighbourVoxels.edges[info.direction].lod0s.Length);
                }
            } else {
                return;
            }

            if (math.any(hiPos < 1))
                return;

            for (int i = 0; i < 3; i++) {
                // need this to create tris that might be on the v=0 boundary
                bool skipnation = math.any(hiPos < (1 - quadForwardDirection[i]));

                // need this to create tris that might be on the v=128 boundary
                bool skipnation2 = hiPos[i] > 127;

                if (skipnation || skipnation2)
                    continue;
                
                CheckEdge(loPos, hiPos, i, index);
            }
        }

        /*
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
                return -1;
            } else if (type == 3) {
                if (neighbourIndices.edges[dir].vanilla) {
                    edged = StitchUtils.UnflattenFromEdgeRelative(axis + neighbourIndices.edges[dir].relativeOffsetVanilla * 64, dir);
                    int* ptrs = neighbourIndices.edges[dir].lod1;

                    int3 downsampled = (int3)edged / 2;
                    int val = -1;
                    val = *(ptrs + StitchUtils.PosToBoundaryIndex((uint3)downsampled, 64, true));
                    if (val != int.MaxValue) {
                        return val;
                    }

                    // fallback 1
                    if (StitchUtils.LiesOnBoundary(downsampled + basis, 64)) {
                        val = *(ptrs + StitchUtils.PosToBoundaryIndex((uint3)(downsampled + basis), 64, true));

                        if (val != int.MaxValue) {
                            return val;
                        }
                    }

                    // fallback 2
                    if (StitchUtils.LiesOnBoundary(downsampled - basis, 64)) {
                        val = *(ptrs + StitchUtils.PosToBoundaryIndex((uint3)(downsampled - basis), 64, true));

                        if (val != int.MaxValue) {
                            return val;
                        }
                    }

                    //Debug.LogError("sorry bro...");
                    return int.MaxValue;

                } else {
                    uint2 offset = neighbourIndices.edges[dir].relativeOffsetNonVanilla * 64;
                    uint3 actOffset = StitchUtils.UnflattenFromFaceRelative(offset, neighbourIndices.edges[dir].nonVanillaPlaneDir);
                    uint3 yetAnotherOffset = StitchUtils.UnflattenFromEdgeRelative(axis, dir);

                    int* ptrs = neighbourIndices.edges[dir].lod1;
                    int3 downsampled = ((int3)actOffset + (int3)yetAnotherOffset) / 2;
                    int val = *(ptrs + StitchUtils.PosToBoundaryIndex((uint3)downsampled, 64, true));

                    if (val != int.MaxValue) {
                        return val;
                    }

                    // fallback 1
                    if (StitchUtils.LiesOnBoundary(downsampled + basis, 64)) {
                        val = *(ptrs + StitchUtils.PosToBoundaryIndex((uint3)(downsampled + basis), 64, true));

                        if (val != int.MaxValue) {
                            return val;
                        }
                    }

                    // fallback 2
                    if (StitchUtils.LiesOnBoundary(downsampled - basis, 64)) {
                        val = *(ptrs + StitchUtils.PosToBoundaryIndex((uint3)(downsampled - basis), 64, true));

                        if (val != int.MaxValue) {
                            return val;
                        }
                    }

                    return int.MaxValue;
                }
            }

            throw new Exception("Invalid edge type");
        }
        

        // type=1 -> uniform (normal)
        // type=2 -> lotohi (upsample)
        // type=3 -> hitolo (downsample)
        private unsafe int SampleCornerUsingType(uint3 paddingPosition, uint type) {
            return -1;
        }
        */

        /*
        public unsafe int FetchIndexMultiResolution(uint3 paddingPosition) {
            // 1=plane, 2=edge, 3=corner
            bool3 bool3 = paddingPosition == 129;
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

                return -1;
                //return SampleEdgeUsingType(paddingPosition, type, dir);
            } else {
                // corner case
                uint type = neighbourIndices.state.GetBits(12, 2);

                if (type == 0)
                    return -1;

                return -1;
                //return SampleCornerUsingType(paddingPosition, type);
            }

            return -1;
        }
        */
    }
}