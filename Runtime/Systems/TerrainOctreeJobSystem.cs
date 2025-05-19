using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Octree {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TerrainOctreeJobSystem : ISystem {
        private NativeHashSet<OctreeNode> oldNodesSet;
        private NativeHashSet<OctreeNode> newNodesSet;
        private bool initialized;


        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainOctreeLoader>();
            state.RequireForUpdate<TerrainOctreeConfig>();
            state.RequireForUpdate<TerrainManagerConfig>();

            oldNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);
            newNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);
            initialized = false;
        }

        [BurstCompile]
        private void InitOctree(ref SystemState state) {
            Entity entity = SystemAPI.GetSingletonEntity<TerrainOctreeConfig>();
            state.EntityManager.AddComponent<TerrainOctree>(entity);
            state.EntityManager.SetComponentData<TerrainOctree>(entity, new TerrainOctree {
                nodes = new NativeList<OctreeNode>(Allocator.Persistent),

                added = new NativeList<OctreeNode>(Allocator.Persistent),
                removed = new NativeList<OctreeNode>(Allocator.Persistent),
                neighbourMasks = new NativeList<BitField32>(Allocator.Persistent),
                handle = default,

                continuous = true,
                pending = false,
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            if (!initialized) {
                InitOctree(ref state);
                initialized = true;
            }

            TerrainOctreeConfig config = SystemAPI.GetSingleton<TerrainOctreeConfig>();
            int maxDepth = config.maxDepth;

            RefRW<TerrainOctree> _octree = SystemAPI.GetSingletonRW<TerrainOctree>();
            ref TerrainOctree octree = ref _octree.ValueRW;

            if (octree.handle.IsCompleted && octree.pending) {
                octree.handle.Complete();
                octree.continuous = false;
                octree.pending = false;
                return;
            }

            if (!octree.continuous || !octree.handle.IsCompleted || octree.pending) {
                return;
            }

            Entity entity = SystemAPI.GetSingletonEntity<TerrainOctreeLoader>();
            TerrainOctreeLoader loader = SystemAPI.GetComponent<TerrainOctreeLoader>(entity);
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(entity);

            octree.nodes.Clear();
            octree.neighbourMasks.Clear();
            octree.added.Clear();
            octree.removed.Clear();
            newNodesSet.Clear();

            OctreeNode root = OctreeNode.RootNode(maxDepth, 64 /* >> (int)terrain.voxelSizeReduction */);
            octree.nodes.Add(root);
            octree.neighbourMasks.Add(new BitField32(0));
            
            SubdivideJob job = new SubdivideJob {
                maxDepth = maxDepth,
                root = root,

                nodes = octree.nodes,
                neighbourMasks = octree.neighbourMasks,
                
                center = transform.Position,
                radius = loader.factor,                
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
            JobHandle neighbourJobHandle = neighbourJob.Schedule<NeighbourJob, OctreeNode>(octree.nodes, 128, subdivideJobHandle);

            JobHandle setJobHandle = toHashSetJob.Schedule(neighbourJobHandle);
            JobHandle addedJobHandle = addedDiffJob.Schedule(setJobHandle);
            JobHandle removedJobHandle = removedDiffJob.Schedule(setJobHandle);

            JobHandle temp = JobHandle.CombineDependencies(addedJobHandle, removedJobHandle);
            JobHandle swapJobHandle = swapJob.Schedule(temp);
            octree.handle = JobHandle.CombineDependencies(swapJobHandle, neighbourJobHandle);
            octree.pending = true;
            //UnityEngine.Debug.Log("begin job");
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            if (SystemAPI.TryGetSingletonRW<TerrainOctree>(out RefRW<TerrainOctree> _octree)) {
                ref TerrainOctree octree = ref _octree.ValueRW;

                octree.handle.Complete();
                octree.added.Dispose();
                octree.removed.Dispose();
                octree.nodes.Dispose();
                octree.neighbourMasks.Dispose();
            }

            oldNodesSet.Dispose();
            newNodesSet.Dispose();
        }
    }
}

/*
    public class VoxelOctree : VoxelBehaviour {
        public bool drawGizmos;
        [Min(1)]
        public int maxDepth = 8;
        public OctreeLoader target;

        private NativeHashSet<OctreeNode> oldNodesSet;
        private NativeHashSet<OctreeNode> newNodesSet;
        private NativeList<OctreeNode> nodesList;
        private NativeList<BitField32> neighbourMasksList;


        // only used from inside the job. for some reason, jobs can't dipose of native queues inside of them
        // so we allocate this on the c# and use it internally in the job side
        private NativeQueue<OctreeNode> pending;

        private NativeList<OctreeNode> addedNodes;
        private NativeList<OctreeNode> removedNodes;

        public delegate void OnOctreeChanged(ref NativeList<OctreeNode> added, ref NativeList<OctreeNode> removed, ref NativeList<OctreeNode> all, ref NativeList<BitField32> neighbourMasks);
        public event OnOctreeChanged onOctreeChanged;

        private JobHandle? handle;

        public bool continuousCheck = true;

        public override void CallerStart() {
            nodesList = new NativeList<OctreeNode>(Allocator.Persistent);
            neighbourMasksList = new NativeList<BitField32>(Allocator.Persistent);

            oldNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);
            newNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);

            addedNodes = new NativeList<OctreeNode>(Allocator.Persistent);
            removedNodes = new NativeList<OctreeNode>(Allocator.Persistent);
            pending = new NativeQueue<OctreeNode>(Allocator.Persistent);
            handle = null;

            continuousCheck = true;

            if (target == null) {
                Debug.LogWarning("OctreeLoader not set...");
                return;
            }
        }

        private void BeginJob() {
            nodesList.Clear();
            neighbourMasksList.Clear();
            newNodesSet.Clear();
            addedNodes.Clear();
            removedNodes.Clear();

            OctreeNode root = OctreeNode.RootNode(maxDepth, 64 >> (int)terrain.voxelSizeReduction);
            nodesList.Add(root);
            neighbourMasksList.Add(new BitField32(0));
            pending.Clear();
            pending.Enqueue(root);

            // always update
            target.data.center = target.transform.position;

            SubdivideJob job = new SubdivideJob {
                pending = pending,
                maxDepth = maxDepth,
                nodes = nodesList,
                target = target.data,
                neighbourMasks = neighbourMasksList,
            };

            NeighbourJob neighbourJob = new NeighbourJob {
                nodes = nodesList,
                neighbourMasks = neighbourMasksList.AsDeferredJobArray(),
            };

            ToHashSetJob toHashSetJob = new ToHashSetJob {
                list = nodesList,
                set = newNodesSet,
            };

            DiffJob addedDiffJob = new DiffJob {
                src1 = oldNodesSet,
                src2 = newNodesSet,
                diffedNodes = removedNodes,
            };

            DiffJob removedDiffJob = new DiffJob {
                src1 = newNodesSet,
                src2 = oldNodesSet,
                diffedNodes = addedNodes,
            };

            SwapJob swapJob = new SwapJob {
                src = newNodesSet,
                dst = oldNodesSet,
            };

            JobHandle subdivideJobHandle = job.Schedule();
            JobHandle neighbourJobHandle = neighbourJob.Schedule<NeighbourJob, OctreeNode>(nodesList, 128, subdivideJobHandle);

            JobHandle setJobHandle = toHashSetJob.Schedule(neighbourJobHandle);
            JobHandle addedJobHandle = addedDiffJob.Schedule(setJobHandle);
            JobHandle removedJobHandle = removedDiffJob.Schedule(setJobHandle);

            JobHandle temp = JobHandle.CombineDependencies(addedJobHandle, removedJobHandle);
            JobHandle swapJobHandle = swapJob.Schedule(temp);
            handle = JobHandle.CombineDependencies(swapJobHandle, neighbourJobHandle);
        }

        public override void CallerTick() {
            if (terrain.mesher.meshingRequests.Count == 0 && terrain.readback.queued.Count == 0 && continuousCheck) {
                if (handle.HasValue) {
                    if (handle.Value.IsCompleted) {
                        handle.Value.Complete();
                        handle = null;

                        if (addedNodes.Length > 0 || removedNodes.Length > 0) {
                            continuousCheck = false;
                            onOctreeChanged?.Invoke(ref addedNodes, ref removedNodes, ref nodesList, ref neighbourMasksList);
                        } else {
                            continuousCheck = true;
                        }
                    }
                } else {
                    BeginJob();
                }
            }
        }

        public override void CallerDispose() {
            handle?.Complete();
            
            oldNodesSet.Dispose();
            newNodesSet.Dispose();
            addedNodes.Dispose();
            removedNodes.Dispose();
            pending.Dispose();
            nodesList.Dispose();
            neighbourMasksList.Dispose();
        }

        private void OnDrawGizmos() {
            if (terrain != null && nodesList.IsCreated && drawGizmos) {
                NativeList<OctreeNode> nodes = nodesList;

                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                foreach (var node in nodes) {
                    Gizmos.DrawWireCube(node.Center, node.size * Vector3.one);
                }
            }

        }
    }
*/