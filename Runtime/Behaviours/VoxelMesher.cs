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
        internal struct MeshingRequest {
            public VoxelChunk chunk;
            public bool collisions;
            public int maxTicks;
            public Action<VoxelChunk> callback;
        }

        // Stitching request where "chunk" is the higher res chunk (LOD0)
        internal struct StitchingRequest {
            public VoxelChunk chunk;
            public OctreeNode[] stitchingNeighbours;
            public BitField32 stitchMask;
        }

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
        internal Queue<MeshingRequest> queuedMeshingRequests;
        internal HashSet<MeshingRequest> meshingRequests;
        internal Queue<StitchingRequest> queuedStitchingRequests;
        private StitchJobHandler stitcher;

        // Initialize the voxel mesher
        public override void CallerStart() {
            handlers = new List<MeshJobHandler>(meshJobsPerTick);
            queuedMeshingRequests = new Queue<MeshingRequest>();
            meshingRequests = new HashSet<MeshingRequest>();
            queuedStitchingRequests = new Queue<StitchingRequest>();
            stitcher = new StitchJobHandler(this);

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
                //  || (tick - handler.startingTick) > handler.request.maxTicks)
                if (handler.finalJobHandle.IsCompleted && !handler.Free) {
                    Profiler.BeginSample("Finish Mesh Jobs");
                    FinishJob(handler);
                    Profiler.EndSample();
                }
            }

            for (int i = 0; i < meshJobsPerTick; i++) {
                if (handlers[i].Free) {
                    // Check if the chunk has valid neighbours
                    if (queuedMeshingRequests.TryPeek(out MeshingRequest job)) {
                        // All of the chunk neighbours in the 3 axii
                        // This contains one more chunk ptr that is always set to null (the one at index 13)
                        // since that one represent the source chunk (this)
                        NativeArray<Voxel>[] neighbours = new NativeArray<Voxel>[27];

                        // Bitset that tells us what of the 26 chunks we have voxel data access to
                        // In some cases (when the source chunk is at the edge of the map or when our neighbours are of different LOD) we don't have access to all the neighbouring chunks
                        // This bitset lets the job system know to skip over fetching those voxels and don't do same-level chunk mesh skirting
                        BitField32 neighbourMask = new BitField32(0);

                        // Keep another bitfield that tells us what neighbours we can do octree stitching to (when they're done with their meshing obvi)
                        BitField32 stitchingMask = new BitField32(0);
                        OctreeNode[] stitchingOctreeNodes = new OctreeNode[27];

                        // Get the neighbour indices from the octree
                        int neighbourIndicesStart = job.chunk.node.neighbourDataStartIndex;
                        NativeSlice<int> slice = terrain.octree.neighbourData.AsArray().Slice(neighbourIndicesStart, 27);
                        int depth = job.chunk.node.depth;

                        // Loop over all the neighbouring chunks, starting from the one at -1,-1,-1
                        bool all = true;
                        for (int j = 0; j < 27; j++) {
                            neighbours[j] = new NativeArray<Voxel>();
                            stitchingOctreeNodes[j] = OctreeNode.Invalid;

                            uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                            int3 offset = (int3)_offset - 1;

                            // Skip self since that's the source chunk that we alr have data for in the jobs
                            if (math.all(offset == int3.zero)) {
                                neighbourMask.SetBits(j, true);
                                continue;
                            }
                            
                            int index = slice[j];
                            if (index == -1)
                                continue;
                            
                            OctreeNode neighbourNode = terrain.octree.nodesList[index];

                            if (terrain.chunks.TryGetValue(neighbourNode, out var chunk)) {
                                VoxelChunk neighbour = chunk.GetComponent<VoxelChunk>();
                                all &= neighbour.HasVoxelData();

                                if (neighbourNode.depth == depth) {
                                    // If the neighbour is of the same depth, just do normal meshing with neighbour data
                                    neighbours[j] = neighbour.voxels;
                                    neighbourMask.SetBits(j, true);
                                } else if ((neighbourNode.depth+1) == depth) {
                                    // If the neighbour is one level higher (neighbour is lower res) then we can use it for octree stitching
                                    stitchingMask.SetBits(j, true);
                                    stitchingOctreeNodes[j] = neighbourNode;
                                }
                            }
                        }

                        // Slight trolling...
                        job.chunk.neighbourMask = neighbourMask;
                        job.chunk.stitchingMask = stitchingMask;

                        // Only begin meshing if we have the correct neighbours
                        if (all) {
                            if (queuedMeshingRequests.TryDequeue(out MeshingRequest request)) {
                                meshingRequests.Remove(request);
                                Profiler.BeginSample("Begin Mesh Jobs");

                                // TODO: don't forget to set this back :333
                                neighbourMask.Clear();
                                neighbourMask.SetBits(13, true);
                                BeginJob(handlers[i], request, neighbours, neighbourMask);
                                Profiler.EndSample();
                            }
                        } else {
                            // We can be smart and move this chunk back to the end of the queue
                            // This allows the next free mesh job handler to peek at the next element, not this one again
                            if (queuedMeshingRequests.TryDequeue(out MeshingRequest request)) {
                                queuedMeshingRequests.Enqueue(request);
                            }
                        }

                        // Put the mesh for octree stitching if needed
                        if (all && stitchingMask.CountBits() > 0) {
                            QueueStitching(job.chunk, stitchingOctreeNodes, stitchingMask);
                        }
                    }
                }
            }
        
            // Technically we should first peek and make sure that the chunk and its stitching neighbours have a valid mesh (since we need the vertex index shit)
            if (queuedStitchingRequests.TryDequeue(out var result)) {
                // For testing only:
                // src data is coming from LOD0. 
                // dst data is onto LOD1; face that is facing the positive x axis (padding)

                if (!result.stitchMask.IsSet(12)) {
                    return;
                }
                
                // we only care about going to the NEGATIVE directions... (-x, -y, -z)
                VoxelChunk lod0 = result.chunk;


                // from src, we fetch testFacetVoxelData1 and write to dst's testFacetVoxelData2 (1/4)
                OctreeNode test = result.stitchingNeighbours[12];
                VoxelChunk lod1 = terrain.chunks[test];
                lod0.other = test.Center;                
                stitcher.DoThingyMajig(result, lod0, lod1);
            }
        }

        private void BeginJob(MeshJobHandler handler, MeshingRequest request, NativeArray<Voxel>[] neighbours, BitField32 mask) {
            handler.request = request;
            handler.startingTick = tick;

            var copy = new AsyncMemCpy { src = request.chunk.voxels, dst = handler.voxels }.Schedule();
            handler.BeginJob(copy, neighbours, mask);
        }

        private void QueueStitching(VoxelChunk chunk, OctreeNode[] stitchingNeighbours, BitField32 stitchMask) {
            queuedStitchingRequests.Enqueue(new StitchingRequest {
                chunk = chunk,
                stitchingNeighbours = stitchingNeighbours,
                stitchMask = stitchMask
            });
        }

        private void FinishJob(MeshJobHandler handler) {
            if (handler.request.chunk != null) {
                VoxelChunk chunk = handler.request.chunk;
                VoxelMesh stats = handler.Complete(chunk.sharedMesh);
                chunk.voxelMaterialsLookup = stats.VoxelMaterialsLookup;
                chunk.triangleOffsetLocalMaterials = stats.TriangleOffsetLocalMaterials;
                chunk.state = VoxelChunk.ChunkState.Done;

                onMeshingComplete?.Invoke(chunk, stats);
                handler.request.callback?.Invoke(chunk);

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
            stitcher.Dispose();
        }
    }

}