using System.Linq;
using Codice.Client.BaseCommands;
using Codice.Client.BaseCommands.BranchExplorer;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    public class VoxelOctree : VoxelBehaviour {
        [Min(1)]
        public int maxDepth = 8;
        public OctreeLoader target;

        private NativeHashSet<OctreeNode> oldNodesSet;
        private NativeHashSet<OctreeNode> newNodesSet;
        private NativeList<OctreeNode> nodesList;


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
            oldNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);
            newNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);

            addedNodes = new NativeList<OctreeNode>(Allocator.Persistent);
            removedNodes = new NativeList<OctreeNode>(Allocator.Persistent);
            pending = new NativeQueue<OctreeNode>(Allocator.Persistent);
            handle = default;
        }

        private void Compute() {
            nodesList.Clear();
            newNodesSet.Clear();
            
            addedNodes.Clear();
            removedNodes.Clear();

            OctreeNode root = OctreeNode.RootNode(maxDepth, VoxelUtils.SIZE * terrain.voxelSizeFactor);
            nodesList.Add(root);
            pending.Clear();
            pending.Enqueue(root);

            SubdivideJob job = new SubdivideJob {
                pending = pending,
                maxDepth = maxDepth,
                nodes = nodesList,
                target = target.Data,
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
            JobHandle setJobHandle = toHashSetJob.Schedule(subdivideJobHandle);
            JobHandle addedJobHandle = addedDiffJob.Schedule(setJobHandle);
            JobHandle removedJobHandle = removedDiffJob.Schedule(setJobHandle);

            JobHandle temp = JobHandle.CombineDependencies(addedJobHandle, removedJobHandle);
            JobHandle swapJobHandle = swapJob.Schedule(temp);
            handle = JobHandle.CombineDependencies(addedJobHandle, removedJobHandle, swapJobHandle);
            handle.Complete();
        }

        public override void CallerTick() {
            if (target == null) {
                Debug.LogWarning("OctreeLoader not set...");
                return;
            }

            Compute();

            if (addedNodes.Length > 0 || removedNodes.Length > 0) {
                onOctreeChanged?.Invoke(ref addedNodes, ref removedNodes, ref nodesList);
            }
        }

        public override void CallerDispose() {
            oldNodesSet.Dispose();
            newNodesSet.Dispose();
            addedNodes.Dispose();
            removedNodes.Dispose();
            pending.Dispose();
            nodesList.Dispose();
        }

        private void OnDrawGizmos() {
            if (terrain != null && nodesList.IsCreated) {
                NativeList<OctreeNode> nodes = nodesList;

                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                foreach (var node in nodes) {
                    Gizmos.DrawWireCube(node.Center, node.size * Vector3.one);
                }
            }
        }
    }
}