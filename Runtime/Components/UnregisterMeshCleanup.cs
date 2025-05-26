using Unity.Entities;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    public struct UnregisterMeshCleanup : ICleanupComponentData {
        public BatchMeshID meshId;
    }
}