using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

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

        private bool DoABitOfALookupIykwim(float3 point, ref SpanBackedStack<int> pending, int ogDepth) {
            pending.Clear();
            pending.Enqueue(0);

            while (pending.TryDequeue(out int index)) {
                OctreeNode node = nodes[index];
                if (node.childBaseIndex == -1) {
                    if (node.Bounds.Contains(point) && ogDepth == node.depth) {
                        return true;
                    }
                } else {
                    for (int i = 0; i < 8; i++) {
                        if (nodes[node.childBaseIndex + i].Bounds.Contains(point)) {
                            pending.Enqueue(node.childBaseIndex + i);
                        }
                    }
                }
            }

            return false;
        }

        public void Execute(int index) {
            OctreeNode node = nodes[index];

            Span<int> pendingNodesBacking = stackalloc int[50];
            SpanBackedStack<int> pending = SpanBackedStack<int>.New(pendingNodesBacking);

            BitField32 mask = new BitField32(1 << 13);
            if (node.childBaseIndex == -1) {
                for (int j = 0; j < 27; j++) {
                    if (j == 13) {
                        // it's you! you little sussy baka! ~w~
                        // so silly... :3
                    } else {
                        uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                        int3 offset = (int3)_offset - 1;
                        float3 position = node.Center + (float3)offset * node.size;
                        bool set = DoABitOfALookupIykwim(position, ref pending, node.depth);
                        mask.SetBits(j, set);
                    }
                }
            }

            neighbourMasks[index] = mask;
        }
    }
}