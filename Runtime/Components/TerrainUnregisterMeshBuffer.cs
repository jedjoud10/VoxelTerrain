using Unity.Entities;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    public struct TerrainUnregisterMeshBuffer : IBufferElementData {
        public BatchMeshID meshId;
    }
}