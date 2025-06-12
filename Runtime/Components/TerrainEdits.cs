using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    public class TerrainEdits : IComponentData {
        public NativeHashMap<int3, int> chunkPositionsToChunkEditIndices;
        public UnsafeList<VoxelData> chunkEdits;
        public JobHandle applySystemHandle;
        public EditTypeRegistry registry;
    }
}