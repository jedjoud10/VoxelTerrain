using System;
using jedjoud.VoxelTerrain.Unsafe;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    [BurstCompile(CompileSynchronously = true)]
    public struct NeighbourJob : IJobParallelForDefer {
        [ReadOnly]
        public NativeList<OctreeNode> nodes;

        [WriteOnly]
        public NativeArray<BitField32> neighbourMasks;

        public static readonly int3[] DIRECTIONS = new int3[] {
            new int3(-1, 0, 0),
            new int3(0, -1, 0),
            new int3(0, 0, -1),
            new int3(1, 0, 0),
            new int3(0, 1, 0),
            new int3(0, 0, 1),
        };

        public static MinMaxAABB CreatePlane(float3 min, float3 max, int signedDir) {
            int axii = signedDir % 3;

            // so that we don't trigger literal "edge" cases
            min += 1f;
            max -= 1f;

            // flatten in the direction of the axis
            float value = 0f;
            if (signedDir < 3) {
                value = min[signedDir % 3];
            } else {
                value = max[signedDir % 3];
            }
            value += signedDir < 3 ? -4f : 4f;

            // thin stripping time
            min[axii] = value-0.1f;
            max[axii] = value+0.1f;
            return new MinMaxAABB(min, max);
        }

        public static MinMaxAABB CreateEdge(float3 min, float3 max, int axii, int localEdgeIndex) {
            // so that we don't trigger literal "edge" cases
            min += 1f;
            max -= 1f;
            float size = max.x - min.x;

            // calculate edge local position
            uint2 mortoned = VoxelUtils.IndexToPos2D(localEdgeIndex, 2);
            float2 offset = (float2)mortoned * (size + 8) - 4;

            if (axii == 0) {
                offset += min.yz;
            } else if (axii == 1) {
                offset += min.xz;
            } else if (axii == 2) {
                offset += min.xy;
            }

            // flatten the bounds into a single edge
            if (axii == 0) {
                min.yz = offset;
                max.yz = offset;
            } else if (axii == 1) {
                min.xz = offset;
                max.xz = offset;
            } else if (axii == 2) {
                min.xy = offset;
                max.xy = offset;
            }

            return new MinMaxAABB(min, max);
        }

        public static MinMaxAABB CreateCorner(float3 min, float3 max, int corner) {
            // expand the bounds so we can fetch corners
            min -= 1f;
            max += 1f;

            float3 position = min;
            float size = max.x - min.x;

            uint3 mortoned = VoxelUtils.IndexToPos(corner, 2);

            float3 point = position + size * (float3)mortoned;
            return MinMaxAABB.CreateFromCenterAndHalfExtents(point, 0.5f);
        }

        private bool CheckIfNeighbourIsSameLod(MinMaxAABB bounds, ref SpanBackedStack<ushort> pending, int ogDepth) {
            pending.Clear();
            pending.Enqueue(0);

            BitField32 mask = new BitField32(0);
            while (pending.TryDequeue(out ushort index)) {
                OctreeNode node = nodes[index];
                for (int c = 0; c < 8; c++) {
                    OctreeNode child = nodes[node.childBaseIndex + c];
                    if (child.Bounds.Overlaps(bounds)) {
                        if (child.childBaseIndex == -1) {
                            if (ogDepth == child.depth) {
                                return true;
                            } else {
                                return false;
                            }
                        } else {
                            pending.Enqueue((ushort)child.index);
                        }
                    }
                }
            }

            return false;

            /*
            if (neighbours.Length == 0) {
                return;
            } else if (neighbours.Length == 1) {
                OctreeNode neighbour = nodes[neighbours[0]];
                OctreeOmnidirectionalNeighbourData.Mode mode = default;

                if (ogDepth == neighbour.depth) {
                    mode = OctreeOmnidirectionalNeighbourData.Mode.SameLod;
                } else if ((ogDepth - 1) == neighbour.depth) {
                    mode = OctreeOmnidirectionalNeighbourData.Mode.HigherLod;
                } else if ((ogDepth + 1) == neighbour.depth) {
                    mode = OctreeOmnidirectionalNeighbourData.Mode.LowerLod;
                } else {
                    Debug.Log("nice 2:1 ratio you got there kek");
                }

                omnidirectionalNeighbourData[omniDirIndex] = new OctreeOmnidirectionalNeighbourData {
                    mode = mode,
                    baseIndex = (int)neighbours[0],
                };
            } else if (neighbours.Length == 4) {
                int baseIndex = counter.Add(4);
                omnidirectionalNeighbourData[omniDirIndex] = new OctreeOmnidirectionalNeighbourData {
                    mode = OctreeOmnidirectionalNeighbourData.Mode.LowerLod,
                    baseIndex = baseIndex,
                };

                for (int c = 0; c < 4; c++) {
                    neighbourIndices[baseIndex + c] = neighbours[c];
                }
            } else if (neighbours.Length == 2) {
                int baseIndex = counter.Add(2);
                omnidirectionalNeighbourData[omniDirIndex] = new OctreeOmnidirectionalNeighbourData {
                    mode = OctreeOmnidirectionalNeighbourData.Mode.LowerLod,
                    baseIndex = baseIndex,
                };
                for (int c = 0; c < 2; c++) {
                    neighbourIndices[baseIndex + c] = neighbours[c];
                }
            } else {
                Debug.LogError($"should not happen, {neighbours.Length}");
            }
            */
        }



        public void Execute(int index) {
            // We should never modify this one...
            OctreeNode original = nodes[index];

            // skip if we're the root node, no way we can ever get neighbours for it
            if (index == 0)
                return;

            // also skip if this isn't a leaf node. we only care about leaf nodes
            if (original.childBaseIndex != -1)
                return;

            Span<ushort> pendingNodesBacking = stackalloc ushort[50];
            SpanBackedStack<ushort> pending = SpanBackedStack<ushort>.New(pendingNodesBacking);

            MinMaxAABB bounds = original.Bounds;
            float3 min = bounds.Min;
            float3 max = bounds.Max;

            BitField32 mask = new BitField32(0);

            // planes only. 
            for (int i = 0; i < 6; i++) {
                int bitMaskIndex = VoxelUtils.PosToIndex((uint3)(DIRECTIONS[i] + 1), 3);
                MinMaxAABB plane = CreatePlane(min, max, i);
                bool set = CheckIfNeighbourIsSameLod(plane, ref pending, original.depth);
                mask.SetBits(bitMaskIndex, set);
            }

            // edges only. 
            for (int i = 0; i < 3; i++) {
                int3 dir = DIRECTIONS[i+3];

                // for each axis we have 4 edges
                for (int l = 0; l < 4; l++) {
                    uint2 mortoned = VoxelUtils.IndexToPos2D(l, 2) * 2;
                    uint3 offset = 1;

                    if (i == 0) {
                        offset.yz = mortoned;
                    } else if (i == 1) {
                        offset.xz = mortoned;
                    } else if (i == 2) {
                        offset.xy = mortoned;
                    }

                    int bitMaskIndex = VoxelUtils.PosToIndex(offset, 3);
                    MinMaxAABB edge = CreateEdge(min, max, i, l);
                    bool set = CheckIfNeighbourIsSameLod(edge, ref pending, original.depth);
                    mask.SetBits(bitMaskIndex, set);
                }
            }

            // corners only
            for (int i = 0; i < 8; i++) {
                uint3 mortoned = VoxelUtils.IndexToPos(i, 2) * 2;
                MinMaxAABB corner = CreateCorner(min, max, i);

                int bitMaskIndex = VoxelUtils.PosToIndex(mortoned, 3);
                bool set = CheckIfNeighbourIsSameLod(corner, ref pending, original.depth);
                mask.SetBits(bitMaskIndex, set);
            }

            //neighbourMasks[original.index] = mask;
        }
    }
}