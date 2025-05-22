using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Octree {
    [BurstCompile(CompileSynchronously = true)]
    public struct SubdivideJob : IJob {
        public NativeList<OctreeNode> nodes;
        public NativeList<BitField32> neighbourMasks;
        public OctreeNode root;

        public float3 center;
        public float radius;

        [ReadOnly] public int maxDepth;

        public void Execute() {
            NativeQueue<OctreeNode> pending = new NativeQueue<OctreeNode>(Allocator.Temp);
            pending.Enqueue(root);

            while (pending.TryDequeue(out OctreeNode node)) {
                // AABB bounds method
                //bool subdivide = node.Bounds.Overlaps(targetBounds);

                float3 clamped = math.clamp(center, nodes[0].Bounds.Min, nodes[0].Bounds.Max);

                // relative distance method
                bool subdivide = math.distance(node.Center, clamped) < radius * node.size;

                if (subdivide && node.depth < maxDepth) {
                    Subdivide(node, ref pending);
                }
            }
        }


        // Offsets used for octree generation
        // Also mortonated!!!
        public static readonly int3[] OCTREE_CHILD_OFFSETS = new int3[] {
            new int3(0, 0, 0),
            new int3(1, 0, 0),
            new int3(0, 1, 0),
            new int3(1, 1, 0),
            new int3(0, 0, 1),
            new int3(1, 0, 1),
            new int3(0, 1, 1),
            new int3(1, 1, 1),
        };

        private void Subdivide(OctreeNode node, ref NativeQueue<OctreeNode> pending) {
            node.childBaseIndex = nodes.Length;

            for (int i = 0; i < 8; i++) {
                int3 offset = OCTREE_CHILD_OFFSETS[i];
                OctreeNode child = new OctreeNode {
                    position = offset * (node.size / 2) + node.position,
                    depth = node.depth + 1,
                    size = node.size / 2,
                    parentIndex = node.index,
                    index = node.childBaseIndex + i,
                    childBaseIndex = -1,
                };

                pending.Enqueue(child);
                nodes.Add(child);
                neighbourMasks.Add(new BitField32(0));
            }

            nodes[node.index] = node;
        }
    }
}