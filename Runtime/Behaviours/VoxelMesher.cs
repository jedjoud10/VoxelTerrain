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
        public GameObject stichingPrefab;

        internal struct MeshingRequest {
            public VoxelChunk chunk;
            public bool collisions;
            public int maxTicks;
            public Action<VoxelChunk> callback;
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
        internal List<VoxelStitch> pendingPaddingVoxelSamplingRequests;
        internal List<VoxelStitch> pendingStitchRequests;
        private StitchJobHandler stitcher;

        // Initialize the voxel mesher
        public override void CallerStart() {
            handlers = new List<MeshJobHandler>(meshJobsPerTick);
            queuedMeshingRequests = new Queue<MeshingRequest>();
            meshingRequests = new HashSet<MeshingRequest>();
            pendingPaddingVoxelSamplingRequests = new List<VoxelStitch>();
            pendingStitchRequests = new List<VoxelStitch>();
            stitcher = new StitchJobHandler(this);

            for (int i = 0; i < meshJobsPerTick; i++) {
                handlers.Add(new MeshJobHandler(this));
            }


            /*
            Debug.Log(cal);
            if (bitsSet == 1) {
                Debug.Log("plane");
            } else if (bitsSet == 2) {
                Debug.Log("edge");
            } else if (bitsSet == 3) {
                Debug.Log("corner");
            } else {
                Debug.LogError("WHAT!!!");
            }
            */

            /*
            for (int i = 0; i < StitchUtils.CalculateBoundaryLength(64); i++) {
                bool check = StitchUtils.PosToBoundaryIndex(StitchUtils.BoundaryIndexToPos(i, 64, false), 64, false) == i;
                uint3 cal = StitchUtils.BoundaryIndexToPos(i, 64, false);

                bool3 bool3 = cal == 63;
                int bitmask = math.bitmask(new bool4(bool3, false));
                int bitsSet = math.countbits(bitmask);


                if (bitsSet == 1) {
                    Debug.Log($"plane: {check}");
                } else if (bitsSet == 2) {
                    Debug.Log($"edge: {check}");
                } else if (bitsSet == 3) {
                    Debug.Log($"corner: {check}");
                } else {
                    Debug.LogError("WHAT!!!");
                }
            }
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
                // do NOT forget this check!
                if (handler.request.chunk != null) {
                    VoxelChunk chunk = handler.request.chunk;
                    bool copyBoundaryJobs = chunk.copyBoundaryVerticesJobHandle.Value.IsCompleted && chunk.copyBoundaryVoxelsJobHandle.Value.IsCompleted;

                    //  || (tick - handler.startingTick) > handler.request.maxTicks)
                    if (handler.finalJobHandle.IsCompleted && copyBoundaryJobs && !handler.Free) {
                        Profiler.BeginSample("Finish Mesh Jobs");
                        chunk.copyBoundaryVerticesJobHandle.Value.Complete();
                        chunk.copyBoundaryVoxelsJobHandle.Value.Complete();
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
                        GameObject stitchGo = Instantiate(stichingPrefab, chunk.transform);
                        stitchGo.transform.localPosition = Vector3.zero;
                        stitchGo.transform.localScale = Vector3.one;
                        chunk.stitch = stitchGo.GetComponent<VoxelStitch>();
                        chunk.stitch.Init(chunk);

                        // Create a mesh for this chunk (no stitching involved)
                        // We do need to keep some boundary data for *upcomging* stitching though
                        Profiler.BeginSample("Begin Mesh Job");
                        BeginJob(handlers[i], job);
                        Profiler.EndSample();

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

                        /*
                        int depth = src.node.depth;

                        // Loop over all the neighbouring chunks, starting from the one at -1,-1,-1
                        for (int j = 0; j < 27; j++) {
                            sameLodNeighbours[j] = null;
                            diffLodNeighbours[j] = null;

                            uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                            int3 offset = (int3)_offset - 1;

                            // Skip self since that's the source chunk
                            if (math.all(offset == int3.zero)) {
                                continue;
                            }

                            // If no valid octree neighbour, skip
                            int index = slice[j];
                            if (index == -1)
                                continue;

                            OctreeNode neighbourNode = terrain.octree.nodesList[index];
                            if (terrain.chunks.TryGetValue(neighbourNode, out var neighbourGo)) {
                                VoxelChunk neighbour = neighbourGo.GetComponent<VoxelChunk>();

                                if (neighbourNode.depth == depth) {
                                    // If the neighbour is of the same depth, just do normal meshing with neighbour data
                                    sameLodNeighbours[j] = neighbour;
                                    sameLodMask.SetBits(j, true);
                                } else if ((neighbourNode.depth + 1) == depth) {
                                    // If the neighbour is one level higher (neighbour is lower res) then we can use it for octree stitching
                                    diffLodNeighbours[j] = neighbour;
                                    diffLodMask.SetBits(j, true);
                                }
                            }
                        }
                        */

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
                        

                        // check if we have any neighbours of the same resolution
                        // we only need to look in the pos axii for this one
                        // start at 1 to skip src chunk
                        FetchPositiveNeighbours(stitch, sameLodNeighbours, sameLodMask, false);

                        // check if we have any neighbours that are at a higher LOD (src=LOD0, neigh=LOD1)
                        // we only need to look in the pos axii for this one
                        FetchPositiveNeighbours(stitch, highLodNeighbours, highLodMask, true);

                        // check if we have any neighbours that are at a low LOD (src=LOD0, neigh=LOD1)
                        // we only need to look in the pos axii for this one. there can be multiple neighbours for this!!!
                        FetchPositiveNeighboursMultiNeighbour(stitch, lowLodNeighbours, lowLodMask);

                        // Tell the chunk to wait until all neighbours have voxel data to begin sampling the extra padding voxels
                        pendingPaddingVoxelSamplingRequests.Add(stitch);
                    }
                }
            }

            // Check the padding voxel sampling requests and wait until all the planes/edges/corners have valid voxel data so we can start sampling
            for (int i = pendingPaddingVoxelSamplingRequests.Count - 1; i >= 0; i--) {
                VoxelStitch stitch = pendingPaddingVoxelSamplingRequests[i];
            
                // When we can, create the extra padding voxels using downsampled or upsampled data from the neighbours
                if (stitch.CanSampleExtraVoxels()) {
                    unsafe {
                        stitch.DoTheSamplinThing();
                    }
                    pendingPaddingVoxelSamplingRequests.RemoveAt(i);
                    pendingStitchRequests.Add(stitch);
                }
            }

            // Check the stitching request and wait until all the required neighbours have had their mesh generated
            for (int i = pendingStitchRequests.Count - 1; i >= 0; i--) {
                VoxelStitch stitch = pendingStitchRequests[i];

                // When we can, create do the awesome pawsome stitching!!
                if (stitch.CanStitch()) {
                    unsafe {
                        stitch.DoTheStitchingThing();
                    }
                    pendingStitchRequests.RemoveAt(i);
                }
            }
        }


        // Sets the appropriate plane/edge/corner values with the given neighbour data and neighbour mask data
        // Only works with single neighbour systems, so only with Uniform or HiToLow
        // The bool hiToLow allows you to set the plane/edge/corner instances as HiToLow variants which means that src=LOD0, neighbour=LOD1 and where the stitch goes in the positive directions
        private void FetchPositiveNeighbours(VoxelStitch stitch, int[] neighbourIndices, BitField32 mask, bool hiToLow) {
            for (int j = 1; j < 8; j++) {
                uint3 zeroToOneOffset = VoxelUtils.IndexToPos(j, 2);
                int zeroToTwoIndex = VoxelUtils.PosToIndex(zeroToOneOffset + 1, 3);

                // set the corresponding plane/edge/corner
                if (mask.IsSet(zeroToTwoIndex)) {
                    int indirectionIndex = neighbourIndices[zeroToTwoIndex];
                    VoxelChunk neighbour = terrain.chunks[terrain.octree.nodesList[indirectionIndex]];

                    // 1=plane, 2=edge, 3=corner
                    bool3 bool3 = zeroToOneOffset == 1;
                    int bitmask = math.bitmask(new bool4(bool3, false));
                    int bitsSet = math.countbits(bitmask);

                    // Calculate relative offset in 3D
                    // Only needed when hiToLow is set to true
                    float3 srcPos = stitch.source.node.position;
                    float3 dstPos = neighbour.node.position;
                    uint3 relativeOffset = (uint3)((srcPos - dstPos) / VoxelUtils.SIZE);

                    if (bitsSet == 1) {
                        // check which axis is set
                        int dir = math.tzcnt(bitmask);
                        uint2? relativePlaneOffset = hiToLow ? StitchUtils.FlattenToFaceRelative(relativeOffset, dir) : null;
                        stitch.planes[dir] = VoxelStitch.Plane.CreateWithNeighbour(neighbour, hiToLow, relativePlaneOffset);
                    } else if (bitsSet == 2) {
                        // check which axis is NOT set
                        int inv = (~bitmask) & 0b111;
                        int dir = math.tzcnt(inv);
                        uint? relativeEdgeOffset = hiToLow ? (uint)StitchUtils.FlattenToEdgeRelative(relativeOffset, dir) : null;
                        stitch.edges[dir] = VoxelStitch.Edge.CreateWithNeighbour(neighbour, hiToLow, relativeEdgeOffset);
                    } else {
                        // corner case
                        stitch.corner = VoxelStitch.Corner.CreateWithNeighbour(neighbour, hiToLow);
                    }
                }
            }
        }

        // Used for LOD1 chunks that have multiple LOD0 neighbours in their planes/edges
        private void FetchPositiveNeighboursMultiNeighbour(VoxelStitch stitch, int[][] multiNeighbourIndices, BitField32 mask) {
            for (int j = 1; j < 8; j++) {
                uint3 zeroToOneOffset = VoxelUtils.IndexToPos(j, 2);
                int zeroToTwoIndex = VoxelUtils.PosToIndex(zeroToOneOffset + 1, 3);

                // set the corresponding plane/edge/corner
                if (mask.IsSet(zeroToTwoIndex)) {
                    // 1=plane, 2=edge, 3=corner
                    bool3 bool3 = zeroToOneOffset == 1;
                    int bitmask = math.bitmask(new bool4(bool3, false));
                    int bitsSet = math.countbits(bitmask);

                    if (bitsSet == 1) {
                        // check which axis is set
                        int dir = math.tzcnt(bitmask);

                        // sort the neighbours based on their face relative local index
                        VoxelChunk[] sortedNeighbours = new VoxelChunk[4];

                        for (int c = 0; c < 4; c++) {
                            int neighbourIndex = multiNeighbourIndices[zeroToTwoIndex][c];
                            OctreeNode neighbourNode = terrain.octree.nodesList[neighbourIndex];
                            float3 srcPos = neighbourNode.position;
                            float3 dstPos = stitch.source.node.position;
                            uint3 relativeOffset = (uint3)((srcPos - dstPos) / neighbourNode.size);
                            uint2 relativePlaneOffset = StitchUtils.FlattenToFaceRelative(relativeOffset, dir);
                            int targetIndex = VoxelUtils.PosToIndexMorton2D(relativePlaneOffset);
                            sortedNeighbours[targetIndex] = terrain.chunks[neighbourNode];
                        }

                        stitch.planes[dir] = new VoxelStitch.LoToHiPlane() {
                            lod0Neighbours = sortedNeighbours,
                        };
                    } else if (bitsSet == 2) {
                        // check which axis is NOT set
                        int inv = (~bitmask) & 0b111;
                        int dir = math.tzcnt(inv);

                        VoxelChunk[] sortedNeighbours = new VoxelChunk[2];
                        for (int c = 0; c < 2; c++) {
                            int neighbourIndex = multiNeighbourIndices[zeroToTwoIndex][c];
                            OctreeNode neighbourNode = terrain.octree.nodesList[neighbourIndex];
                            float3 srcPos = neighbourNode.position;
                            float3 dstPos = stitch.source.node.position;
                            uint3 relativeOffset = (uint3)((srcPos - dstPos) / neighbourNode.size);
                            int relativeEdgeOffset = StitchUtils.FlattenToEdgeRelative(relativeOffset, dir);
                            sortedNeighbours[relativeEdgeOffset] = terrain.chunks[neighbourNode];
                        }

                        stitch.edges[dir] = new VoxelStitch.LoToHiEdge() {
                            lod0Neighbours = sortedNeighbours,
                        };
                    } else {
                        stitch.corner = new VoxelStitch.LoToHiCorner() {
                            lod0Neighbour = terrain.chunks[terrain.octree.nodesList[multiNeighbourIndices[zeroToTwoIndex][0]]],
                        };
                    }
                }
            }
        }


        private void BeginJob(MeshJobHandler handler, MeshingRequest request) {
            handler.request = request;
            handler.startingTick = tick;

            var copy = new AsyncMemCpy { src = request.chunk.voxels, dst = handler.voxels }.Schedule();
            handler.BeginJob(copy);

            // Copy positive boundary vertices and indices to the stitching object for later stitching (as src) (at v=62)
            CopyBoundaryVerticesJob copyPositiveBoundaryVertices = new CopyBoundaryVerticesJob {
                counter = request.chunk.stitch.boundaryCounter,
                indices = handler.indices,
                vertices = handler.vertices,
                boundaryIndices = request.chunk.stitch.boundaryIndices,
                boundaryVertices = request.chunk.stitch.boundaryVertices,
                negative = false,
            };

            // Copy negative boundary vertices and indices to the stitching object for later stitching (as neighbour) (at v=0)
            CopyBoundaryVerticesJob copyNegativeBoundaryVertices = new CopyBoundaryVerticesJob {
                counter = request.chunk.negativeBoundaryCounter,
                indices = handler.indices,
                vertices = handler.vertices,
                boundaryIndices = request.chunk.negativeBoundaryIndices,
                boundaryVertices = request.chunk.negativeBoundaryVertices,
                negative = true,
            };

            // Copy positive boundary voxels at v=63
            CopyBoundaryVoxelsJob copyPositiveBoundaryVoxels = new CopyBoundaryVoxelsJob {
                voxels = handler.voxels,
                boundaryVoxels = request.chunk.stitch.boundaryVoxels,
                negative = false
            };

            // Copy negative boundary voxels at v=0
            CopyBoundaryVoxelsJob copyNegativeBoundaryVoxels = new CopyBoundaryVoxelsJob {
                voxels = handler.voxels,
                boundaryVoxels = request.chunk.negativeBoundaryVoxels,
                negative = true
            };

            JobHandle copyPosBoundaryVoxelsHandle = copyPositiveBoundaryVoxels.Schedule(StitchUtils.CalculateBoundaryLength(64), 2048, copy);
            JobHandle copyNegBoundaryVoxelsHandle = copyNegativeBoundaryVoxels.Schedule(StitchUtils.CalculateBoundaryLength(64), 2048, copy);
            request.chunk.copyBoundaryVoxelsJobHandle = JobHandle.CombineDependencies(copyPosBoundaryVoxelsHandle, copyNegBoundaryVoxelsHandle);

            // Copy the boundary data. One job runs at size=64 and other runs at size=63
            JobHandle copyPosBoundaryVerticesHandle = copyPositiveBoundaryVertices.Schedule(StitchUtils.CalculateBoundaryLength(63), 2048, handler.vertexJobHandle);
            JobHandle copyNegBoundaryVerticesHandle = copyNegativeBoundaryVertices.Schedule(StitchUtils.CalculateBoundaryLength(63), 2048, handler.vertexJobHandle);
            request.chunk.copyBoundaryVerticesJobHandle = JobHandle.CombineDependencies(copyPosBoundaryVerticesHandle, copyNegBoundaryVerticesHandle);
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