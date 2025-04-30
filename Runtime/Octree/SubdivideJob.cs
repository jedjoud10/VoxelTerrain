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

        [ReadOnly]
        public OctreeLoader.Target target;

        [ReadOnly] public int maxDepth;

        public void Execute() {
            while (pending.TryDequeue(out OctreeNode node)) {
                MinMaxAABB targetBounds = MinMaxAABB.CreateFromCenterAndHalfExtents(target.center, target.radius);
                bool subdivide = node.Bounds.Overlaps(targetBounds);

                if (subdivide && node.depth < maxDepth) {
                    Subdivide(node);
                }
            }

            // go through each node and subdivide again if we can
            // makes us able to take advantage of the octal readback stuff from GPU
            int copy = nodes.Length;
            for (int i = 0; i < copy; i++) {
                OctreeNode node = nodes[i];
                if (node.childBaseIndex == -1 && node.depth < maxDepth) {
                    Subdivide(node);
                }
            }
        }

        private void Subdivide(OctreeNode node) {
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

                pending.Enqueue(child);
                nodes.Add(child);
            }

            //Debug.Log(node.index);
            //Debug.Log(nodes.Length);
            nodes[node.index] = node;
        }
    }
}