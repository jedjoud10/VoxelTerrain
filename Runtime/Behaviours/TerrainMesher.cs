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
    public class TerrainMesher : TerrainBehaviour {
        public Material material;
        [Range(1, 8)]
        public int meshJobsPerTick = 1;
        
        private List<MeshJobHandler> handlers;
        private QueueDedupped<MeshJobHandler.Request> queue;

        public delegate void OnMeshingComplete(TerrainChunk chunk, MeshJobHandler.Stats stats);
        public event OnMeshingComplete onMeshingComplete;
        
        public bool Free => queue.IsEmpty();

        // Initialize the voxel mesher
        public override void CallerStart() {
            handlers = new List<MeshJobHandler>(meshJobsPerTick);
            queue = new QueueDedupped<MeshJobHandler.Request>();

            for (int i = 0; i < meshJobsPerTick; i++) {
                handlers.Add(new MeshJobHandler());
            }
        }

        public void GenerateMesh(TerrainChunk chunk, Action<TerrainChunk> completed = null) {
            var job = new MeshJobHandler.Request {
                chunk = chunk,
                callback = completed,
            };

            queue.Enqueue(job);
            return;
        }

        public override void CallerTick() {
            foreach (var handler in handlers) {
                if (handler.IsComplete()) {
                    Profiler.BeginSample("Finish Mesh Jobs");

                    if (handler.TryComplete(out MeshJobHandler.Request request, out MeshJobHandler.Stats stats)) {
                        onMeshingComplete?.Invoke(request.chunk, stats);

                        TerrainChunk chunk = request.chunk;
                        chunk.GetComponent<MeshFilter>().sharedMesh = chunk.sharedMesh;
                        var renderer = chunk.GetComponent<MeshRenderer>();
                        renderer.enabled = true;
                        renderer.material = material;

                        float scalingFactor = chunk.node.size / VoxelUtils.PHYSICAL_CHUNK_SIZE;
                        Bounds bounds = stats.bounds;
                        bounds.center += chunk.transform.position;
                        bounds.extents *= scalingFactor;
                        renderer.bounds = bounds;
                    }

                    Profiler.EndSample();
                }
            }

            for (int i = 0; i < meshJobsPerTick; i++) {
                if (queue.TryDequeue(out MeshJobHandler.Request job)) {
                    MeshJobHandler handler = handlers.Find(x => x.Free);

                    if (handler != null) {
                        Profiler.BeginSample("Begin Mesh Job");
                        handler.BeginJob(job);
                        Profiler.EndSample();
                    }
                }
            }
        }

        public override void CallerDispose() {
            foreach (MeshJobHandler handler in handlers) {
                handler.TryComplete(out _, out _);
                handler.Dispose();
            }
        }
    }
}