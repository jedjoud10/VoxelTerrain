using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using Unity.Burst;

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
            var job = new PendingMeshJob {
                chunk = chunk,
                collisions = true,
                maxFrames = 5,
                callback = completed,
            };

            if (immediate) {
                FinishJob(handlers[0]);
                BeginJob(handlers[0], job);
                FinishJob(handlers[0]);
                return;
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
                    if (queuedJob.TryDequeue(out PendingMeshJob request)) {
                        pendingJobs.Remove(request);
                        BeginJob(handlers[i], request);
                    }
                }
            }
        }

        private void BeginJob(MeshJobHandler handler, PendingMeshJob request) {
            handler.chunk = request.chunk;
            handler.request = request;
            handler.startingFrame = Time.frameCount;

            JobHandle temp = request.chunk.dependency.GetValueOrDefault();
            var copy = new CustomCopy { src = request.chunk.voxels, dst = handler.voxels }.Schedule(temp);
            handler.BeginJob(copy);
        }

        private void FinishJob(MeshJobHandler handler) {
            if (handler.chunk != null) {
                VoxelChunk voxelChunk = handler.chunk;
                var stats = handler.Complete(voxelChunk.sharedMesh);
                voxelChunk.dependency = default;

                onVoxelMeshingComplete?.Invoke(voxelChunk, stats);
                handler.request.callback?.Invoke(handler.chunk);

                voxelChunk.GetComponent<MeshFilter>().sharedMesh = voxelChunk.sharedMesh;
                var renderer = voxelChunk.GetComponent<MeshRenderer>();

                renderer.materials = stats.VoxelMaterialsLookup.Select(x => terrain.materials[x].material).ToArray();

                // TODO: make bounds fit more tightly using atomic ops. on vertices during vertex job
                renderer.bounds = new Bounds {
                    min = voxelChunk.transform.position,
                    max = voxelChunk.transform.position + VoxelUtils.Size * VoxelUtils.VoxelSizeFactor * Vector3.one,
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