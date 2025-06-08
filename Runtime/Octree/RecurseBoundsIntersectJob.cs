using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;


namespace jedjoud.VoxelTerrain.Octree {
    [BurstCompile(CompileSynchronously = true)]
    public struct RecurseBoundsIntersectJob : IJob {
        [ReadOnly]
        public NativeList<OctreeNode> nodes;
        [ReadOnly]
        public NativeArray<Unity.Mathematics.Geometry.MinMaxAABB> boundsArray;
        public NativeList<int> intersecting;


        public void Execute() {
            NativeQueue<int> pending = new NativeQueue<int>(Allocator.Temp);
            pending.Enqueue(0);

            while (pending.TryDequeue(out int nodeIndex)) {
                OctreeNode node = nodes[nodeIndex]; 
                Unity.Mathematics.Geometry.MinMaxAABB nodeBounds = node.Bounds;

                bool overlaps = false;

                foreach (var bound in boundsArray) {
                    if (nodeBounds.Overlaps(bound)) {
                        overlaps = true;
                        break;
                    }
                }
                
                if (overlaps) {
                    if (node.childBaseIndex != -1) {
                        for (int i = 0; i < 8; i++) {
                            pending.Enqueue(i + node.childBaseIndex);
                        }
                    }

                    intersecting.Add(nodeIndex);
                }
            }
        }
    }
}