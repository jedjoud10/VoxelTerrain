using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainOctreeSystem))]
    [UpdateBefore(typeof(TerrainManagerSystem))]
    [UpdateAfter(typeof(TerrainEditIncrementalModifySystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TerrainEditRefreshChunksSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainOctree>();
            state.RequireForUpdate<TerrainManager>();
            state.RequireForUpdate<TerrainEdits>();
            state.RequireForUpdate<TerrainReadySystems>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            TerrainEdits backing = SystemAPI.GetSingleton<TerrainEdits>();
            TerrainReadySystems ready = SystemAPI.GetSingleton<TerrainReadySystems>();
            if (!backing.applySystemHandle.IsCompleted || !ready.readback || !ready.manager)
                return;

            TerrainManager manager = SystemAPI.GetSingleton<TerrainManager>();
            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();

            // notify the underlying chunks that contain these values
            // the EditApplySystem automatically applies the modified voxel values to the chunks, so we don't have to worry abt that
            // since we will only modify LOD0 chunks, we don't need to do any LOD related stuff either. you will ALWAYS modify the closest chunk, so they are the ones that need their mesh updated immediately
            for (int i = 0; i < backing.modifiedChunkEditPositions.Length; i++) {
                int3 editChunkPosition = backing.modifiedChunkEditPositions[i];
                OctreeNode node = OctreeNode.LeafLodZeroNode(editChunkPosition, octreeConfig.maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE);

                if (manager.chunks.TryGetValue(node, out Entity entity)) {
                    SystemAPI.SetComponentEnabled<TerrainChunkMesh>(entity, false);
                    SystemAPI.SetComponentEnabled<TerrainChunkRequestCollisionTag>(entity, true);
                    SystemAPI.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(entity, true);
                    SystemAPI.SetComponentEnabled<TerrainChunkRequestMeshingTag>(entity, true);
                    SystemAPI.SetComponentEnabled<TerrainChunkEndOfPipeTag>(entity, false);
                    SystemAPI.SetComponentEnabled<TerrainChunkVoxels>(entity, true);
                    SystemAPI.SetComponentEnabled<TerrainChunkRequestReadbackTag>(entity, false);
                    SystemAPI.SetComponentEnabled<TerrainChunkRequestLightingTag>(entity, true);
                    SystemAPI.GetComponentRW<TerrainChunkRequestMeshingTag>(entity).ValueRW.deferredVisibility = false;
                }
            }
        }
    }
}