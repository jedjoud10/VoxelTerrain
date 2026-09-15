using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain.Octree {
    [BurstCompile(CompileSynchronously = true)]
    public struct DiffJob : IJob {
        [ReadOnly]
        public NativeHashSet<OctreeNode> src1;

        [ReadOnly]
        public NativeHashSet<OctreeNode> src2;

        [WriteOnly]
        public NativeList<OctreeNode> diffedNodes;

        public void Execute() {
            diffedNodes.Clear();

            foreach (var node in src1) {
                if (!src2.Contains(node)) {
                    diffedNodes.Add(node);
                }
            }
        }
    }
}