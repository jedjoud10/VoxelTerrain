using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Octree {
    [BurstCompile(CompileSynchronously = true)]
    public struct SubdivideJob : IJob {
        public NativeList<OctreeNode> nodes;
        public NativeList<BitField32> neighbourMasks;
        public OctreeNode root;

        public NativeList<float3> loaders;

        [ReadOnly] public int maxDepth;

        public void Execute() {
            NativeQueue<OctreeNode> pending = new NativeQueue<OctreeNode>(Allocator.Temp);
            pending.Enqueue(root);

            while (pending.TryDequeue(out OctreeNode node)) {
                if (InRangeOfLoaders(ref node) && node.depth < maxDepth) {
                    Subdivide(node, ref pending);
                }
            }
        }

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

        private bool InRangeOfLoaders(ref OctreeNode node) {
            // TODO: implement clustering algorithm to make this faster...
            foreach (float3 center in loaders) {
                // pls add me back...
                float factor = math.clamp(1.8f, 1f, 2f);

                // clamp to the root node
                float3 clamped = math.clamp(center, nodes[0].Bounds.Min, nodes[0].Bounds.Max);

                if (math.distance(node.Center, clamped) < factor * node.size) {
                    return true;
                }
            }

            return false;
        }

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