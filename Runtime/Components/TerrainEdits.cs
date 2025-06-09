using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    public struct TerrainEdits : IComponentData {
        public NativeHashMap<int3, int> chunkPositionsToChunkEditIndices;
        public UnsafeList<NativeArray<Voxel>> chunkEdits;
        public JobHandle applySystemHandle;
    }
}