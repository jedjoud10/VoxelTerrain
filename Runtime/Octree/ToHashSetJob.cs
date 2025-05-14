using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    [BurstCompile(CompileSynchronously = true)]
    public struct ToHashSetJob : IJob {
        [ReadOnly]
        public NativeList<OctreeNode> list;
        [WriteOnly]
        public NativeHashSet<OctreeNode> set;


        public void Execute() {
            set.Clear();
            foreach (var node in list) {
                set.Add(node);
            }
        }
    }
}