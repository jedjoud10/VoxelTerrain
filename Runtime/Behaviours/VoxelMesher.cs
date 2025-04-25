using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace jedjoud.VoxelTerrain.Meshing {
    // Responsible for creating and executing the mesh generation jobs
    public class VoxelMesher : VoxelBehaviour {
        [Range(1, 8)]
        public int meshJobsPerTick = 1;

        // List of persistently allocated mesh data
        internal List<MeshJobHandler> handlers;

        // Called when a chunk finishes generating its voxel data
        public delegate void OnVoxelMeshingComplete(VoxelChunk chunk, VoxelMesh mesh);
        public event OnVoxelMeshingComplete onVoxelMeshingComplete;
        internal Queue<PendingMeshJob> queuedJob;
        internal HashSet<PendingMeshJob> pendingJobs;

        // Initialize the voxel mesher
        public override void CallerStart() {
            handlers = new List<MeshJobHandler>(meshJobsPerTick);
            queuedJob = new Queue<PendingMeshJob>();
            pendingJobs = new HashSet<PendingMeshJob>();

            for (int i = 0; i < meshJobsPerTick; i++) {
                handlers.Add(new MeshJobHandler());
            }
        }

        // Begin generating the mesh data using the given chunk and voxel container
        public void GenerateMesh(VoxelChunk chunk, bool immediate, Action<VoxelChunk> completed = null) {
            chunk.state = VoxelChunk.ChunkState.Meshing;
            var job = new PendingMeshJob {
                chunk = chunk,
                collisions = true,
                maxTicks = 5,
                callback = completed,
            };

            if (immediate) {
                Debug.LogWarning("impl neighbour fetching here too pls");
                /*
                FinishJob(handlers[0]);
                BeginJob(handlers[0], job);
                FinishJob(handlers[0]);
                return;
                */
            }

            if (pendingJobs.Contains(job))
                return;

            queuedJob.Enqueue(job);
            pendingJobs.Add(job);
            return;
        }

        public override void CallerTick() {
            foreach (var handler in handlers) {
                if ((handler.finalJobHandle.IsCompleted || (tick - handler.startingTick) > handler.request.maxTicks) && !handler.Free) {
                    Profiler.BeginSample("Finish Mesh Jobs");
                    FinishJob(handler);
                    Profiler.EndSample();
                    //Debug.Log($"Job finished in {tick - handler.startingTick} ticks");
                }
            }

            for (int i = 0; i < meshJobsPerTick; i++) {
                if (handlers[i].Free) {
                    // Check if the chunk has valid neighbours
                    if (queuedJob.TryPeek(out PendingMeshJob job)) {
                        Vector3Int pos = job.chunk.chunkPosition;

                        bool all = true;
                        NativeArray<Voxel>[] neighbours = new NativeArray<Voxel>[7];
                        
                        // Assume we can acess all the neighbours in the positive x,y,z directions
                        // In case we can't, we just set the according bools to false
                        bool3 neighbourMask = true;
                        for (int j = 0; j < 7; j++) {
                            // Since we use morton encoding here, inside the Job we can do this step but in the other direction to fetch the chunk ID based on the index
                            // This only works because we are using morton encoding for voxel data indexing.
                            uint3 _offset = VoxelUtils.IndexToPosMorton(j + 1);
                            Vector3Int offset = new Vector3Int((int)_offset.x, (int)_offset.y, (int)_offset.z);

                            neighbours[j] = new NativeArray<Voxel>();
                            if (terrain.totalChunks.TryGetValue(pos + offset, out var chunk)) {
                                VoxelChunk neighbour = chunk.GetComponent<VoxelChunk>();
                                all &= neighbour.HasVoxelData();
                                neighbours[j] = neighbour.voxels;
                            } else {
                                if (math.all(_offset == math.uint3(1, 0, 0))) {
                                    neighbourMask.x = false;
                                }

                                if (math.all(_offset == math.uint3(0, 1, 0))) {
                                    neighbourMask.y = false;
                                }

                                if (math.all(_offset == math.uint3(0, 0, 1))) {
                                    neighbourMask.z = false;
                                }
                            }
                        }

                        // Only begin meshing if we have the correct neighbours
                        if (all) {
                            if (queuedJob.TryDequeue(out PendingMeshJob request)) {
                                pendingJobs.Remove(request);
                                Profiler.BeginSample("Begin Mesh Jobs");
                                BeginJob(handlers[i], request, neighbours, neighbourMask);
                                Profiler.EndSample();
                            }
                        } else {
                            // We can be smart and move this chunk back to the end of the queue
                            // This allows the next free mesh job handler to peek at the next element, not this one again
                            if (queuedJob.TryDequeue(out PendingMeshJob request)) {
                                queuedJob.Enqueue(request);
                            }
                        }
                    }
                }
            }
        }

        private void BeginJob(MeshJobHandler handler, PendingMeshJob request, NativeArray<Voxel>[] neighbours, bool3 neighbourMask) {
            handler.chunk = request.chunk;
            handler.request = request;
            handler.startingTick = tick;

            var copy = new AsyncMemCpy { src = request.chunk.voxels, dst = handler.voxels }.Schedule();
            handler.BeginJob(copy, neighbours, neighbourMask);
        }

        private void FinishJob(MeshJobHandler handler) {
            if (handler.chunk != null) {
                VoxelChunk chunk = handler.chunk;
                VoxelMesh stats = handler.Complete(chunk.sharedMesh);
                chunk.voxelMaterialsLookup = stats.VoxelMaterialsLookup;
                chunk.triangleOffsetLocalMaterials = stats.TriangleOffsetLocalMaterials;
                chunk.state = VoxelChunk.ChunkState.Done;

                onVoxelMeshingComplete?.Invoke(chunk, stats);
                handler.request.callback?.Invoke(handler.chunk);

                chunk.GetComponent<MeshFilter>().sharedMesh = chunk.sharedMesh;
                var renderer = chunk.GetComponent<MeshRenderer>();

                renderer.materials = stats.VoxelMaterialsLookup.Select(x => terrain.materials[x].material).ToArray();

                chunk.bounds = new Bounds {
                    min = chunk.transform.position + stats.Bounds.min,
                    max = chunk.transform.position + stats.Bounds.max,
                };
                renderer.bounds = chunk.bounds;
            }
        }

        public override void CallerDispose() {
            foreach (MeshJobHandler handler in handlers) {
                handler.Complete(new Mesh());
                handler.Dispose();
            }
        }
    }

}