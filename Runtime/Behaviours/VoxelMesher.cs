using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;
using jedjoud.VoxelTerrain.Octree;

namespace jedjoud.VoxelTerrain.Meshing {
    // Responsible for creating and executing the mesh generation jobs
    public class VoxelMesher : VoxelBehaviour {
        [Range(1, 8)]
        public int meshJobsPerTick = 1;

        public float aoGlobalOffset = 1f;
        public float aoMinDotNormal = 0.0f;
        public float aoGlobalSpread = 0.5f;
        public float aoStrength = 1.0f;

        // List of persistently allocated mesh data
        internal List<MeshJobHandler> handlers;

        // Called when a chunk finishes generating its voxel data
        public delegate void OnMeshingComplete(VoxelChunk chunk, VoxelMesh mesh);
        public event OnMeshingComplete onMeshingComplete;
        internal Queue<PendingMeshJob> queuedJob;
        internal HashSet<PendingMeshJob> pendingJobs;

        // Initialize the voxel mesher
        public override void CallerStart() {
            handlers = new List<MeshJobHandler>(meshJobsPerTick);
            queuedJob = new Queue<PendingMeshJob>();
            pendingJobs = new HashSet<PendingMeshJob>();

            for (int i = 0; i < meshJobsPerTick; i++) {
                handlers.Add(new MeshJobHandler(this));
            }

            /*
            // Used to calculate the lookup table for morton -> non-morton neighbour lookup
            for (int i = 0; i < 27; i++) {
                uint3 morton = VoxelUtils.IndexToPosMorton(i);
                uint3 normal = VoxelUtils.IndexToPos(i, 3);
                int mapped = VoxelUtils.PosToIndex(morton, 3);

                if ()
                Debug.Log($"i={i}, morton={morton}, normal={normal}, mapped={mapped}");
            }
            */

            /*
            int3 amogus = new int3(VoxelUtils.SIZE + 4) / VoxelUtils.SIZE;
            Debug.Log(amogus);
            */
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
                //  || (tick - handler.startingTick) > handler.request.maxTicks)
                if (handler.finalJobHandle.IsCompleted && !handler.Free) {
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
                        // All of the chunk neighbours in the 3 axii
                        // This contains one more chunk ptr that is always set to null (the one at index 13)
                        // since that one represent the source chunk (this)
                        NativeArray<Voxel>[] neighbours = new NativeArray<Voxel>[27];

                        // Bitset that tells us what of the 26 chunks we have voxel data access to
                        // In some cases (when the source chunk is at the edge of the map or when our neighbours are of different LOD) we don't have access to all the neighbouring chunks
                        // This bitset lets the job system know to skip over fetching those voxels and don't do same-level chunk mesh skirting
                        BitField32 mask = new BitField32(uint.MaxValue);

                        // Get the neighbour indices from the octree
                        int neighbourIndicesStart = job.chunk.node.neighbourDataStartIndex;
                        NativeSlice<int> slice = terrain.octree.neighbourData.AsArray().Slice(neighbourIndicesStart, 27);
                        int depth = job.chunk.node.depth;

                        // Loop over all the neighbouring chunks, starting from the one at -1,-1,-1
                        bool all = true;
                        for (int j = 0; j < 27; j++) {
                            uint3 _offset = VoxelUtils.IndexToPos(j, 3);

                            // Since we need this to be between -1 and 1
                            int3 offset = (int3)_offset - 1;

                            // Skip self since that's the source chunk that we alr have data for in the jobs
                            if (math.all(offset == int3.zero)) {
                                mask.SetBits(j, true);
                                continue;
                            }

                            neighbours[j] = new NativeArray<Voxel>();
                            
                            int index = slice[j];
                            if (index == -1) {
                                mask.SetBits(j, false);
                                continue;
                            }

                            OctreeNode neighbourNode = terrain.octree.nodesList[index];
                            if (terrain.chunks.TryGetValue(neighbourNode, out var chunk) && neighbourNode.depth == depth) {
                                VoxelChunk neighbour = chunk.GetComponent<VoxelChunk>();
                                all &= neighbour.HasVoxelData();
                                neighbours[j] = neighbour.voxels;
                            } else {
                                mask.SetBits(j, false);
                            }
                        }

                        job.chunk.neighbourMask = mask;

                        // Only begin meshing if we have the correct neighbours
                        if (all) {
                            if (queuedJob.TryDequeue(out PendingMeshJob request)) {
                                pendingJobs.Remove(request);
                                Profiler.BeginSample("Begin Mesh Jobs");
                                BeginJob(handlers[i], request, neighbours, mask);
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

        private void BeginJob(MeshJobHandler handler, PendingMeshJob request, NativeArray<Voxel>[] neighbours, BitField32 mask) {
            handler.chunk = request.chunk;
            handler.request = request;
            handler.startingTick = tick;

            var copy = new AsyncMemCpy { src = request.chunk.voxels, dst = handler.voxels }.Schedule();
            handler.BeginJob(copy, neighbours, mask);
        }

        private void FinishJob(MeshJobHandler handler) {
            if (handler.chunk != null) {
                VoxelChunk chunk = handler.chunk;
                VoxelMesh stats = handler.Complete(chunk.sharedMesh);
                chunk.voxelMaterialsLookup = stats.VoxelMaterialsLookup;
                chunk.triangleOffsetLocalMaterials = stats.TriangleOffsetLocalMaterials;
                chunk.state = VoxelChunk.ChunkState.Done;

                onMeshingComplete?.Invoke(chunk, stats);
                handler.request.callback?.Invoke(handler.chunk);

                chunk.GetComponent<MeshFilter>().sharedMesh = chunk.sharedMesh;
                var renderer = chunk.GetComponent<MeshRenderer>();
                renderer.enabled = true;
                renderer.materials = stats.VoxelMaterialsLookup.Select(x => terrain.materials[x].material).ToArray();

                float scalingFactor = chunk.node.size / (VoxelUtils.SIZE * terrain.voxelSizeFactor);
                chunk.bounds = new Bounds {
                    min = chunk.transform.position + stats.Bounds.min * scalingFactor,
                    max = chunk.transform.position + stats.Bounds.max * scalingFactor,
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