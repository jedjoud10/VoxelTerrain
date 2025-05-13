using System.Linq;
using Codice.Client.BaseCommands;
using Codice.Client.BaseCommands.BranchExplorer;
using jedjoud.VoxelTerrain.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    public class VoxelOctree : VoxelBehaviour {
        public bool drawGizmos;
        [Min(1)]
        public int maxDepth = 8;
        public OctreeLoader target;

        private NativeHashSet<OctreeNode> oldNodesSet;
        private NativeHashSet<OctreeNode> newNodesSet;
        public NativeList<OctreeNode> nodesList;
        public NativeList<BitField32> neighbourMasksList;
        private NativeCounter neighboursIndicesCounter;
        public NativeArray<int> neighbourIndices;


        // only used from inside the job. for some reason, jobs can't dipose of native queues inside of them
        // so we allocate this on the c# and use it internally in the job side
        private NativeQueue<OctreeNode> pending;

        private NativeList<OctreeNode> addedNodes;
        private NativeList<OctreeNode> removedNodes;

        public delegate void OnOctreeChanged(ref NativeList<OctreeNode> added, ref NativeList<OctreeNode> removed, ref NativeList<OctreeNode> all);
        public event OnOctreeChanged onOctreeChanged;

        private JobHandle handle;

        public override void CallerStart() {
            nodesList = new NativeList<OctreeNode>(Allocator.Persistent);
            neighbourMasksList = new NativeList<BitField32>(Allocator.Persistent);

            // TODO: change this heuristic for a more tighter fit
            // currently calculates worst worst case (which is actually impossible but wtv)
            int worst = 56 * (int)math.pow(8f, (float)maxDepth);
            neighbourIndices = new NativeArray<int>(worst, Allocator.Persistent);
            neighboursIndicesCounter = new NativeCounter(Allocator.Persistent);

            oldNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);
            newNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);

            addedNodes = new NativeList<OctreeNode>(Allocator.Persistent);
            removedNodes = new NativeList<OctreeNode>(Allocator.Persistent);
            pending = new NativeQueue<OctreeNode>(Allocator.Persistent);
            handle = default;

            if (target == null) {
                Debug.LogWarning("OctreeLoader not set...");
                return;
            }

            Compute();

            if (addedNodes.Length > 0 || removedNodes.Length > 0) {
                onOctreeChanged?.Invoke(ref addedNodes, ref removedNodes, ref nodesList);
            }
        }

        private void Compute() {
            neighboursIndicesCounter.Count = 0;
            nodesList.Clear();
            neighbourMasksList.Clear();
            newNodesSet.Clear();
            addedNodes.Clear();
            removedNodes.Clear();

            OctreeNode root = OctreeNode.RootNode(maxDepth, 64 * terrain.voxelSizeFactor);
            nodesList.Add(root);
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
            handle.Complete();
        }

        public override void CallerTick() {

        }

        public override void CallerDispose() {
            oldNodesSet.Dispose();
            newNodesSet.Dispose();
            addedNodes.Dispose();
            removedNodes.Dispose();
            pending.Dispose();
            nodesList.Dispose();
            neighbourIndices.Dispose();
            neighbourMasksList.Dispose();
            neighboursIndicesCounter.Dispose();
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
}