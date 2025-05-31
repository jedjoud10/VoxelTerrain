using Unity.Collections;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Segments {
    public struct TerrainSegmentPermPropLookup : IComponentData {
        // we can support up to 32 prop types...
        public FixedList128Bytes<int> offsetsList;
        public FixedList128Bytes<int> countsList;
    }
}