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

        internal struct StitchRequest {
            public VoxelChunk chunk;
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
        private StitchJobHandler stitcher;

        // Initialize the voxel mesher
        public override void CallerStart() {
            handlers = new List<MeshJobHandler>(meshJobsPerTick);
            queuedMeshingRequests = new Queue<MeshingRequest>();
            meshingRequests = new HashSet<MeshingRequest>();
            pendingPaddingVoxelSamplingRequests = new List<VoxelStitch>();
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
                    if (queuedMeshingRequests.TryDequeue(out MeshingRequest job)) {
                        meshingRequests.Remove(job);

                        // Create a mesh for this chunk (no stitching involved)
                        Profiler.BeginSample("Begin Mesh Job");
                        BeginJob(handlers[i], job);
                        Profiler.EndSample();

                        // Always create a stitching mesh no matter what
                        VoxelChunk chunk = job.chunk;
                        GameObject stitchGo = Instantiate(stichingPrefab, chunk.transform);
                        stitchGo.transform.localPosition = Vector3.zero;
                        stitchGo.transform.localScale = Vector3.one;
                        chunk.stitch = stitchGo.GetComponent<VoxelStitch>();
                        chunk.stitch.Init();

                        VoxelChunk src = job.chunk;
                        VoxelStitch stitch = src.stitch;

                        // All of the chunk neighbours of the same LOD in the 3 axii
                        // This contains one more chunk ptr that is always set to null (the one at index 13)
                        // since that one represent the source chunk (this)
                        VoxelChunk[] sameLodNeighbours = new VoxelChunk[27];
                        BitField32 sameLodMask = new BitField32(0);

                        // All of the chunk neighbours of a higher LOD in the 3 axii
                        // This contains one more chunk ptr that is always set to null (the one at index 13)
                        // since that one represent the source chunk (this)
                        VoxelChunk[] diffLodNeighbours = new VoxelChunk[27];
                        BitField32 diffLodMask = new BitField32(0);

                        // Get the neighbour indices from the octree
                        int neighbourIndicesStart = src.node.neighbourDataStartIndex;
                        NativeSlice<int> slice = terrain.octree.neighbourData.AsArray().Slice(neighbourIndicesStart, 27);
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

                        src.neighbourMask = sameLodMask;
                        src.stitchingMask = diffLodMask;

                        // check if we have any neighbours of the same resolution
                        // we only need to look in the pos axii for this one
                        // start at 1 to skip src chunk
                        FetchPositiveNeighbours(stitch, sameLodNeighbours, sameLodMask, false);

                        // check if we have any neighbours that are at a higher LOD (src=LOD0, neigh=LOD1)
                        // we only need to look in the pos axii for this one
                        FetchPositiveNeighbours(stitch, diffLodNeighbours, diffLodMask, true);

                        // check if we have any neighbours that are at a higher LOD (src=LOD0, neigh=LOD1), but this time to update *their* stitch values
                        // we need to look in the negative direction only, since LOD1 chunks that need to be LoToHi will be in that direction
                        FetchNegativeNeighboursLod1(src, diffLodNeighbours, diffLodMask);

                        // Create a voxel adaptation request
                        pendingPaddingVoxelSamplingRequests.Add(stitch);
                    }
                }
            }

            // Check the padding voxel sampling requests and wait until all the planes/edges/corners have valid voxel data so we can start sampling
            for (int i = pendingPaddingVoxelSamplingRequests.Count - 1; i >= 0; i--) {
                VoxelStitch stitch = pendingPaddingVoxelSamplingRequests[i];
            
                // When we can, create the extra padding voxels using downsampled or upsampled data from the neighbours
                if (stitch.CanSampleExtraVoxels()) {
                    Debug.Log("thingimante");
                    pendingPaddingVoxelSamplingRequests.RemoveAt(i);
                }
            }

            // If we have LOD1 neighbours in the negative directions, they also need to create their own stitching mesh
            // 4, 10, 12

            /*
            // Negative X
            if (req.stitchMask.IsSet(4)) {

            }

            // Negative 
            if (req.stitchMask.IsSet(4)) {

            }

            // Negative Z
            if (req.stitchMask.IsSet(4)) {

            }
            */

            /*
            // For testing only:
            // src data is coming from LOD0. 
            // dst data is onto LOD1; face that is facing the positive x axis (padding)
            if (!result.stitchMask.IsSet(14)) {
                return;
            }

            // we only care about going to the NEGATIVE directions... (-x, -y, -z)
            VoxelChunk lod0 = result.chunk;

            OctreeNode test = result.stitchingNeighbours[14];
            VoxelChunk lod1 = terrain.chunks[test];
            lod0.other = test.Center;
            lod0.blurredPositiveXFacingExtraVoxelsFlat = new NativeArray<Voxel>(VoxelUtils.SIZE * VoxelUtils.SIZE, Allocator.Persistent);

            // Create a new gameobject specific for holding the stitching mesh data
            if (lod1.stitch == null) {
                lod1.blurredPositiveXFacingExtraVoxelsFlat = new NativeArray<Voxel>(VoxelUtils.SIZE * VoxelUtils.SIZE, Allocator.Persistent);


            }

            // calculate offset
            int quadrantVolume = VoxelUtils.SIZE * VoxelUtils.SIZE / 4;
            float3 srcPos = lod0.node.position;
            float3 dstPos = lod1.node.position;
            uint2 offset = (uint2)((srcPos - dstPos).yz / lod0.node.size);

            // store this chunk for later stitching
            int localIndex = VoxelUtils.PosToIndexMorton2D(offset);
            lod0.relativeOffsetToLod1 = offset;
            lod1.stitch.lod0NeighboursNegativeX[localIndex] = lod0;
            //lod1.stitch.lod0NeighboursPositiveX[] = lod0;

            // fetch the voxels from the source chunk and blur them
            int mortonOffset = VoxelUtils.PosToIndexMorton2D(offset) * quadrantVolume;
            //Debug.Log(mortonOffset);
            */

            /*
            FaceVoxelsDownsampleJob downsample = new FaceVoxelsDownsampleJob() {
                lod0Voxels = lod0.voxels,
                dstFace = lod1.blurredPositiveXFacingExtraVoxelsFlat,
                mortonOffset = mortonOffset,
            };
            */

            /*
            FaceVoxelsUpsampleJob upsample = new FaceVoxelsUpsampleJob() {
                dstFace = lod0.blurredPositiveXFacingExtraVoxelsFlat,
                lod1Voxels = lod1.voxels,
                relativeLod1Offset = offset,
            };

            upsample.Schedule(VoxelUtils.SIZE * VoxelUtils.SIZE, 1024).Complete();
            */

            // since we will be blurring each 2x2x2 region (from LOD0) into a single voxel (into LOD1) we will at max be writing to a single "quadrant" of the face
            //copy.Schedule(quadrantVolume, 1024).Complete();
            //lod1.stitch.neighbourChunkBlurredSections++;


            /*
            // we can do stitching if LOD1 has fully blurred out data
            if (lod1.stitch.neighbourChunkBlurredSections == 4) {
                stitcher.DoThingyMajig(result, lod1, lod1.stitch);
            }
            */

        }

        // Sets the appropriate plane/edge/corner values for the LOD1 neighbours manually
        // Looks in the negative direciton, since that's where the LOD1 neighbours do their own stitching
        private static void FetchNegativeNeighboursLod1(VoxelChunk src, VoxelChunk[] diffLodNeighbours, BitField32 diffLodMask) {
            VoxelChunk lod0 = src;

            // skip j=7 since that's source
            for (int j = 0; j < 7; j++) {
                uint3 zeroToOneOffset = VoxelUtils.IndexToPos(j, 2);
                int zeroToTwoIndex = VoxelUtils.PosToIndex(zeroToOneOffset, 3);
                VoxelChunk lod1 = diffLodNeighbours[zeroToTwoIndex];
                uint3 offset = VoxelUtils.IndexToPos(zeroToTwoIndex, 3);

                // set the corresponding plane/edge/corner
                if (diffLodMask.IsSet(zeroToTwoIndex)) {
                    // do the negative direction check
                    bool3 bool3 = offset == 0;

                    // 1=plane, 2=edge, 3=corner
                    int bitmask = math.bitmask(new bool4(bool3, false));
                    int bitsSet = math.countbits(bitmask);

                    // Calculate relative offset in 3D
                    float3 srcPos = src.node.position;
                    float3 dstPos = lod1.node.position;
                    uint3 relativeOffset = (uint3)((srcPos - dstPos) / VoxelUtils.SIZE);
                    //Debug.Log($"src={srcPos}, lod1={dstPos}, rel={relativeOffset}");

                    if (bitsSet == 1) {
                        // check which axis is set
                        int dir = math.tzcnt(bitmask);

                        // Update LOD1's plane
                        if (lod1.stitch.planes[dir] == null) {
                            lod1.stitch.planes[dir] = new VoxelStitch.LoToHiPlane {
                                lod0Neighbours = new VoxelChunk[4],
                            };
                        }

                        // Map the 3D position's to the face 2D flattened position
                        uint2 flattenedOffset = StitchUtils.FlattenToFaceRelative(relativeOffset, dir);

                        // Set the LOD0 neighbour (this) using calculated offset index (2D plane)
                        int index = VoxelUtils.PosToIndexMorton2D(flattenedOffset);
                        VoxelStitch.LoToHiPlane plane = lod1.stitch.planes[dir] as VoxelStitch.LoToHiPlane;
                        plane.lod0Neighbours[index] = lod0;
                    } else if (bitsSet == 2) {
                        // check which axis is NOT set
                        int inv = (~bitmask) & 0b111;
                        int dir = math.tzcnt(inv);

                        // Update LOD1's edge
                        if (lod1.stitch.edges[dir] == null) {
                            lod1.stitch.edges[dir] = new VoxelStitch.LoToHiEdge {
                                lod0Neighbours = new VoxelChunk[2],
                            };
                        }

                        // Map the 3D position's to the edge 1D flattened index
                        int index = StitchUtils.FlattenToEdgeRelative(relativeOffset, dir);
                        VoxelStitch.LoToHiEdge edge = lod1.stitch.edges[dir] as VoxelStitch.LoToHiEdge;
                        edge.lod0Neighbours[index] = lod0;
                    } else {
                        // Update LOD1's corner. There can only be one corner piece so we don't need to check for this one
                        lod1.stitch.corner = new VoxelStitch.LoToHiCorner {
                            lod0Neighbour = lod0,
                        };
                    }
                }
            }
        }

        // Sets the appropriate plane/edge/corner values with the given neighbour data and neighbour mask data
        // The bool hiToLow allows you to set the plane/edge/corner instances as HiToLow variants which means that src=LOD0, neighbour=LOD1 and where the stitch goes in the positive directions
        private static void FetchPositiveNeighbours(VoxelStitch stitch, VoxelChunk[] neighbours, BitField32 mask, bool hiToLow) {
            for (int j = 1; j < 8; j++) {
                uint3 zeroToOneOffset = VoxelUtils.IndexToPos(j, 2);
                int zeroToTwoIndex = VoxelUtils.PosToIndex(zeroToOneOffset + 1, 3);
                VoxelChunk neighbour = neighbours[zeroToTwoIndex];

                // set the corresponding plane/edge/corner
                if (mask.IsSet(zeroToTwoIndex)) {
                    // 1=plane, 2=edge, 3=corner
                    bool3 bool3 = zeroToOneOffset == 1;
                    int bitmask = math.bitmask(new bool4(bool3, false));
                    int bitsSet = math.countbits(bitmask);

                    if (bitsSet == 1) {
                        // check which axis is set
                        int dir = math.tzcnt(bitmask);
                        stitch.planes[dir] = VoxelStitch.Plane.CreateWithNeighbour(neighbour, hiToLow);
                    } else if (bitsSet == 2) {
                        // check which axis is NOT set
                        int inv = (~bitmask) & 0b111;
                        int dir = math.tzcnt(inv);
                        stitch.edges[dir] = VoxelStitch.Edge.CreateWithNeighbour(neighbour, hiToLow);
                    } else {
                        // corner case
                        stitch.corner = VoxelStitch.Corner.CreateWithNeighbour(neighbour, hiToLow);
                    }
                }
            }
        }

        private void BeginJob(MeshJobHandler handler, MeshingRequest request) {
            handler.request = request;
            handler.startingTick = tick;

            var copy = new AsyncMemCpy { src = request.chunk.voxels, dst = handler.voxels }.Schedule();
            handler.BeginJob(copy);
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