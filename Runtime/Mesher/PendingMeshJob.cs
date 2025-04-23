using System;

namespace jedjoud.VoxelTerrain.Meshing {
    public struct PendingMeshJob {
        public VoxelChunk chunk;
        public bool collisions;
        public int maxTicks;
        public Action<VoxelChunk> callback;
    }
}