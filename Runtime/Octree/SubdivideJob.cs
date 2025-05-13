using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    [BurstCompile(CompileSynchronously = true)]
    public struct SubdivideJob : IJob {
        public NativeList<OctreeNode> nodes;
        public NativeQueue<OctreeNode> pending;
        public NativeList<BitField32> neighbourMasks;

        [ReadOnly]
        public OctreeLoader.Target target;

        [ReadOnly] public int maxDepth;

        public void Execute() {
            while (pending.TryDequeue(out OctreeNode node)) {
                // AABB bounds method
                //bool subdivide = node.Bounds.Overlaps(targetBounds);

                float3 clamped = math.clamp(target.center, nodes[0].Bounds.Min, nodes[0].Bounds.Max);

                // relative distance method
                bool subdivide = math.distance(node.Center, clamped) < target.radius * node.size;

                if (subdivide && node.depth < maxDepth) {
                    Subdivide(node, true);
                }
            }
        }

        private void Subdivide(OctreeNode node, bool enqueue) {
            node.childBaseIndex = nodes.Length;

            for (int i = 0; i < 8; i++) {
                float3 offset = math.float3(VoxelUtils.OCTREE_CHILD_OFFSETS[i]);
                OctreeNode child = new OctreeNode {
                    position = offset * (node.size / 2.0F) + node.position,
                    depth = node.depth + 1,
                    size = node.size / 2,
                    parentIndex = node.index,
                    index = node.childBaseIndex + i,
                    childBaseIndex = -1,
                };

                if (enqueue) {
                    pending.Enqueue(child);
                }

                nodes.Add(child);
                neighbourMasks.Add(new BitField32(0));
            }

            nodes[node.index] = node;
        }
    }
}