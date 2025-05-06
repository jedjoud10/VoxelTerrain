using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;
using jedjoud.VoxelTerrain.Octree;
using Unity.Collections.LowLevel.Unsafe;
using jedjoud.VoxelTerrain.Unsafe;

namespace jedjoud.VoxelTerrain.Meshing {
    // We only do stitching in the positive x,y,z directions
    // So we only care about the neighbour data for 3 faces
    // well actually no, since you also want to fix the gaps between chunks of the SAME LOD
    // you only care about the data for 3 faces but also the 3 edges and single corner
    // if we're smart we can also handle same-res stitching using this algo!!!
    public class VoxelStitch : MonoBehaviour {
        interface IChunkCollector {
            void Collect(List<VoxelChunk> list);
        }

        // Stitch boundary (plane) (stored 3 times for each of the x,y,z axii)
        public abstract class Plane : IChunkCollector {
            public abstract void Collect(List<VoxelChunk> list);
            public static Plane CreateWithNeighbour(VoxelChunk neighbour, bool hiToLow, uint2? relativeOffset) {
                if (hiToLow) {
                    return new HiToLoPlane() { lod1Neighbour = neighbour, relativeOffset = relativeOffset.Value };
                } else {
                    return new UniformPlane() { neighbour = neighbour };
                }
            }
        }

        // this=LOD0, neighbour=LOD1
        public class HiToLoPlane : Plane {
            // we only have one LOD1 neighbour
            public VoxelChunk lod1Neighbour;

            // relative offset of the LOD0 chunk relative to LOD1
            public uint2 relativeOffset;

            public override void Collect(List<VoxelChunk> list) {
                list.Add(lod1Neighbour);
            }
        }

        // this=LOD1, neighbour=LOD0
        // means that we need to downsample the data
        public class LoToHiPlane : Plane {
            // We can have up to 4 neighbours in that direction
            public VoxelChunk[] lod0Neighbours;

            public override void Collect(List<VoxelChunk> list) {
                list.AddRange(lod0Neighbours);
            }
        }

        // this=LOD0, neighbour=LOD0
        public class UniformPlane : Plane {
            public VoxelChunk neighbour;

            public override void Collect(List<VoxelChunk> list) {
                list.Add(neighbour);
            }
        }

        // Stitch edge (line) (stored 3 times for each of the x,y,z axii)
        public abstract class Edge : IChunkCollector {
            public abstract void Collect(List<VoxelChunk> list);
            public static Edge CreateWithNeighbour(VoxelChunk neighbour, bool hiToLow, uint? relativeOffset) {
                if (hiToLow) {
                    return new HiToLoEdge() { lod1Neighbour = neighbour, relativeOffset = relativeOffset.Value };
                } else {
                    return new UniformEdge() { neighbour = neighbour };
                }
            }
        }

        // this=LOD0, diagonal neighbour=LOD1
        public class HiToLoEdge : Edge {
            // we only have one LOD1 neighbour
            public VoxelChunk lod1Neighbour;

            // relative offset of the LOD0 chunk relative to LOD1
            public uint relativeOffset;

            public override void Collect(List<VoxelChunk> list) {
                list.Add(lod1Neighbour);
            }
        }

        // this=LOD1, diagonal neighbour=LOD0
        // means that we need to downsample the data
        public class LoToHiEdge : Edge {
            // we can have up to 2 neighbours in that direction
            public VoxelChunk[] lod0Neighbours;

            public override void Collect(List<VoxelChunk> list) {
                list.AddRange(lod0Neighbours);
            }
        }

        // this=LOD0, neighbour=LOD0
        public class UniformEdge : Edge {
            public VoxelChunk neighbour;

            public override void Collect(List<VoxelChunk> list) {
                list.Add(neighbour);
            }
        }

        // Stitch corner (point) stored one since we only have one corner
        public abstract class Corner : IChunkCollector {
            public abstract void Collect(List<VoxelChunk> list);
            public static Corner CreateWithNeighbour(VoxelChunk neighbour, bool hiToLow) {
                if (hiToLow) {
                    return new HiToLoCorner() { lod1Neighbour = neighbour };
                } else {
                    return new UniformCorner() { neighbour = neighbour };
                }
            }
        }

        // this=LOD0, corner neighbour=LOD1
        public class HiToLoCorner : Corner {
            // we only have one LOD1 neighbour
            public VoxelChunk lod1Neighbour;

            // we don't need to store the relative offset since we know it's always (0,0,0)

            public override void Collect(List<VoxelChunk> list) {
                list.Add(lod1Neighbour);
            }
        }

        // this=LOD1, corner neighbour=LOD0
        // means that we need to downsample the data
        public class LoToHiCorner : Corner  {
            // we only have one LOD0 neighbour
            public VoxelChunk lod0Neighbour;

            public override void Collect(List<VoxelChunk> list) {
                list.Add(lod0Neighbour);
            }
        }

        // this=LOD0, neighbour=LOD0
        public class UniformCorner : Corner {
            public VoxelChunk neighbour;

            public override void Collect(List<VoxelChunk> list) {
                list.Add(neighbour);
            }
        }

        // Source chunk
        public VoxelChunk source;

        // X,Y,Z
        public Plane[] planes;
        
        // X,Y,Z
        public Edge[] edges;
        
        // Corner
        public Corner corner;

        // Collects all the neighbouring chunks in a list so we can do bulk operations on them
        // Returns null if any of the planes/edges/corner are null
        public List<VoxelChunk> CollectNeighbours() {
            bool valid = planes.All(x => x != null) && edges.All(x => x != null) && corner != null;

            if (!valid) {
                return null;
            }

            List<VoxelChunk> list = new List<VoxelChunk>();

            for (int i = 0; i < 3; i++) {
                planes[i].Collect(list);
            }

            for (int i = 0; i < 3; i++) {
                edges[i].Collect(list);
            }

            corner.Collect(list);
            return list;
        }

        // Check if we can adapt neighbouring voxels to our padding voxels array
        // This requires us to have access to all the neighbouring chunks in the positive axii in 3D ABD also that they have valid voxel data
        public bool CanSampleExtraVoxels() {
            List<VoxelChunk> neighbours = CollectNeighbours();

            if (neighbours == null) {
                return false;
            } else {
                return neighbours.All(x => x != null && x.HasNegativeBoundaryVoxelData());
            }
        }

        // Check if we can do stitching (if we have our [down/up]-sampled extra voxels and if the neighbouring chunks got their mesh data ready)
        public bool CanStitch() {
            List<VoxelChunk> neighbours = CollectNeighbours();

            if (neighbours == null) {
                return false;
            } else {
                bool neighboursValid = neighbours.All(x => x != null && x.HasNegativeBoundaryMeshData());
                bool selfBoundaryVoxelsValid = source.copyBoundaryVoxelsJobHandle.HasValue && source.copyBoundaryVoxelsJobHandle.Value.IsCompleted;
                bool selfBoundaryVerticesValid = source.copyBoundaryVerticesJobHandle.HasValue && source.copyBoundaryVerticesJobHandle.Value.IsCompleted;
                return neighboursValid && selfBoundaryVoxelsValid && selfBoundaryVerticesValid && adaptedVoxels;
            }
        }


        // STORING UPSAMPLED / DONWSAMPLED DATA:
        // We basically need to store 65^3 voxels to be able to create that "padding" vertex on the boundary
        // I first store the values for the 3 faces but only the region of 64x64 voxels sequentially
        // Then I store the edges separately, x,y,z
        // Then I store the corner piece by itself, last value
        // Down/up-sampling is done SEPARATELY FROM STITCHING
        public NativeArray<Voxel> paddingVoxels;

        // These are the boundary voxels from the source chunk. Compacted the same way as extraVoxels but with size=64
        public NativeArray<Voxel> boundaryVoxels;
        private bool adaptedVoxels;

        // Copied indices from the source chunk mesh
        // Packed so we only store the indices on the boundary (v=62)
        public NativeArray<float3> boundaryVertices;
        public NativeArray<int> boundaryIndices;
        public NativeCounter boundaryCounter;

        // Padding vertices that are generated using the boundary voxels and extra voxels
        // Packed so we only store the indices for the padding strip (v=63)
        public NativeArray<float3> paddingVertices;
        public NativeArray<int> paddingIndices;
        public NativeCounter paddingCounter;
        public bool stitched;

        // The generated stitched vertices & triangles for our mesh
        public NativeArray<float3> vertices;
        public NativeArray<int> indices;

        public void Init(VoxelChunk self) {
            this.source = self;
            stitched = false;
            adaptedVoxels = false;
            int smallerBoundary = StitchUtils.CalculateBoundaryLength(63);
            int boundary = StitchUtils.CalculateBoundaryLength(64);
            int paddedBoundary = StitchUtils.CalculateBoundaryLength(65);

            // limit=64
            paddingVoxels = new NativeArray<Voxel>(paddedBoundary, Allocator.Persistent);

            // limit=63
            paddingVertices = new NativeArray<float3>(boundary, Allocator.Persistent);
            paddingIndices = new NativeArray<int>(boundary, Allocator.Persistent);
            boundaryVoxels = new NativeArray<Voxel>(boundary, Allocator.Persistent);
            
            // limit=62
            boundaryIndices = new NativeArray<int>(smallerBoundary, Allocator.Persistent);
            boundaryVertices = new NativeArray<float3>(smallerBoundary, Allocator.Persistent);

            paddingCounter = new NativeCounter(Allocator.Persistent);
            boundaryCounter = new NativeCounter(Allocator.Persistent);

            // Set the boundary helpers to null since we haven't set them up yet
            planes = new Plane[3] { null, null, null };
            edges = new Edge[3] { null, null, null };
            corner = null;
        }

        public unsafe struct GenericBoundaryPlane<T> where T: unmanaged {
            // Uniform neighbour data
            [ReadOnly]
            public T* uniform;

            // LOD1 neighbour data, not sliced, whole
            [ReadOnly]
            public T* lod1;
            public uint2 relativeOffset;

            // LOD0 neighbours (4 of them) data, morton 2D
            [ReadOnly]
            public UnsafePtrList<T> lod0s;
        }

        public unsafe struct GenericBoundaryEdge<T> where T : unmanaged {
            // Uniform neighbour data
            [ReadOnly]
            public T* uniform;

            // LOD1 neighbour data, not sliced, whole
            [ReadOnly]
            public T* lod1;
            public uint relativeOffset;

            // LOD0 neighbours (2 of them) data
            [ReadOnly]
            public UnsafePtrList<T> lod0s;
        }

        public unsafe struct GenericBoundaryCorner<T> where T : unmanaged {
            // Uniform neighbour data
            [ReadOnly]
            public T* uniform;

            // LOD1 neighbour data, not sliced, whole
            [ReadOnly]
            public T* lod1;

            // LOD0 neighbour data
            [ReadOnly]
            public T* lod0;
        }

        // All the data stored in boundary formatting.
        // ALWAYS ASSUMES WE ARE DEALING WITH THE NEGATIVE BOUNDARIES!!!!
        public struct GenericBoundaryData<T> where T: unmanaged {
            [ReadOnly]
            public UnsafeList<GenericBoundaryPlane<T>> planes;
            [ReadOnly]
            public UnsafeList<GenericBoundaryEdge<T>> edges;
            [ReadOnly]
            public GenericBoundaryCorner<T> corner;

            // Uniform | LoToHi | HiToLo => 3 states => 2 bits
            // 2 bits per plane, 2 bits per edge, 2 bits per corner
            // 3 planes, 3 edges, 1 corner => 2*3 + 2*3 + 2 => 14 bits in total
            [ReadOnly]
            public BitField32 state;

            public void Dispose() {
                for (int i = 0; i < 3; i++) {
                    if (planes[i].lod0s.IsCreated) {
                        planes[i].lod0s.Dispose();
                    }

                    if (edges[i].lod0s.IsCreated) {
                        edges[i].lod0s.Dispose();
                    }
                }

                planes.Dispose();
                edges.Dispose();
            }
        }

        public unsafe void DoTheSamplinThing() {
            List<VoxelChunk> list = CollectNeighbours();

            foreach (VoxelChunk chunk in list) {
                chunk.copyBoundaryVoxelsJobHandle.Value.Complete();
            }

            GenericBoundaryData<Voxel> jobData = new GenericBoundaryData<Voxel>();
            jobData.planes = new UnsafeList<GenericBoundaryPlane<Voxel>>(3, Allocator.TempJob);
            jobData.edges = new UnsafeList<GenericBoundaryEdge<Voxel>>(3, Allocator.TempJob);
            
            BitField32 bits = new BitField32(0);
            
            // map the planes
            for (int i = 0; i < 3; i++) {
                Plane plane = planes[i];

                int type = -1;
                if (plane is UniformPlane uniform) {
                    jobData.planes.Add(new GenericBoundaryPlane<Voxel> {
                        uniform = (Voxel*)uniform.neighbour.negativeBoundaryVoxels.GetUnsafeReadOnlyPtr(),
                        lod0s = new UnsafePtrList<Voxel>(),
                        lod1 = null,
                        relativeOffset = 0,
                    });
                    type = 0;
                } else if (plane is LoToHiPlane loToHi) {
                    UnsafePtrList<Voxel> lod0s = new UnsafePtrList<Voxel>(4, Allocator.TempJob);

                    for (int n = 0; n < 4; n++) {
                        lod0s.Add(loToHi.lod0Neighbours[n].negativeBoundaryVoxels.GetUnsafeReadOnlyPtr());
                    }

                    jobData.planes.Add(new GenericBoundaryPlane<Voxel> {
                        uniform = null,
                        lod0s = lod0s,
                        lod1 = null,
                        relativeOffset = 0,
                    });
                    type = 1;
                } else if (plane is HiToLoPlane hiToLo) {
                    jobData.planes.Add(new GenericBoundaryPlane<Voxel> {
                        uniform = null,
                        lod0s = new UnsafePtrList<Voxel>(),
                        lod1 = (Voxel*)hiToLo.lod1Neighbour.negativeBoundaryVoxels.GetUnsafeReadOnlyPtr(),
                        relativeOffset = hiToLo.relativeOffset,
                    });
                    type = 2;
                }

                bits.SetBits(i * 2, (type & 1) == 1);
                bits.SetBits(i * 2 + 1, (type & 2) == 2);
            }

            // map the edges
            for (int i = 0; i < 3; i++) {
                Edge edge = edges[i];

                int type = -1;
                if (edge is UniformEdge uniform) {
                    jobData.edges.Add(new GenericBoundaryEdge<Voxel> {
                        uniform = (Voxel*)uniform.neighbour.negativeBoundaryVoxels.GetUnsafeReadOnlyPtr(),
                        lod0s = new UnsafePtrList<Voxel>(),
                        lod1 = null,
                        relativeOffset = 0,
                    });
                    type = 0;
                } else if (edge is LoToHiEdge loToHi) {
                    UnsafePtrList<Voxel> lod0s = new UnsafePtrList<Voxel>(2, Allocator.TempJob);

                    for (int n = 0; n < 2; n++) {
                        lod0s.Add(loToHi.lod0Neighbours[n].negativeBoundaryVoxels.GetUnsafeReadOnlyPtr());
                    }

                    jobData.edges.Add(new GenericBoundaryEdge<Voxel> {
                        uniform = null,
                        lod0s = lod0s,
                        lod1 = null,
                        relativeOffset = 0,
                    });
                    type = 1;
                } else if (edge is HiToLoEdge hiToLo) {
                    jobData.edges.Add(new GenericBoundaryEdge<Voxel> {
                        uniform = null,
                        lod0s = new UnsafePtrList<Voxel>(),
                        lod1 = (Voxel*)hiToLo.lod1Neighbour.negativeBoundaryVoxels.GetUnsafeReadOnlyPtr(),
                        relativeOffset = hiToLo.relativeOffset,
                    });
                    type = 2;
                }

                bits.SetBits(i * 2 + 6, (type & 1) == 1);
                bits.SetBits(i * 2 + 1 + 6, (type & 2) == 2);
            }

            // map the corner
            {
                int type = -1;
                if (corner is UniformCorner uniform) {
                    jobData.corner = new GenericBoundaryCorner<Voxel> {
                        uniform = (Voxel*)uniform.neighbour.negativeBoundaryVoxels.GetUnsafeReadOnlyPtr(),
                        lod0 = null,
                        lod1 = null,
                    };
                    type = 0;
                } else if (corner is LoToHiCorner loToHi) {
                    jobData.corner = new GenericBoundaryCorner<Voxel> {
                        uniform = null,
                        lod0 = (Voxel*)loToHi.lod0Neighbour.negativeBoundaryVoxels.GetUnsafeReadOnlyPtr(),
                        lod1 = null,
                    };
                    type = 1;
                } else if (corner is HiToLoCorner hiToLo) {
                    jobData.corner = new GenericBoundaryCorner<Voxel> {
                        uniform = null,
                        lod0 = null,
                        lod1 = (Voxel*)hiToLo.lod1Neighbour.negativeBoundaryVoxels.GetUnsafeReadOnlyPtr(),
                    };
                    type = 2;
                }

                bits.SetBits(12, (type & 1) == 1);
                bits.SetBits(13, (type & 2) == 2);
            }

            jobData.state = bits;
            SampleVoxelsLodJob job = new SampleVoxelsLodJob {
                paddingVoxels = paddingVoxels,
                jobData = jobData,
            };
            job.Schedule(StitchUtils.CalculateBoundaryLength(65), 1024).Complete();
            jobData.Dispose();
            adaptedVoxels = true;
        }

        // Data type for unpacked neighbours (for the stitch jobs)
        // Contains extra data that will be used to transform its negative boundary vertices
        private struct UnpackedNeighbour {
            public VoxelChunk chunk;
            public float3 vertexGlobalOffset;
            public float vertexGlobalSize;

            public static UnpackedNeighbour Uniform(VoxelChunk src, VoxelChunk chunk) {
                float3 offset = chunk.node.position - src.node.position;

                return new UnpackedNeighbour {
                    chunk = chunk,
                    vertexGlobalOffset = offset,
                    vertexGlobalSize = 1f
                };
            }

            public static UnpackedNeighbour LoToHi(VoxelChunk src, VoxelChunk lod0) {
                float3 srcPos = lod0.node.position;
                float3 dstPos = src.node.position;
                float3 offset = (float3)(srcPos - dstPos) / 2f;

                return new UnpackedNeighbour {
                    chunk = lod0,
                    vertexGlobalOffset = offset,
                    vertexGlobalSize = 0.5f
                };
            }

            public static UnpackedNeighbour HiToLo(VoxelChunk src, VoxelChunk lod1) {
                float3 srcPos = lod1.node.position;
                float3 dstPos = src.node.position;
                uint3 offset = (uint3)(srcPos - dstPos);

                return new UnpackedNeighbour {
                    chunk = lod1,
                    vertexGlobalOffset = offset,
                    vertexGlobalSize = 2f
                };
            }
        }

        // Fetch the chunks in an unpacked order, leaving some of them being null
        private UnpackedNeighbour[] CollectNeighboursUnpacked() {
            UnpackedNeighbour[] arr = new UnpackedNeighbour[19];
            Array.Fill(arr, default);
            for (int p = 0; p < 3; p++) {
                Plane plane = planes[p];

                if (plane is UniformPlane uniform) {
                    arr[p * 4] = UnpackedNeighbour.Uniform(source, uniform.neighbour);
                } else if (plane is LoToHiPlane loTohi) {
                    for (int i = 0; i < 4; i++) {
                        arr[p * 4 + i] = UnpackedNeighbour.LoToHi(source, loTohi.lod0Neighbours[i]);
                    }
                } else if (plane is HiToLoPlane hiToLo) {
                    arr[p * 4] = UnpackedNeighbour.HiToLo(source, hiToLo.lod1Neighbour);
                } 
            }

            /*
            for (int e = 0; e < 3; e++) {
                Edge edge = edges[e];

                if (edge is UniformEdge uniform) {
                    arr[e * 2 + 12] = UnpackedNeighbour.Uniform(source, uniform.neighbour);
                } else if (edge is LoToHiEdge loTohi) {
                    for (int i = 0; i < 2; i++) {
                        arr[e * 2 + i + 12] = UnpackedNeighbour.LoToHi(source, loTohi.lod0Neighbours[i]);
                    }
                } else if (edge is HiToLoEdge hiToLo) {
                    arr[e * 2 + 12] = UnpackedNeighbour.HiToLo(source, hiToLo.lod1Neighbour);
                }
            }
            */

            {
                if (corner is UniformCorner uniform) {
                    arr[18] = UnpackedNeighbour.Uniform(source, uniform.neighbour);
                } else if (corner is LoToHiCorner loTohi) {
                    arr[18] = UnpackedNeighbour.LoToHi(source, loTohi.lod0Neighbour);
                } else if (corner is HiToLoCorner hiToLo) {
                    arr[18] = UnpackedNeighbour.HiToLo(source, hiToLo.lod1Neighbour);
                }
            }

            return arr;
        }

        public unsafe void DoTheStitchingThing() {
            // Just makes sure that the copy boundary jobs are done
            {
                List<VoxelChunk> chunks = CollectNeighbours();
                chunks.Insert(0, source);
                for (int i = 0; i < chunks.Count; i++) {
                    chunks[i].copyBoundaryVerticesJobHandle.Value.Complete();
                    chunks[i].copyBoundaryVoxelsJobHandle.Value.Complete();
                }
            }

            // then use padding voxels and extra voxels to create padding vertices of the same resolution as the current chunk
            PaddingVertexJob paddingVertexJob = new PaddingVertexJob {
                paddingVoxels = paddingVoxels,
                boundaryVoxels = boundaryVoxels,
                counter = paddingCounter,
                paddingIndices = paddingIndices,
                vertices = paddingVertices,
            };

            paddingVertexJob.Schedule(StitchUtils.CalculateBoundaryLength(64), 1024).Complete();

            // then copy all the vertices into one big contiguous array
            int totalVertices = 0;
            int worstCaseIndices = 0;

            // create some sort of look up table for vertex index offsets for each of the planes, edges, and corner
            // each index will represent the offset that each chunk should use. this is unpacked data since it makes reading from it in the job easier
            UnpackedNeighbour[] unpackedNeighbours = CollectNeighboursUnpacked();
            NativeArray<int> indexOffsets = new NativeArray<int>(19, Allocator.TempJob);
            NativeArray<int> vertexCounts = new NativeArray<int>(19, Allocator.TempJob);

            // create the copy job that will copy the neighbours' vertices and src vertices to the permanent vertex buffer
            UnsafePtrList<float3> neighbourVertices = new UnsafePtrList<float3>(19, Allocator.TempJob);

            // add the source chunk vertices (boundary + padding)
            int srcCount = boundaryCounter.Count + paddingCounter.Count;
            totalVertices += srcCount;

            // add the neighbour chunks negative boundary vertices (also keep track of offsets and count)
            for (int i = 0; i < 19; i++) {
                VoxelChunk maybe = unpackedNeighbours[i].chunk;
                if (maybe != null) {
                    int count = maybe.negativeBoundaryCounter.Count;
                    indexOffsets[i] = totalVertices;
                    vertexCounts[i] = count;
                    totalVertices += count;
                    neighbourVertices.Add(maybe.negativeBoundaryVertices.GetUnsafeReadOnlyPtr());
                } else {
                    indexOffsets[i] = -1;
                    vertexCounts[i] = -1;
                    neighbourVertices.Add(IntPtr.Zero);
                }
            }
            worstCaseIndices = totalVertices * 5; // idk bro...

            // Ermmm... what the sigmoid functor???
            vertices = new NativeArray<float3>(totalVertices, Allocator.Persistent);


            CopyVerticesStitch copyVerticesStitch = new CopyVerticesStitch {
                indexOffsets = indexOffsets,
                vertexCounts = vertexCounts,
                boundaryVertices = boundaryVertices,
                boundaryVerticesCount = boundaryCounter.Count,
                paddingVertices = paddingVertices,
                paddingVerticesCount = paddingCounter.Count,
                neighbourVertices = neighbourVertices,
                vertices = vertices
            };

            copyVerticesStitch.Schedule().Complete();

            for (int i = 0; i < 19; i++) {
                UnpackedNeighbour maybe = unpackedNeighbours[i];
                if (maybe.chunk != null) {
                    int offset = indexOffsets[i];
                    int count = vertexCounts[i];

                    TransformVerticesStitch transformer = new TransformVerticesStitch {
                        globalOffset = maybe.vertexGlobalOffset,
                        globalScale = maybe.vertexGlobalSize,
                        localVertices = vertices.Slice(offset, count),
                    };

                    transformer.Schedule(count, 1024).Complete();
                }
            }

            /*
            for (int i = 0; i < 19; i++) {
                Debug.Log(indexOffsets[i]);
            }
            */



            // then create quads between padding vertices and boundary vertices (same res)

            // then create quads between neighbouring chunks vertices and padding vertices
            // this must always run at a higher resolution than the source chunk
            // if we have a plane for example of type Uniform, just run it at a 1:1 scale 
            // if it is LoToHi, run the stitch quadding stuff for each of its neighbours (since *they* contain the negative boundary voxels)
            // if it is HiToLo, run the stitch quadding stuff for its LOD1 neighbours (since *it* contains the negative boundary voxels)
            stitched = true;

            indexOffsets.Dispose();
            vertexCounts.Dispose();
            neighbourVertices.Dispose();
        }

        public void Dispose() {
            paddingVoxels.Dispose();
            boundaryVoxels.Dispose();

            boundaryVertices.Dispose();
            boundaryIndices.Dispose();
            boundaryCounter.Dispose();
            
            paddingVertices.Dispose();
            paddingIndices.Dispose();
            paddingCounter.Dispose();

            if (vertices.IsCreated) {
                vertices.Dispose();
            }
        }
    }
}