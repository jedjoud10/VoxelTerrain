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

        [ReadOnly]
        public NativeList<TerrainLoader> loaders;

        public int maxDepth;

        public void Execute() {
            NativeQueue<OctreeNode> pending = new NativeQueue<OctreeNode>(Allocator.Temp);
            pending.Enqueue(root);

            while (pending.TryDequeue(out OctreeNode node)) {
                if (ShouldSubdivide(ref node) && node.depth < maxDepth) {
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

        private bool ShouldSubdivide(ref OctreeNode node) {
            // TODO: implement clustering algorithm to make this faster...
            foreach (TerrainLoader loader in loaders) {
                // pls add me back...
                float factor = math.clamp(loader.octreeNodeFactor + 1, 1f, 2f);

                // clamp to the root node
                float3 clamped = math.clamp(loader.position, root.Bounds.Min, root.Bounds.Max);

                if ((math.distance(node.Center, clamped)) < factor * node.size) {
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
                    atMaxDepth = (node.depth + 1) == maxDepth,
                };

                pending.Enqueue(child);
                nodes.Add(child);
                neighbourMasks.Add(new BitField32(0));
            }

            nodes[node.index] = node;
        }
    }
}