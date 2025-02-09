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
        // Number of simultaneous mesh generation tasks that happen during one frame
        [Range(1, 8)]
        public int meshJobsPerFrame = 1;


        [Header("Mesh Mode & Settings")]
        public bool smoothing = true;

        [Header("Mesh Ambient Occlusion")]
        public float ambientOcclusionOffset = 0.4f;
        public float ambientOcclusionPower = 2f;
        public float ambientOcclusionSpread = 0.4f;
        public float ambientOcclusionGlobalOffset = 0f;

        [Header("Mesh Materials")]
        public Material[] voxelMaterials;

        // List of persistently allocated mesh data
        internal List<MeshJobHandler> handlers;

        // Called when a chunk finishes generating its voxel data
        public delegate void OnVoxelMeshingComplete(VoxelChunk chunk, VoxelMesh mesh);
        public event OnVoxelMeshingComplete onVoxelMeshingComplete;
        internal Queue<PendingMeshJob> pendingMeshJobs;

        private void UpdateParams() {
            VoxelUtils.Smoothing = smoothing;
            VoxelUtils.PerVertexUvs = true;
            VoxelUtils.PerVertexNormals = true;
            VoxelUtils.AmbientOcclusionOffset = ambientOcclusionOffset;
            VoxelUtils.AmbientOcclusionPower = ambientOcclusionPower;
            VoxelUtils.AmbientOcclusionSpread = ambientOcclusionSpread;
            VoxelUtils.AmbientOcclusionGlobalOffset = ambientOcclusionGlobalOffset;
        }

        private void OnValidate() {
            UpdateParams();
        }

        // Initialize the voxel mesher
        public override void CallerStart() {
            handlers = new List<MeshJobHandler>(meshJobsPerFrame);
            pendingMeshJobs = new Queue<PendingMeshJob>();
            UpdateParams();

            for (int i = 0; i < meshJobsPerFrame; i++) {
                handlers.Add(new MeshJobHandler());
            }
        }

        // Begin generating the mesh data using the given chunk and voxel container
        public void GenerateMesh(VoxelChunk chunk, bool immediate) {
            var job = new PendingMeshJob {
                chunk = chunk,
                collisions = true,
                maxFrames = 5,
            };

            if (pendingMeshJobs.Contains(job))
                return;

            if (immediate) {
                FinishJob(handlers[0]);
                BeginJob(handlers[0], job);
                FinishJob(handlers[0]);
                return;
            }

            pendingMeshJobs.Enqueue(job);
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

        public override void CallerUpdate() {
            foreach (var handler in handlers) {
                if ((handler.finalJobHandle.IsCompleted || (Time.frameCount - handler.startingFrame) > handler.maxFrames) && !handler.Free) {
                    FinishJob(handler);
                }
            }

            for (int i = 0; i < meshJobsPerFrame; i++) {
                if (handlers[i].Free) {
                    if (pendingMeshJobs.TryDequeue(out PendingMeshJob request)) {
                        BeginJob(handlers[i], request);
                    }
                }
            }
        }

        private void BeginJob(MeshJobHandler handler, PendingMeshJob request) {
            handler.chunk = request.chunk;
            handler.colisions = request.collisions;
            handler.maxFrames = request.maxFrames;
            handler.startingFrame = Time.frameCount;

            JobHandle temp = request.chunk.dependency;
            var copy = new CustomCopy { src = request.chunk.voxels, dst = handler.voxels }.Schedule(temp);
            handler.BeginJob(copy);
        }

        private void FinishJob(MeshJobHandler handler) {
            if (handler.chunk != null) {
                VoxelChunk voxelChunk = handler.chunk;
                var stats = handler.Complete(voxelChunk.sharedMesh);
                voxelChunk.dependency = default;
                onVoxelMeshingComplete?.Invoke(voxelChunk, stats);
                voxelChunk.GetComponent<MeshFilter>().sharedMesh = voxelChunk.sharedMesh;
                var renderer = voxelChunk.GetComponent<MeshRenderer>();

                renderer.materials = stats.VoxelMaterialsLookup.Select(x => voxelMaterials[x]).ToArray();

                // Set renderer bounds
                renderer.bounds = new Bounds {
                    min = voxelChunk.transform.position,
                    max = voxelChunk.transform.position + VoxelUtils.Size * Vector3.one,
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