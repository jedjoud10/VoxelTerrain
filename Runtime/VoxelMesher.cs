using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using Unity.Collections;

// Responsible for creating and executing the mesh generation jobs
public class VoxelMesher : VoxelBehaviour {
    public override int Priority => -10;

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
    public void GenerateMesh(VoxelChunk chunk, bool collisions = true, int maxFrames = 5) {
        /*
        if (chunk.container == null)
            return;
        */

        var job = new PendingMeshJob {
            chunk = chunk,
            collisions = collisions,
            maxFrames = maxFrames,
        };

        if (pendingMeshJobs.Contains(job))
            return;

        pendingMeshJobs.Enqueue(job);
    }

    struct CustomCopy : IJob {
        public NativeArray<Voxel> src;
        public NativeArray<Voxel> dst;
        public void Execute() {
            dst.CopyFrom(src);
        }
    }

    public override void CallerUpdate() {
        // Complete the jobs that finished and create the meshes
        foreach (var handler in handlers) {
            if ((handler.finalJobHandle.IsCompleted || (Time.frameCount - handler.startingFrame) > handler.maxFrames) && !handler.Free) {
                VoxelChunk voxelChunk = handler.chunk;

                if (voxelChunk == null)
                    return;


                var stats = handler.Complete(voxelChunk.sharedMesh);
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

        

        // Begin the jobs for the meshes
        for (int i = 0; i < meshJobsPerFrame; i++) {
            PendingMeshJob request = PendingMeshJob.Empty;
            if (pendingMeshJobs.TryDequeue(out request)) {
                if (!handlers[i].Free) {
                    pendingMeshJobs.Enqueue(request);
                    continue;
                }


                MeshJobHandler handler = handlers[i];
                handler.chunk = request.chunk;
                handler.colisions = request.collisions;
                handler.maxFrames = request.maxFrames;
                handler.startingFrame = Time.frameCount;

                /*
                // Pass through the edit system for any chunks that should be modifiable
                handler.voxelCounters.Reset();
                JobHandle dynamicEdit = terrain.VoxelEdits.TryGetApplyDynamicEditJobDependency(request.chunk, ref handler.voxels);
                JobHandle voxelEdit = terrain.VoxelEdits.TryGetApplyVoxelEditJobDependency(request.chunk, ref handler.voxels, handler.voxelCounters, dynamicEdit);
                */
                JobHandle temp = request.chunk.dependency;
                var copy = new CustomCopy { src = request.chunk.voxels, dst = handler.voxels }.Schedule(temp);
                handler.BeginJob(copy);
            }
        }
    }

    public override void CallerDispose() {
        foreach (MeshJobHandler handler in handlers) {
            handler.Complete(new Mesh());
            handler.Dispose();
        }
    }
}
