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

        // if we're going in a specific direction index, these are the child indices that we should look out for.
        // if we find these when going up the tree we stop and start going down instead
        public static readonly uint4[] IMPORTANT_CHILD_INDICES_PER_DIRECTION = new uint4[] {
            new uint4(1,3,5,7),
            new uint4(2,3,6,7),
            new uint4(4,5,6,7),
            new uint4(0,2,4,6),
            new uint4(0,1,4,5),
            new uint4(0,1,2,3),
        };

        public int maxDepth;

        private int FlipChildIndexWithDir(int childIndex, int dir) {
            uint3 offset = VoxelUtils.IndexToPosMorton(childIndex);
            offset[dir] = (uint)(1 - (int)offset[dir]);
            return VoxelUtils.PosToIndexMorton(offset);
        }

        public void Execute(int index) {
            // We should never modify this one...
            OctreeNode original = nodes[index];
            //Debug.Log(original);

            // skip if we're the root node, no way we can ever get neighbours for it
            if (index == 0)
                return;

            // also skip if this isn't a leaf node. we only care about leaf nodes
            if (original.childBaseIndex != -1)
                return;
        
            // planes only
            for (int i = 0; i < 6; i++) {
                int3 dir = DIRECTIONS[i];

                OctreeNode current = original;
                OctreeNode parent = nodes[original.parentIndex];

                // convert to 0-26 index for the omni directional data
                int omniDirectionalIndex = VoxelUtils.PosToIndex((uint3)(dir + 1), 3);
                int omniDirectionalIndexBase = original.neighbourDataBaseIndex;

                // keep track of the child offset indices
                Span<int> childIndicesBacking = stackalloc int[maxDepth];
                SpanBackedStack previousChildOffsetIndices = SpanBackedStack.New(childIndicesBacking);

                // go up the tree until we find a node of "interest"
                for (int b = 0; b < maxDepth; b++) {
                    //Debug.Log($"b={b}, node={current.depth}");

                    // exit if we reached the root (meaning we have no neighbour)
                    if (current.parentIndex == -1) {
                        break;
                    }

                    // calculate the index of the node relative to the parent node
                    parent = nodes[current.parentIndex];
                    int childIndex = current.index - parent.childBaseIndex;
                    previousChildOffsetIndices.Enqueue(childIndex);

                    // check if the child index is important for this direction
                    uint4 triggers = IMPORTANT_CHILD_INDICES_PER_DIRECTION[i];
                    bool any = math.any(triggers == new uint4(childIndex));

                    if (any) {
                        // Our sibling is either the neighbour or is the ancestor/parent of one
                        int3 childOffset = VoxelUtils.OCTREE_CHILD_OFFSETS[childIndex];
                        childOffset += dir;
                        int siblingIndex = VoxelUtils.PosToIndexMorton((uint3)childOffset) + parent.childBaseIndex;
                        OctreeNode sibling = nodes[siblingIndex];

                        // if the sibling is a leaf node then that's our neighbour
                        if (sibling.childBaseIndex == -1) {
                            omnidirectionalNeighbourData[omniDirectionalIndexBase + omniDirectionalIndex] = new OctreeOmnidirectionalNeighbourData {
                                mode = sibling.depth == original.depth ? OctreeOmnidirectionalNeighbourData.Mode.SameLod : OctreeOmnidirectionalNeighbourData.Mode.HigherLod,
                                baseIndex = siblingIndex,
                            };
                        } else {
                            //Debug.Log("amogus");

                            current = nodes[sibling.index];

                            // if not, start going down the tree in the opposite direction STARTING FROM THE PARENT
                            // also need to keep a queue of pending nodes to visit (children). needed for multi res.
                            Span<int> backingForwardProp = stackalloc int[30];
                            SpanBackedStack pendingNodeIndices = SpanBackedStack.New(backingForwardProp);
                            pendingNodeIndices.Enqueue(siblingIndex);

                            // Worst case, we have 4 neighbours. Always assume 2:1 ratio
                            Span<int> backingNeighbours = stackalloc int[4];
                            SpanBackedStack neighbours = SpanBackedStack.New(backingNeighbours);

                            // we can skip a lot of the steps if we do the child index shit in reverse but also flipped about the direction
                            previousChildOffsetIndices.TryDequeue(out int _);
                            //Debug.Log($"begin = {current}");
                            while (previousChildOffsetIndices.TryDequeue(out int childIndex2)) {
                                // we found a leaf node early, meaning this is the neighbour
                                if (current.childBaseIndex == -1) {
                                    //Debug.Log($"add = {current}");
                                    neighbours.Enqueue(current.index);
                                    break;
                                }

                                int flipped = FlipChildIndexWithDir(childIndex2, i % 3);
                                current = nodes[current.childBaseIndex + flipped];
                            }

                            // either it's of the same LOD or a higher LOD
                            if (neighbours.Length == 1) {
                                if (nodes[neighbours[0]].depth == original.depth) {
                                    omnidirectionalNeighbourData[omniDirectionalIndexBase + omniDirectionalIndex] = new OctreeOmnidirectionalNeighbourData {
                                        mode = OctreeOmnidirectionalNeighbourData.Mode.SameLod,
                                        baseIndex = neighbours[0],
                                    };
                                    break;
                                } else if ((nodes[neighbours[0]].depth) == (original.depth-1)) {
                                    omnidirectionalNeighbourData[omniDirectionalIndexBase + omniDirectionalIndex] = new OctreeOmnidirectionalNeighbourData {
                                        mode = OctreeOmnidirectionalNeighbourData.Mode.HigherLod,
                                        baseIndex = neighbours[0],
                                    };
                                    break;
                                } else {
                                    Debug.Log($"other={nodes[neighbours[0]].depth}, src={original.depth}");
                                    Debug.LogError("Bro... where's that 2:1 ratio you were talking about??? :skull:");
                                }
                            }

                            // since we assume 2:1 ratio, the node that we're currently at contains 4 of our neighbours (LOD0 whilst we are at LOD1)
                            for (int c = 0; c < 8; c++) {
                                if (math.any(triggers == new uint4(c))) {
                                    neighbours.Enqueue(current.childBaseIndex + c);
                                }
                            }

                            int baseIndex = counter.Add(4);
                            omnidirectionalNeighbourData[omniDirectionalIndexBase + omniDirectionalIndex] = new OctreeOmnidirectionalNeighbourData {
                                mode = OctreeOmnidirectionalNeighbourData.Mode.LowerLod,
                                baseIndex = baseIndex,
                            };

                            for (int c = 0; c < 4; c++) {
                                neighbourIndices[baseIndex + c] = neighbours[c];
                            }

                            /*
                            // mr white I don't feel so good... auuyuyuyuuuuugh
                            for (int f = 0; f < 10000; f++) {
                                if (pendingNodeIndices.TryDequeue(out int idx)) {
                                    OctreeNode child = nodes[idx];
                                    Debug.Log("test: " + child);

                                    // we reached a leaf, add it to the neighbours...
                                    if (child.childBaseIndex == -1) {
                                        Debug.Log("add: " + child);


                                        // MY SHIT BREAKS HERE!!! WHY ARE THERE MULTIPLE CHILDREN????
                                        neighbours.Enqueue(idx);
                                        
                                        // if it's the same depth or higher LOD then we can quit, since we know we'll only have one at max
                                        if (child.depth == original.depth || child.depth < original.depth) {
                                            break;
                                        }
                                    } else {
                                        // add the node's children that go in the opposite direction
                                        // we can tell which ones those are with the tagging thing
                                        for (int c = 0; c < 8; c++) {
                                            if (!math.any(triggers == new uint4(c))) {
                                                pendingNodeIndices.Enqueue(child.childBaseIndex + c);
                                            }
                                        }
                                    }
                                } else {
                                    break;
                                }
                            }


                            */

                        }
                    } else {
                        // keep going up the tree...
                        current = parent;
                    }
                }
            }
        }
    }
}