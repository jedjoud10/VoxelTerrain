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
        [NativeDisableParallelForRestriction]
        public NativeArray<int> neighbourIndices;

        public NativeCounter.Concurrent counter;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<OctreeOmnidirectionalNeighbourData> omnidirectionalNeighbourData;

        public static readonly int3[] DIRECTIONS = new int3[] {
            new int3(-1, 0, 0),
            new int3(0, -1, 0),
            new int3(0, 0, -1),
            new int3(1, 0, 0),
            new int3(0, 1, 0),
            new int3(0, 0, 1),
        };

        public static MinMaxAABB CreatePlane(float3 min, float3 max, int dir) {
            int axii = dir % 3;

            // so that we don't trigger literal "edge" cases
            min += 1f;
            max -= 1f;

            // flatten in the direction of the axis
            float value = 0f;
            if (dir < 3) {
                value = min[dir % 3];
            } else {
                value = max[dir % 3];
            }
            value += dir < 3 ? -4f : 4f;

            // thin stripping time
            min[axii] = value-0.1f;
            max[axii] = value+0.1f;
            return new MinMaxAABB(min, max);
        }

        private void TraverseAndCollect(MinMaxAABB bounds, ref SpanBackedStack<ushort> pending, ref SpanBackedStack<ushort> neighbours, int omniDirIndex, int ogDepth) {
            pending.Clear();
            neighbours.Clear();
            pending.Enqueue(0);

            while (pending.TryDequeue(out ushort index)) {
                OctreeNode node = nodes[index];
                for (int c = 0; c < 8; c++) {
                    OctreeNode child = nodes[node.childBaseIndex + c];
                    if (child.Bounds.Overlaps(bounds)) {
                        if (child.childBaseIndex == -1) {
                            neighbours.Enqueue((ushort)child.index);
                        } else {
                            pending.Enqueue((ushort)child.index);
                        }
                    }
                }
            }

            if (neighbours.Length == 0) {
                return;
            } else if (neighbours.Length == 1) {
                OctreeNode neighbour = nodes[neighbours[0]];
                omnidirectionalNeighbourData[omniDirIndex] = new OctreeOmnidirectionalNeighbourData {
                    mode = ogDepth == neighbour.depth ? OctreeOmnidirectionalNeighbourData.Mode.SameLod : OctreeOmnidirectionalNeighbourData.Mode.HigherLod,
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
            } else {
                Debug.LogError($"should not happen, {neighbours.Length}");
            }
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

            Span<ushort> pendingNodesBacking = stackalloc ushort[100];
            SpanBackedStack<ushort> pending = SpanBackedStack<ushort>.New(pendingNodesBacking);

            Span<ushort> neighboursBacking = stackalloc ushort[4];
            SpanBackedStack<ushort> neighbours = SpanBackedStack<ushort>.New(neighboursBacking);

            // planes only. 
            for (int i = 0; i < 6; i++) {
                // convert to 0-26 index for the omni directional data
                int omniDirectionalIndex = VoxelUtils.PosToIndex((uint3)(DIRECTIONS[i] + 1), 3);
                int omniDirectionalIndexBase = original.neighbourDataBaseIndex;

                MinMaxAABB bounds = original.Bounds;
                float3 min = bounds.Min;
                float3 max = bounds.Max;
                MinMaxAABB plane = CreatePlane(min, max, i);
                TraverseAndCollect(plane, ref pending, ref neighbours, omniDirectionalIndex + omniDirectionalIndexBase, original.depth);
            }
        }
    }
}