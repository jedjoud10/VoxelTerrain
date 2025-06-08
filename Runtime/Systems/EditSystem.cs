using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Edits {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct EditSystem : ISystem {
        private NativeHashMap<int3, int> chunkPositionsToChunkEditIndices;
        private UnsafeList<NativeArray<Voxel>> chunkEdits;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            chunkPositionsToChunkEditIndices = new NativeHashMap<int3, int>(0, Allocator.Persistent);
            chunkEdits = new UnsafeList<NativeArray<Voxel>>(0, Allocator.Persistent);
            state.RequireForUpdate<TerrainOctree>();
            state.RequireForUpdate<TerrainOctreeConfig>();
            state.RequireForUpdate<TerrainEditConfig>();

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainEditNeedsToBeApplied, TerrainEdit, LocalToWorld>().Build();
            state.RequireForUpdate(query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainEditNeedsToBeApplied, TerrainEdit, LocalToWorld>().Build();
            NativeArray<Unity.Mathematics.Geometry.MinMaxAABB> aabbs = new NativeArray<Unity.Mathematics.Geometry.MinMaxAABB>(query.CalculateEntityCount(), Allocator.TempJob);
            state.EntityManager.RemoveComponent<TerrainEditNeedsToBeApplied>(query);

            NativeArray<LocalToWorld> transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

            for (int i = 0; i < transforms.Length; i++) {
                aabbs[i] = Unity.Mathematics.Geometry.MinMaxAABB.CreateFromCenterAndExtents(transforms[i].Position, 10);
            }

            int previousCount = chunkPositionsToChunkEditIndices.Count;

            NativeList<int3> modifiedChunkEditsPosition = new NativeList<int3>(Allocator.TempJob);
            CreateEditChunksFromBoundsJob job = new CreateEditChunksFromBoundsJob {
                boundsArray = aabbs,
                chunkPositionsToChunkEditIndices = chunkPositionsToChunkEditIndices,
                modifiedChunkEditPositions = modifiedChunkEditsPosition
            };

            job.Schedule().Complete();

            int addedCount = chunkPositionsToChunkEditIndices.Count - previousCount;

            chunkEdits.AddReplicate(default, addedCount);
            for (int i = 0; i < addedCount; i++) {
                chunkEdits[previousCount + i] = new NativeArray<Voxel>(VoxelUtils.VOLUME, Allocator.Persistent);
            }

            for (int i = 0; i < modifiedChunkEditsPosition.Length; i++) {
                int3 chunkPosition = modifiedChunkEditsPosition[i];
                NativeArray<Voxel> voxels = chunkEdits[chunkPositionsToChunkEditIndices[chunkPosition]];
                voxels.AsSpan().Fill(Voxel.Empty);
            }


            TerrainOctreeConfig octreeConfig = SystemAPI.GetSingleton<TerrainOctreeConfig>();
            ref TerrainOctree octree = ref SystemAPI.GetSingletonRW<TerrainOctree>().ValueRW;
            ref TerrainManager manager = ref SystemAPI.GetSingletonRW<TerrainManager>().ValueRW;
            if (OctreeUtils.TryRecurseCheckMultipleAABB(ref octree, aabbs, out OctreeUtils.RecruseResults results)) {
                results.handle.Complete();

                foreach (int intersectingNodeIndex in results.intersecting) {
                    OctreeNode node = octree.nodes[intersectingNodeIndex];
                    Entity chunk = manager.chunks[node];
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            chunkPositionsToChunkEditIndices.Dispose();

            foreach (var editData in chunkEdits) {
                editData.Dispose();
            }

            chunkEdits.Dispose();
        }
    }
}