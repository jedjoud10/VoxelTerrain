using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public struct TerrainReadbackConfig : IComponentData {
        public bool skipEmptyChunks;
    }
}