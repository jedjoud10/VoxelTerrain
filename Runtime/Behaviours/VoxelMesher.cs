using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using UnityEngine.Profiling;
using Unity.Collections;
using jedjoud.VoxelTerrain.Octree;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    // Responsible for creating and executing the mesh generation jobs
    public class VoxelMesher : VoxelBehaviour {
        internal struct MeshingRequest {
            public VoxelChunk chunk;
            public bool collisions;
            public int maxTicks;
            public Action<VoxelChunk> callback;
        }

        [Range(1, 8)]
        public int meshJobsPerTick = 1;
        public bool useSkirting;
        public float aoGlobalOffset = 1f;
        public float aoMinDotNormal = 0.0f;
        public float aoGlobalSpread = 0.5f;
        public float aoStrength = 1.0f;
        
        // List of persistently allocated mesh data
        internal List<MeshJobHandler> handlers;

        // Called when a chunk finishes generating its voxel data
        public delegate void OnMeshingComplete(VoxelChunk chunk, VoxelMesh mesh);
        public event OnMeshingComplete onMeshingComplete;
        internal Queue<MeshingRequest> queuedMeshingRequests;
        internal HashSet<MeshingRequest> meshingRequests;

        // Initialize the voxel mesher
        public override void CallerStart() {
            handlers = new List<MeshJobHandler>(meshJobsPerTick);
            queuedMeshingRequests = new Queue<MeshingRequest>();
            meshingRequests = new HashSet<MeshingRequest>();

            for (int i = 0; i < meshJobsPerTick; i++) {
                handlers.Add(new MeshJobHandler(this));
            }
        }

        // Begin generating the mesh data using the given chunk and voxel container
        public void GenerateMesh(VoxelChunk chunk, bool immediate, Action<VoxelChunk> completed = null) {
            chunk.state = VoxelChunk.ChunkState.Meshing;
            var job = new MeshingRequest {
                chunk = chunk,
                collisions = true,
                maxTicks = 5,
                callback = completed,
            };

            if (immediate) {
                throw new NotImplementedException();
            }

            if (meshingRequests.Contains(job))
                return;

            queuedMeshingRequests.Enqueue(job);
            meshingRequests.Add(job);
            return;
        }

        public override void CallerTick() {
            foreach (var handler in handlers) {
                // do NOT forget this check!
                if (handler.request.chunk != null) {
                    VoxelChunk chunk = handler.request.chunk;
                    
                    //  || (tick - handler.startingTick) > handler.request.maxTicks)
                    if (handler.finalJobHandle.IsCompleted && !handler.Free) {
                        Profiler.BeginSample("Finish Mesh Jobs");
                        FinishJob(handler);
                        Profiler.EndSample();
                    }
                }
            }

            for (int i = 0; i < meshJobsPerTick; i++) {
                if (handlers[i].Free) {
                    if (queuedMeshingRequests.TryPeek(out MeshingRequest job)) {

                        // All of the chunk neighbours in the 3 axii
                        // This contains one more chunk ptr that is always set to null (the one at index 13)
                        // since that one represent the source chunk (this)
                        NativeArray<Voxel>[] neighbours = new NativeArray<Voxel>[27];
                        OctreeNode self = job.chunk.node;
                        BitField32 mask = job.chunk.neighbourMask;

                        // Loop over all the neighbouring chunks, starting from the one at -1,-1,-1
                        bool all = true;
                        for (int j = 0; j < 27; j++) {
                            neighbours[j] = new NativeArray<Voxel>();
                            uint3 _offset = VoxelUtils.IndexToPos(j, 3);

                            // Since we need this to be between -1 and 1
                            int3 offset = (int3)_offset - 1;

                            // Skip self since that's the source chunk that we alr have data for in the jobs
                            if (math.all(offset == int3.zero)) {
                                continue;
                            }

                            // We can 'create' the neighbour node (since we know it's a leaf node and at the same depth as the current node)
                            if (mask.IsSet(j)) {
                                OctreeNode neighbourNode = new OctreeNode {
                                    size = self.size,
                                    childBaseIndex = -1,
                                    depth = self.depth,
                                    
                                    // doesn't matter since we don't consider this in the hash/equality check!!!
                                    index = -1,
                                    parentIndex = -1,

                                    position = self.position + offset * self.size,
                                };

                                if (terrain.chunks.TryGetValue(neighbourNode, out var chunk)) {
                                    VoxelChunk neighbour = chunk.GetComponent<VoxelChunk>();
                                    all &= neighbour.HasVoxelData();
                                    neighbours[j] = neighbour.voxels;
                                }
                            }
                        }

                        // Only begin meshing if we have the correct neighbours
                        if (all) {
                            if (queuedMeshingRequests.TryDequeue(out MeshingRequest request)) {
                                meshingRequests.Remove(request);
                                Profiler.BeginSample("Begin Mesh Jobs");
                                BeginJob(handlers[i], request, neighbours, mask);
                                Profiler.EndSample();
                            }
                        } else {
                            // We can be smart and move this chunk back to the end of the queue
                            // This allows the next free mesh job handler to peek at the next element, not this one again
                            if (queuedMeshingRequests.TryDequeue(out MeshingRequest request)) {
                                queuedMeshingRequests.Enqueue(request);
                            }
                        }
                    }
                }
            }
        }

        private void BeginJob(MeshJobHandler handler, MeshingRequest request, NativeArray<Voxel>[] neighbours, BitField32 mask) {
            handler.request = request;
            handler.startingTick = tick;

            var copy = new AsyncMemCpy<Voxel> { src = request.chunk.voxels, dst = handler.voxels }.Schedule();
            handler.BeginJob(copy, neighbours, mask);
        }

        private void FinishJob(MeshJobHandler handler) {
            if (handler.request.chunk != null) {
                VoxelChunk chunk = handler.request.chunk;
                VoxelMesh stats = handler.Complete(chunk.sharedMesh, chunk.skirt);
                chunk.voxelMaterialsLookup = stats.VoxelMaterialsLookup;
                chunk.triangleOffsetLocalMaterials = stats.TriangleOffsetLocalMaterials;
                chunk.state = VoxelChunk.ChunkState.Done;

                onMeshingComplete?.Invoke(chunk, stats);
                handler.request.callback?.Invoke(chunk);

                chunk.GetComponent<MeshFilter>().sharedMesh = chunk.sharedMesh;
                var renderer = chunk.GetComponent<MeshRenderer>();
                renderer.enabled = true;
                renderer.materials = stats.VoxelMaterialsLookup.Select(x => terrain.materials[x].material).ToArray();

                float scalingFactor = chunk.node.size / (64f * terrain.voxelSizeFactor);
                chunk.bounds = new Bounds {
                    min = chunk.transform.position + stats.Bounds.min * scalingFactor,
                    max = chunk.transform.position + stats.Bounds.max * scalingFactor,
                };
                renderer.bounds = chunk.bounds;
            }
        }

        public override void CallerDispose() {
            foreach (MeshJobHandler handler in handlers) {
                VoxelChunk chunk = handler.request.chunk;

                handler.Complete(null, null);
                handler.Dispose();
            }
        }
    }
}