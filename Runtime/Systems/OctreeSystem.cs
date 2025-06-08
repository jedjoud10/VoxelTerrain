using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Octree {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct OctreeSystem : ISystem {
        private NativeHashSet<OctreeNode> oldNodesSet;
        private NativeHashSet<OctreeNode> newNodesSet;
        private NativeArray<LocalTransform> loaders;

        private bool initialized;


        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainLoader>();
            state.RequireForUpdate<TerrainOctreeConfig>();
            state.RequireForUpdate<TerrainManagerConfig>();

            oldNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);
            newNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);
            initialized = false;
            loaders = default;
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
            if (!initialized) {
                state.EntityManager.CreateSingleton<TerrainOctree>(InitOctree());
                initialized = true;
                return;
            }

            TerrainOctreeConfig config = SystemAPI.GetSingleton<TerrainOctreeConfig>();
            int maxDepth = config.maxDepth;

            RefRW<TerrainOctree> _octree = SystemAPI.GetSingletonRW<TerrainOctree>();
            ref TerrainOctree octree = ref _octree.ValueRW;

            if (octree.handle.IsCompleted && octree.pending) {
                octree.handle.Complete();
                loaders.Dispose();
                loaders = default;
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

            EntityQuery loadersQuery = SystemAPI.QueryBuilder().WithAll<TerrainLoader, LocalTransform>().Build();
            loaders = loadersQuery.ToComponentDataArray<LocalTransform>(Allocator.Persistent);

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
            JobHandle neighbourJobHandle = neighbourJob.Schedule<NeighbourJob, OctreeNode>(octree.nodes, 64, subdivideJobHandle);

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

            if (initialized) {
                TerrainOctree octree = SystemAPI.GetSingleton<TerrainOctree>();
                DisposeOctree(octree);
            }

            oldNodesSet.Dispose();
            newNodesSet.Dispose();
            loaders.Dispose();
        }
    }
}