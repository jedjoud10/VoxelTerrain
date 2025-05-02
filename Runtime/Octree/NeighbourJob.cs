using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    // pwease multithread me... owo
    [BurstCompile(CompileSynchronously = true)]
    public struct NeighbourJob : IJob {
        public NativeList<OctreeNode> nodes;
        public NativeQueue<OctreeNode> pending;

        public NativeList<int> neighbours;

        public void Execute() {
            for (int i = 0; i < nodes.Length; i++) {
                OctreeNode node = nodes[i];

                if (node.childBaseIndex == -1) {
                    node.neighbourDataStartIndex = neighbours.Length;
                    for (int j = 0; j < 27; j++) {
                        if (j == 13) {
                            // it's you! you little sussy baka! ~w~
                            // so silly... :3
                            neighbours.Add(-1);
                        } else {
                            uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                            int3 offset = (int3)_offset - 1;
                            float3 position = node.Center + (float3)offset * node.size;
                            int index = DoABitOfALookupIykwim(position);
                            neighbours.Add(index);
                        }
                    }
                } else {
                    node.neighbourDataStartIndex = -1;
                }
                
                nodes[i] = node;
            }
        }

        // really stupid neighbour lookup system
        // but I can't be bothered with writing one that handles diagnoals and multi-res neighbour
        // sorgy...
        // TODO: unshittify
        private int DoABitOfALookupIykwim(float3 point) {
            pending.Clear();
            pending.Enqueue(nodes[0]);

            while (pending.TryDequeue(out OctreeNode node)) {
                if (node.childBaseIndex == -1) {
                    if (node.Bounds.Contains(point)) {
                        return node.index;
                    }
                } else {
                    for (int i = 0; i < 8; i++) {
                        OctreeNode child = nodes[node.childBaseIndex + i];

                        if (child.Bounds.Contains(point)) {
                            pending.Enqueue(child);
                        }
                    }
                }
            }

            return -1;
        }
    }
}