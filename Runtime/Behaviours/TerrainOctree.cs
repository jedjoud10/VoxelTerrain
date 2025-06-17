using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    public class TerrainOctree : TerrainBehaviour {
        public bool drawGizmos;
        [Min(1)]
        public int maxDepth = 8;

        private NativeHashSet<OctreeNode> oldNodesSet;
        private NativeHashSet<OctreeNode> newNodesSet;
        private NativeList<OctreeNode> nodesList;
        private NativeList<BitField32> neighbourMasksList;

        private NativeList<OctreeNode> addedNodes;
        private NativeList<OctreeNode> removedNodes;

        [HideInInspector]
        public List<TerrainLoader> loaders;
        private NativeList<TerrainLoader.Data> loadersData;

        public delegate void OnOctreeChanged(ref NativeList<OctreeNode> added, ref NativeList<OctreeNode> removed, ref NativeList<OctreeNode> all, ref NativeList<BitField32> neighbourMasks);
        public event OnOctreeChanged onOctreeChanged;

        private JobHandle? handle;

        [HideInInspector]
        public bool continuousCheck;
        [HideInInspector]
        public bool shouldUpdate;

        public override void CallerStart() {
            nodesList = new NativeList<OctreeNode>(Allocator.Persistent);
            neighbourMasksList = new NativeList<BitField32>(Allocator.Persistent);

            oldNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);
            newNodesSet = new NativeHashSet<OctreeNode>(0, Allocator.Persistent);

            addedNodes = new NativeList<OctreeNode>(Allocator.Persistent);
            removedNodes = new NativeList<OctreeNode>(Allocator.Persistent);
            
            loaders = new List<TerrainLoader>();
            loadersData = new NativeList<TerrainLoader.Data>(Allocator.Persistent);
            
            handle = null;

            continuousCheck = true;
            shouldUpdate = true;
        }

        private void BeginJob() {
            nodesList.Clear();
            neighbourMasksList.Clear();
            newNodesSet.Clear();
            addedNodes.Clear();
            removedNodes.Clear();

            loadersData.Clear();
            loadersData.AddRange(loaders.AsEnumerable().Select(x => x.data));

            OctreeNode root = OctreeNode.RootNode(maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE);
            nodesList.Add(root);
            neighbourMasksList.Add(new BitField32(0));
                        
            SubdivideJob job = new SubdivideJob {
                maxDepth = maxDepth,
                root = root,

                nodes = nodesList,
                neighbourMasks = neighbourMasksList,

                loadersData = loadersData,
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
            JobHandle neighbourJobHandle = neighbourJob.Schedule<NeighbourJob, OctreeNode>(nodesList, 256, subdivideJobHandle);

            JobHandle setJobHandle = toHashSetJob.Schedule(neighbourJobHandle);
            JobHandle addedJobHandle = addedDiffJob.Schedule(setJobHandle);
            JobHandle removedJobHandle = removedDiffJob.Schedule(setJobHandle);

            JobHandle temp = JobHandle.CombineDependencies(addedJobHandle, removedJobHandle);
            JobHandle swapJobHandle = swapJob.Schedule(temp);
            handle = JobHandle.CombineDependencies(swapJobHandle, neighbourJobHandle);
        }

        public override void CallerTick() {
            if (terrain.mesher.Free && terrain.readback.Free && continuousCheck) {
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
                    handle = null;

                    if (shouldUpdate) {
                        shouldUpdate = false;
                        BeginJob();
                    }
                }
            }
        }

        public override void CallerDispose() {
            handle?.Complete();
            
            oldNodesSet.Dispose();
            newNodesSet.Dispose();
            addedNodes.Dispose();
            removedNodes.Dispose();
            loadersData.Dispose();
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

        public void RequestUpdate() {
            shouldUpdate = true;
        }
    }
}