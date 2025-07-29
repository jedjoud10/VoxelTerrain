using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainOctreeSystem))]
    [UpdateBefore(typeof(TerrainManagerSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TerrainEditIncrementalModifySystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainOctree>();
            state.RequireForUpdate<TerrainManager>();
            state.RequireForUpdate<TerrainEdits>();
            state.RequireForUpdate<TerrainReadySystems>();
            state.RequireForUpdate<TerrainEdit>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            ref TerrainEdits backing = ref SystemAPI.GetSingletonRW<TerrainEdits>().ValueRW;
            TerrainReadySystems ready = SystemAPI.GetSingleton<TerrainReadySystems>();
            if (!backing.applySystemHandle.IsCompleted || !ready.readback || !ready.manager)
                return;
            backing.applySystemHandle.Complete();

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainEditBounds, TerrainEdit>().Build();
            NativeArray<MinMaxAABB> aabbs = query.ToComponentDataArray<TerrainEditBounds>(Allocator.Temp).Reinterpret<MinMaxAABB>();
            TerrainManager manager = SystemAPI.GetSingleton<TerrainManager>();
            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();

            // create edit chunks that will contain modified chunk data
            NativeHashSet<int3> intersecting = new NativeHashSet<int3>(0, Allocator.Temp);
            foreach (var bound in aabbs) {
                int3 min = (int3)math.floor(bound.Min / (float)VoxelUtils.PHYSICAL_CHUNK_SIZE);
                int3 max = (int3)math.floor(bound.Max / (float)VoxelUtils.PHYSICAL_CHUNK_SIZE);

                for (int z = min.z; z <= max.z; z++) {
                    for (int y = min.y; y <= max.y; y++) {
                        for (int x = min.x; x <= max.x; x++) {
                            int3 chunkPos = new int3(x, y, z);
                            intersecting.Add(chunkPos);
                        }
                    }
                }
            }

            // detect NEW intersecting edit chunks, the ones that we have just added
            NativeList<int3> newIntersecting = new NativeList<int3>(Allocator.Temp);
            foreach (var what in intersecting) {
                if (!backing.chunkPositionsToChunkEditIndices.ContainsKey(what)) {
                    newIntersecting.Add(what);
                }
            }

            // add chunk edit backing native arrays (using LOD 0 chunks' voxel data as source)
            int newChunkEditIndirectIndex = backing.chunkEdits.Length;
            for (int i = 0; i < newIntersecting.Length; i++) {
                int3 chunkPosOnly = newIntersecting[i];
                OctreeNode node = OctreeNode.LeafLodZeroNode(chunkPosOnly, octreeConfig.maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE);

                // check if the chunk edit represents and LOD0 chunk that we can use for its source voxels
                if (manager.chunks.TryGetValue(node, out Entity chunk)) {
                    VoxelData editVoxels = new VoxelData(Allocator.Persistent);
                    TerrainChunkVoxels chunkVoxels = SystemAPI.GetComponent<TerrainChunkVoxels>(chunk);
                    editVoxels.CopyFrom(chunkVoxels.data);

                    // add a new chunk edit array to the world. this will be stored indefinitely
                    backing.chunkEdits.Add(editVoxels);
                    backing.chunkPositionsToChunkEditIndices.Add(chunkPosOnly, newChunkEditIndirectIndex);
                    newChunkEditIndirectIndex++;
                } else {
                    Debug.LogWarning("Tried accessing chunk that does not exist or non LOD0 chunk. Uh oh...");
                }
            }

            // keep track of the modified chunkedit positions so that we can execute the edits afterwards
            backing.modifiedChunkEditPositions.AddRange(intersecting.ToNativeArray(Allocator.Temp));
        }
    }
}