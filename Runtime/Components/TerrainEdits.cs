using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    public struct TerrainEdits : IComponentData {
        public NativeList<VoxelData> chunkEdits;
        public NativeHashMap<int3, int> chunkPositionsToChunkEditIndices;
        public NativeList<int3> modifiedChunkEditPositions;
        public JobHandle applySystemHandle;
    }
}