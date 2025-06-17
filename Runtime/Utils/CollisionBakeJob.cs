using UnityEngine;
using Unity.Jobs;
using Unity.Burst;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct CollisionBakeJob : IJob {
        public int meshId;
        public void Execute() {
            Physics.BakeMesh(meshId, false);
        }
    }
}
