using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    public struct TerrainEdits : IComponentData {
        // stores the modified LOD0 voxel data for chunk edits.
        public NativeList<VoxelData> chunkEdits;
        
        // stores a mapping between 3D space and the index used within chunkEdit
        public NativeHashMap<int3, int> chunkPositionsToChunkEditIndices;

        // USED ONLY TEMPORARILY WITHIN A SINGLE TERRAIN TICK
        // used to detect what chunks (actual chunks) must be have the edits applied to them and remeshed
        // this changes from tick to tick as the number of edits per tick is extremely low (since they get removed afterward)
        public NativeList<int3> modifiedChunkEditPositions;
        public JobHandle applySystemHandle;
    }
}