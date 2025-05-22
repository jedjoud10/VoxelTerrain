using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    public struct UnregisterMeshCleanup : ICleanupComponentData {
        public BatchMeshID meshId;
    }
}