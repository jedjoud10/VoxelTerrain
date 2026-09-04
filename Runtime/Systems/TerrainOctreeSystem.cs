using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain.Octree {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TerrainOctreeSystem : ISystem {
        private NativeHashSet<OctreeNode> oldNodesSet;
        private NativeHashSet<OctreeNode> newNodesSet;
        private NativeList<TerrainLoader> loaders;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainLoader>();
            state.RequireForUpdate<TerrainOctreeConfig>();
            state.RequireForUpdate<TerrainManagerConfig>();

            oldNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);
            newNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);
            loaders = new NativeList<TerrainLoader>(0, Allocator.Persistent);
            state.EntityManager.CreateSingleton<TerrainOctree>(InitOctree());
        }

        private TerrainOctree InitOctree() {
            return new TerrainOctree {
                nodes = new NativeList<OctreeNode>(Allocator.Persistent),

                added = new NativeList<OctreeNode>(Allocator.Persistent),
                removed = new NativeList<OctreeNode>(Allocator.Persistent),
                neighbourMasks = new NativeList<BitField32>(Allocator.Persistent),
                handle = default,

                continuous = true,
                pending = false,
                readyToSpawn = false,
            };
        }

        private void DisposeOctree(TerrainOctree octree) {
            octree.handle.Complete();
            octree.added.Dispose();
            octree.removed.Dispose();
            octree.nodes.Dispose();
            octree.neighbourMasks.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            TerrainOctreeConfig config = SystemAPI.GetSingleton<TerrainOctreeConfig>();
            int maxDepth = config.maxDepth;

            ref TerrainOctree octree = ref SystemAPI.GetSingletonRW<TerrainOctree>().ValueRW;
            ref TerrainShouldUpdate shouldUpdate = ref SystemAPI.GetSingletonRW<TerrainShouldUpdate>().ValueRW;

            if (octree.handle.IsCompleted && octree.pending) {
                octree.handle.Complete();
                octree.continuous = false;
                octree.pending = false;
                octree.readyToSpawn = true;
                return;
            }

            if (!octree.continuous || !octree.handle.IsCompleted || octree.pending || octree.readyToSpawn) {
                return;
            }

            TerrainReadySystems ready = SystemAPI.GetSingleton<TerrainReadySystems>();

            if (!ready.manager || !ready.mesher || !ready.readback) {
                return;
            }

            if (!shouldUpdate.octree) {
                return;
            }

            shouldUpdate.octree = false;
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainLoader>().Build();
            NativeArray<TerrainLoader> tempLoaders = query.ToComponentDataArray<TerrainLoader>(Allocator.Temp);
            loaders.CopyFrom(tempLoaders);

            octree.nodes.Clear();
            octree.neighbourMasks.Clear();
            octree.added.Clear();
            octree.removed.Clear();
            newNodesSet.Clear();

            OctreeNode root = OctreeNode.RootNode(maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE /* >> (int)terrain.voxelSizeReduction */);
            octree.nodes.Add(root);
            octree.neighbourMasks.Add(new BitField32(0));
            
            SubdivideJob job = new SubdivideJob {
                maxDepth = maxDepth,
                root = root,

                nodes = octree.nodes,
                neighbourMasks = octree.neighbourMasks,
                
                loaders = loaders,
            };

            NeighbourJob neighbourJob = new NeighbourJob {
                nodes = octree.nodes,
                neighbourMasks = octree.neighbourMasks.AsDeferredJobArray(),
            };

            ToHashSetJob toHashSetJob = new ToHashSetJob {
                list = octree.nodes,
                set = newNodesSet,
            };

            DiffJob addedDiffJob = new DiffJob {
                src1 = oldNodesSet,
                src2 = newNodesSet,
                diffedNodes = octree.removed,
            };

            DiffJob removedDiffJob = new DiffJob {
                src1 = newNodesSet,
                src2 = oldNodesSet,
                diffedNodes = octree.added,
            };

            SwapJob swapJob = new SwapJob {
                src = newNodesSet,
                dst = oldNodesSet,
            };

            JobHandle subdivideJobHandle = job.Schedule();
            JobHandle neighbourJobHandle = neighbourJob.Schedule<NeighbourJob, OctreeNode>(octree.nodes, BatchUtils.NEIGHBOUR_BATCH, subdivideJobHandle);

            JobHandle setJobHandle = toHashSetJob.Schedule(neighbourJobHandle);
            JobHandle addedJobHandle = addedDiffJob.Schedule(setJobHandle);
            JobHandle removedJobHandle = removedDiffJob.Schedule(setJobHandle);

            JobHandle temp = JobHandle.CombineDependencies(addedJobHandle, removedJobHandle);
            JobHandle swapJobHandle = swapJob.Schedule(temp);
            octree.handle = JobHandle.CombineDependencies(swapJobHandle, neighbourJobHandle);

            octree.pending = true;
            octree.readyToSpawn = false;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            state.CompleteDependency();
            state.EntityManager.CompleteDependencyBeforeRO<TerrainOctree>();
            state.EntityManager.CompleteDependencyBeforeRW<TerrainOctree>();
            state.EntityManager.CompleteAllTrackedJobs();

            if (SystemAPI.HasSingleton<TerrainOctree>()) {
                TerrainOctree octree = SystemAPI.GetSingleton<TerrainOctree>();
                DisposeOctree(octree);
            }

            oldNodesSet.Dispose();
            newNodesSet.Dispose();
            loaders.Dispose();
        }
    }
}