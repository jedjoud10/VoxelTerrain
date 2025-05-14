using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain.Octree {
    [BurstCompile(CompileSynchronously = true)]
    public struct SwapJob : IJob {
        [ReadOnly]
        public NativeHashSet<OctreeNode> src;

        public NativeHashSet<OctreeNode> dst;

        public void Execute() {
            dst.Clear();
            foreach (var item in src) {
                dst.Add(item);
            }
        }
    }
}