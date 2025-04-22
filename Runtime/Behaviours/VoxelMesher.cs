using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

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
                maxFrames = 5,
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

        [BurstCompile(CompileSynchronously = true)]
        struct CustomCopy : IJob {
            [ReadOnly]
            public NativeArray<Voxel> src;
            [WriteOnly]
            public NativeArray<Voxel> dst;
            public void Execute() {
                dst.CopyFrom(src);
            }
        }

        public override void CallerTick() {
            foreach (var handler in handlers) {
                if ((handler.finalJobHandle.IsCompleted || (Time.frameCount - handler.startingFrame) > handler.request.maxFrames) && !handler.Free) {
                    FinishJob(handler);
                }
            }

            for (int i = 0; i < meshJobsPerTick; i++) {
                if (handlers[i].Free) {
                    // Check if the chunk has valid neighbours
                    if (queuedJob.TryPeek(out PendingMeshJob job)) {
                        Vector3Int pos = job.chunk.chunkPosition;

                        bool all = true;
                        NativeArray<Voxel>[] neighbours = new NativeArray<Voxel>[7];
                        bool3 axisEdgeBitmask = false;
                        for (int j = 0; j < 7; j++) {
                            // Since we use morton encoding here, inside the Job we can do this step but in the other direction to fetch the chunk ID based on the index
                            // This only works because we are using morton encoding for voxel data indexing.
                            uint3 _offset = VoxelUtils.IndexToPosMorton(j + 1);
                            Vector3Int offset = new Vector3Int((int)_offset.x, (int)_offset.y, (int)_offset.z);

                            if (terrain.totalChunks.TryGetValue(pos + offset, out var chunk)) {
                                VoxelChunk neighbour = chunk.GetComponent<VoxelChunk>();
                                all &= neighbour.HasVoxelData();
                                neighbours[j] = neighbour.voxels;
                            } else {
                                // only for now...
                                all = false;


                                // happens when the chunk will never have a valid neighbour in a specific direction
                                //edge = true;
                            }
                        }

                        if (queuedJob.TryDequeue(out PendingMeshJob request)) {
                            if (all) {
                                pendingJobs.Remove(request);
                                BeginJob(handlers[i], request, neighbours);
                            } else {
                                // if we don't have all the neighbours ready yet, just take the job and put it at the end of the queue again
                                queuedJob.Enqueue(request);
                            }
                        }
                    }
                }
            }
        }

        private void BeginJob(MeshJobHandler handler, PendingMeshJob request, NativeArray<Voxel>[] neighbours) {
            handler.chunk = request.chunk;
            handler.request = request;
            handler.startingFrame = Time.frameCount;

            JobHandle temp = request.chunk.dependency.GetValueOrDefault();
            var copy = new CustomCopy { src = request.chunk.voxels, dst = handler.voxels }.Schedule(temp);
            handler.BeginJob(copy, neighbours);
        }

        private void FinishJob(MeshJobHandler handler) {
            if (handler.chunk != null) {
                VoxelChunk chunk = handler.chunk;
                var stats = handler.Complete(chunk.sharedMesh);
                chunk.dependency = default;

                chunk.state = VoxelChunk.ChunkState.Done;
                onVoxelMeshingComplete?.Invoke(chunk, stats);
                handler.request.callback?.Invoke(handler.chunk);

                chunk.GetComponent<MeshFilter>().sharedMesh = chunk.sharedMesh;
                var renderer = chunk.GetComponent<MeshRenderer>();

                renderer.materials = stats.VoxelMaterialsLookup.Select(x => terrain.materials[x].material).ToArray();

                // TODO: make bounds fit more tightly using atomic ops. on vertices during vertex job
                renderer.bounds = new Bounds {
                    min = chunk.transform.position,
                    max = chunk.transform.position + VoxelUtils.Size * VoxelUtils.VoxelSizeFactor * Vector3.one,
                };
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