using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;
using jedjoud.VoxelTerrain.Octree;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;

namespace jedjoud.VoxelTerrain.Meshing {
    // Responsible for creating and executing the mesh generation jobs
    public class VoxelMesher : VoxelBehaviour {
        public GameObject stitchingPrefab;

        internal struct MeshingRequest {
            public VoxelChunk chunk;
            public bool collisions;
            public int maxTicks;
            public Action<VoxelChunk> callback;
        }

        [Range(1, 8)]
        public int meshJobsPerTick = 1;
        public bool useBlocky;
        public bool useFallback;
        public bool useStitching;
        public float aoGlobalOffset = 1f;
        public float aoMinDotNormal = 0.0f;
        public float aoGlobalSpread = 0.5f;
        public float aoStrength = 1.0f;
        public float skirtsDensityThreshold = -10;

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
                    if (queuedMeshingRequests.TryDequeue(out MeshingRequest job)) {
                        meshingRequests.Remove(job);

                        // Always create a stitching mesh no matter what
                        VoxelChunk chunk = job.chunk;
                        GameObject stitchGo = Instantiate(stitchingPrefab, chunk.transform);
                        stitchGo.transform.localPosition = Vector3.zero;
                        stitchGo.transform.localScale = Vector3.one;
                        chunk.skirt = stitchGo.GetComponent<VoxelSkirt>();
                        chunk.skirt.source = chunk;

                        // Create a mesh for this chunk (no stitching involved)
                        // We do need to keep some boundary data for *upcomging* stitching though
                        Profiler.BeginSample("Begin Mesh Job");
                        BeginJob(handlers[i], job);
                        Profiler.EndSample();
                        /*
                        VoxelChunk src = job.chunk;
                        VoxelStitch stitch = src.stitch;

                        // All of the chunk neighbours of the same LOD in the 3 axii
                        // This contains one more chunk ptr that is always set to null (the one at index 13)
                        // since that one represent the source chunk (this)
                        int[] sameLodNeighbours = new int[27];
                        BitField32 sameLodMask = new BitField32(0);

                        // All of the chunk neighbours of a higher LOD in the 3 axii
                        // This contains one more chunk ptr that is always set to null (the one at index 13)
                        // since that one represent the source chunk (this)
                        int[] highLodNeighbours = new int[27];
                        BitField32 highLodMask = new BitField32(0);

                        // All of the chunk neighbours of a lower LOD in the 3 axii
                        // This contains one more chunk ptr that is always set to null (the one at index 13)
                        // since that one represent the source chunk (this)
                        // Since we assume a 2:1 ratio, we will at most have 4 neighbours of a lower LOD in any planes (2 in any edge)
                        int[][] lowLodNeighbours = new int[27][];
                        BitField32 lowLodMask = new BitField32(0);

                        // Get the neighbour indices from the octree
                        int omniDirBaseIndex = src.node.neighbourDataBaseIndex;
                        NativeSlice<OctreeOmnidirectionalNeighbourData> slice = terrain.octree.omniDirectionalNeighbourDataList.AsArray().Slice(omniDirBaseIndex, 27);

                        for (int j = 0; j < 27; j++) {
                            sameLodNeighbours[j] = -1;
                            highLodNeighbours[j] = -1;
                            lowLodNeighbours[j] = null;

                            uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                            int3 offset = (int3)_offset - 1;

                            // Skip self since that's the source chunk
                            if (math.all(offset == int3.zero)) {
                                continue;
                            }

                            // If no valid octree neighbour, skip
                            OctreeOmnidirectionalNeighbourData omni = slice[j];
                            if (!omni.IsValid())
                                continue;

                            // There are multiple case scenario (same lod, diff lod {high, low})
                            int baseIndex = omni.baseIndex;
                            switch (omni.mode) {
                                case OctreeOmnidirectionalNeighbourData.Mode.SameLod:
                                    sameLodMask.SetBits(j, true);
                                    sameLodNeighbours[j] = baseIndex;
                                    break;
                                case OctreeOmnidirectionalNeighbourData.Mode.HigherLod:
                                    highLodMask.SetBits(j, true);
                                    highLodNeighbours[j] = baseIndex;
                                    break;
                                case OctreeOmnidirectionalNeighbourData.Mode.LowerLod:
                                    lowLodMask.SetBits(j, true);
                                    bool3 bool3 = offset == 1 | offset == -1;
                                    int bitmask = math.bitmask(new bool4(bool3, false));
                                    int bitsSet = math.countbits(bitmask);
                                    int multiNeighbourCount = 0;

                                    if (bitsSet == 1) {
                                        multiNeighbourCount = 4;
                                    } else if (bitsSet == 2) {
                                        multiNeighbourCount = 2;
                                    } else if (bitsSet == 3) {
                                        // custom corner case, index is stored in the struct instead of the array
                                        lowLodNeighbours[j] = new int[1];
                                        lowLodNeighbours[j][0] = baseIndex;
                                        break;
                                    } else {
                                        throw new Exception("wut");
                                    }

                                    // mmm I love indirection...
                                    lowLodNeighbours[j] = new int[multiNeighbourCount];
                                    for (int c = 0; c < multiNeighbourCount; c++) {
                                        int index = omni.baseIndex + c;
                                        lowLodNeighbours[j][c] = terrain.octree.neighbourIndices[index];
                                    }

                                    break;
                            }
                        }

                        src.neighbourMask = sameLodMask;
                        src.highLodMask = highLodMask;
                        src.lowLodMask = lowLodMask;
                        
                        if (useStitching) {
                        }
                        */
                    }
                }
            }
        }

        private void BeginJob(MeshJobHandler handler, MeshingRequest request) {
            handler.request = request;
            handler.startingTick = tick;

            var copy = new AsyncMemCpy<Voxel> { src = request.chunk.voxels, dst = handler.voxels }.Schedule();
            handler.BeginJob(copy);
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