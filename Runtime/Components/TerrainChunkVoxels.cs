using Unity.Entities;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain {
    /// <summary>
    /// Voxel data for a single chunk entity
    /// </summary>
    public struct TerrainChunkVoxels : IComponentData, IEnableableComponent {
        /// <summary>
        /// Underlying SoA voxel data
        /// </summary>
        public VoxelData data;

        /// <summary>
        /// A Job handle depicting the last job that wrote to the voxel data
        /// We must add this as a dependency for any new jobs that read/write the data to avoid errors
        /// </summary>
        public JobHandle asyncWriteJobHandle;

        /// <summary>
        /// A Job handle depicting the last job that read from the voxel data
        /// We must add this as a dependency for any new jobs that read/write the data to avoid errors
        /// </summary>
        public JobHandle asyncReadJobHandle;

        /// <summary>
        /// Is the chunk currently being meshed asynchronously?
        /// </summary>
        public bool meshingInProgress;
    }
}