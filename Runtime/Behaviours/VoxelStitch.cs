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
using UnityEditor;

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
        }

        // this=LOD0, diagonal neighbour=LOD1
        public class HiToLoEdge : Edge {
            // we only have one LOD1 neighbour
            public VoxelChunk lod1Neighbour;

            // relative offset of the LOD0 chunk relative to LOD1 when we are using a vanilla case (edge to edge)
            public uint relativeOffsetVanilla;

            // non vanilla case chunk offsets (edge to mid-face).
            public uint2 relativeOffsetNonVanilla;
            public int nonVanillaPlaneDir;

            // vanilla or non-vanilla
            public bool vanilla;

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
        public List<VoxelChunk> CollectNeighbours() {
            List<VoxelChunk> list = new List<VoxelChunk>();

            for (int i = 0; i < 3; i++) {
                planes[i]?.Collect(list);
            }

            for (int i = 0; i < 3; i++) {
                edges[i]?.Collect(list);
            }

            corner?.Collect(list);
            return list;
        }

        // Check if we can do stitching (if we have our [down/up]-sampled extra voxels and if the neighbouring chunks got their mesh data ready)
        public bool CanStitch() {
            List<VoxelChunk> neighbours = CollectNeighbours();
            bool neighboursValid = neighbours.All(x => x == null || x.HasNegativeBoundaryMeshData());
            bool selfBoundaryVerticesValid = source.copyBoundaryVerticesJobHandle.HasValue && source.copyBoundaryVerticesJobHandle.Value.IsCompleted;
            bool selfBoundaryVoxelsValid = source.copyBoundaryVoxelsJobHandle.HasValue && source.copyBoundaryVoxelsJobHandle.Value.IsCompleted;
            return neighboursValid && selfBoundaryVerticesValid && selfBoundaryVoxelsValid;
        }

        // These are the boundary voxels from the source chunk.
        public NativeArray<Voxel> boundaryVoxels;

        // Copied indices from the source chunk mesh
        // Packed so we only store the indices on the boundary (v=63)
        public NativeArray<float3> boundaryVertices;
        public NativeArray<int> boundaryIndices;
        public NativeCounter boundaryCounter;
        public bool stitched;

        // The generated stitched vertices & triangles for our mesh
        public NativeArray<float3> vertices;
        public NativeArray<int> indices;
        public NativeCounter indexCounter;
        public int[] test;

        public void Init(VoxelChunk self) {
            this.source = self;
            stitched = false;
            int smallerBoundary = StitchUtils.CalculateBoundaryLength(64);
            int boundary = StitchUtils.CalculateBoundaryLength(65);

            // limit=64
            boundaryVoxels = new NativeArray<Voxel>(boundary, Allocator.Persistent);
            
            // limit=63
            boundaryIndices = new NativeArray<int>(smallerBoundary, Allocator.Persistent);
            boundaryVertices = new NativeArray<float3>(smallerBoundary, Allocator.Persistent);

            boundaryCounter = new NativeCounter(Allocator.Persistent);
            indexCounter = new NativeCounter(Allocator.Persistent);

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
            public uint relativeOffsetVanilla;
            public uint2 relativeOffsetNonVanilla;
            public int nonVanillaPlaneDir;
            public bool vanilla;

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

        private unsafe GenericBoundaryData<T> FetchNeighbourGenericData<T>(Func<VoxelChunk, NativeArray<T>> map) where T: unmanaged {
            GenericBoundaryData<T> jobData = new GenericBoundaryData<T>();
            jobData.planes = new UnsafeList<GenericBoundaryPlane<T>>(3, Allocator.TempJob);
            jobData.edges = new UnsafeList<GenericBoundaryEdge<T>>(3, Allocator.TempJob);

            BitField32 bits = new BitField32(0);

            // map the planes
            for (int i = 0; i < 3; i++) {
                Plane plane = planes[i];

                int type = -1;

                if (plane == null) {
                    jobData.planes.Add(default);
                    type = 0;
                } else if (plane is UniformPlane uniform) {
                    jobData.planes.Add(new GenericBoundaryPlane<T> {
                        uniform = (T*)map(uniform.neighbour).GetUnsafeReadOnlyPtr(),
                        lod0s = new UnsafePtrList<T>(),
                        lod1 = null,
                        relativeOffset = 0,
                    });
                    type = 1;
                } else if (plane is LoToHiPlane loToHi) {
                    UnsafePtrList<T> lod0s = new UnsafePtrList<T>(4, Allocator.TempJob);

                    for (int n = 0; n < 4; n++) {
                        lod0s.Add(map(loToHi.lod0Neighbours[n]).GetUnsafeReadOnlyPtr());
                    }

                    jobData.planes.Add(new GenericBoundaryPlane<T> {
                        uniform = null,
                        lod0s = lod0s,
                        lod1 = null,
                        relativeOffset = 0,
                    });
                    type = 2;
                } else if (plane is HiToLoPlane hiToLo) {
                    jobData.planes.Add(new GenericBoundaryPlane<T> {
                        uniform = null,
                        lod0s = new UnsafePtrList<T>(),
                        lod1 = (T*)map(hiToLo.lod1Neighbour).GetUnsafeReadOnlyPtr(),
                        relativeOffset = hiToLo.relativeOffset,
                    });
                    type = 3;
                }

                bits.SetBits(i * 2, (type & 1) == 1);
                bits.SetBits(i * 2 + 1, (type & 2) == 2);
            }

            // map the edges
            for (int i = 0; i < 3; i++) {
                Edge edge = edges[i];

                int type = -1;
                if (edge == null) {
                    jobData.edges.Add(default);
                    type = 0;
                } else if (edge is UniformEdge uniform) {
                    jobData.edges.Add(new GenericBoundaryEdge<T> {
                        uniform = (T*)map(uniform.neighbour).GetUnsafeReadOnlyPtr(),
                        lod0s = new UnsafePtrList<T>(),
                        lod1 = null,
                        relativeOffsetNonVanilla = new uint2(int.MaxValue),
                        relativeOffsetVanilla = uint.MaxValue,
                        nonVanillaPlaneDir = -1,
                        vanilla = false,
                    });
                    type = 1;
                } else if (edge is LoToHiEdge loToHi) {
                    UnsafePtrList<T> lod0s = new UnsafePtrList<T>(2, Allocator.TempJob);

                    for (int n = 0; n < 2; n++) {
                        lod0s.Add(map(loToHi.lod0Neighbours[n]).GetUnsafeReadOnlyPtr());
                    }

                    jobData.edges.Add(new GenericBoundaryEdge<T> {
                        uniform = null,
                        lod0s = lod0s,
                        lod1 = null,
                        relativeOffsetNonVanilla = new uint2(int.MaxValue),
                        relativeOffsetVanilla = uint.MaxValue,
                        nonVanillaPlaneDir = -1,
                        vanilla = false,
                    });
                    type = 2;
                } else if (edge is HiToLoEdge hiToLo) {
                    jobData.edges.Add(new GenericBoundaryEdge<T> {
                        uniform = null,
                        lod0s = new UnsafePtrList<T>(),
                        lod1 = (T*)map(hiToLo.lod1Neighbour).GetUnsafeReadOnlyPtr(),
                        relativeOffsetNonVanilla = hiToLo.relativeOffsetNonVanilla,
                        relativeOffsetVanilla = hiToLo.relativeOffsetVanilla,
                        vanilla = hiToLo.vanilla,
                        nonVanillaPlaneDir = hiToLo.nonVanillaPlaneDir,
                    });
                    type = 3;
                }

                bits.SetBits(i * 2 + 6, (type & 1) == 1);
                bits.SetBits(i * 2 + 1 + 6, (type & 2) == 2);
            }

            // map the corner
            {
                int type = -1;
                if (corner == null) {
                    jobData.corner = default;
                    type = 0;
                } else if (corner is UniformCorner uniform) {
                    jobData.corner = new GenericBoundaryCorner<T> {
                        uniform = (T*)map(uniform.neighbour).GetUnsafeReadOnlyPtr(),
                        lod0 = null,
                        lod1 = null,
                    };
                    type = 1;
                } else if (corner is LoToHiCorner loToHi) {
                    jobData.corner = new GenericBoundaryCorner<T> {
                        uniform = null,
                        lod0 = (T*)map(loToHi.lod0Neighbour).GetUnsafeReadOnlyPtr(),
                        lod1 = null,
                    };
                    type = 2;
                } else if (corner is HiToLoCorner hiToLo) {
                    jobData.corner = new GenericBoundaryCorner<T> {
                        uniform = null,
                        lod0 = null,
                        lod1 = (T*)map(hiToLo.lod1Neighbour).GetUnsafeReadOnlyPtr(),
                    };
                    type = 3;
                }

                bits.SetBits(12, (type & 1) == 1);
                bits.SetBits(13, (type & 2) == 2);
            }

            //bits.SetBits(0, false, 6);
            jobData.state = bits;
            return jobData;
        }

        // Data type for unpacked neighbours (for the stitch jobs)
        // Contains extra data that will be used to transform its negative boundary vertices
        [Serializable]
        public struct UnpackedNeighbour {
            public VoxelChunk chunk;
            public float3 vertexGlobalOffset;
            public float vertexGlobalSize;

            public static UnpackedNeighbour Uniform(VoxelChunk src, VoxelChunk chunk) {
                float3 offset = (chunk.node.position - src.node.position) / (src.node.size / 64f);

                return new UnpackedNeighbour {
                    chunk = chunk,
                    vertexGlobalOffset = offset,
                    vertexGlobalSize = 1f
                };
            }

            public static UnpackedNeighbour LoToHi(VoxelChunk src, VoxelChunk lod0) {
                float3 srcPos = lod0.node.position;
                float3 dstPos = src.node.position;
                float3 offset = (float3)(srcPos - dstPos) / (src.node.size / 64f);

                return new UnpackedNeighbour {
                    chunk = lod0,
                    vertexGlobalOffset = offset,
                    vertexGlobalSize = 0.5f
                };
            }

            public static UnpackedNeighbour HiToLo(VoxelChunk src, VoxelChunk lod1) {
                float3 srcPos = lod1.node.position;
                float3 dstPos = src.node.position;
                float3 offset = (float3)(srcPos - dstPos) / (src.node.size / 64f);

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

        public UnpackedNeighbour[] wtf;

        public unsafe void DoTheStitchingThing() {
            // Just makes sure that the copy boundary jobs are done
            {
                source.copyBoundaryVerticesJobHandle.Value.Complete();
                source.copyBoundaryVoxelsJobHandle.Value.Complete();

                // we will NOT use the voxel data of the neighbours, but we still need to complete the job otherwise it will complain desu
                List<VoxelChunk> chunks = CollectNeighbours();
                for (int i = 0; i < chunks.Count; i++) {
                    if (chunks[i].copyBoundaryVerticesJobHandle.HasValue) {
                        chunks[i].copyBoundaryVerticesJobHandle.Value.Complete();
                        chunks[i].copyBoundaryVoxelsJobHandle.Value.Complete();
                    }
                }
            }

            // then copy all the vertices into one big contiguous array
            int totalVertices = 0;
            int worstCaseIndices = 0;

            // create some sort of look up table for vertex index offsets for each of the planes, edges, and corner
            // each index will represent the offset that each chunk should use. this is unpacked data since it makes reading from it in the job easier
            UnpackedNeighbour[] unpackedNeighbours = CollectNeighboursUnpacked();
            wtf = unpackedNeighbours;
            NativeArray<int> indexOffsets = new NativeArray<int>(19, Allocator.TempJob);
            NativeArray<int> vertexCounts = new NativeArray<int>(19, Allocator.TempJob);

            // create the copy job that will copy the neighbours' vertices and src vertices to the permanent vertex buffer
            UnsafePtrList<float3> neighbourVertices = new UnsafePtrList<float3>(19, Allocator.TempJob);

            // add the source chunk vertices (boundary + padding)
            totalVertices += boundaryCounter.Count;

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

            // Ermmm... what the sigmoid functor???
            worstCaseIndices = totalVertices * 5; // idk bro...
            vertices = new NativeArray<float3>(totalVertices, Allocator.Persistent);
            indices = new NativeArray<int>(worstCaseIndices, Allocator.Persistent);

            CopyVerticesStitch copyVerticesStitch = new CopyVerticesStitch {
                indexOffsets = indexOffsets,
                vertexCounts = vertexCounts,
                boundaryVertices = boundaryVertices,
                boundaryVerticesCount = boundaryCounter.Count,
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

            // collect neighbour indices and voxels...
            GenericBoundaryData<Voxel> neighbourBoundaryVoxels = FetchNeighbourGenericData<Voxel>(x => x.negativeBoundaryVoxels);
            GenericBoundaryData<int> neighbourBoundaryIndices = FetchNeighbourGenericData<int>(x => x.negativeBoundaryIndices);

            // then create quads between neighbouring chunks vertices and padding vertices
            // this must always run at a higher resolution than the source chunk
            // if we have a plane for example of type Uniform, just run it at a 1:1 scale 
            // if it is LoToHi, run the stitch quadding stuff for each of its neighbours (since *they* contain the negative boundary voxels)
            // if it is HiToLo, run the stitch quadding stuff for its LOD1 neighbours (since *it* contains the negative boundary voxels)
            // since we store the extra voxel we can chose between two sets, the source (boundary voxels) or neighbours (negative boundary voxels). we must chose the set that's obvi higher res
            debugDataStuff = new NativeArray<float4>(StitchUtils.CalculateBoundaryLength(65), Allocator.Persistent);
            StitchQuadJob stitchJob = new StitchQuadJob {
                debugData = debugDataStuff,
                srcBoundaryVoxels = boundaryVoxels,
                srcBoundaryIndices = boundaryIndices,
                indexCounter = indexCounter,
                indexOffsets = indexOffsets,
                neighbourIndices = neighbourBoundaryIndices,
                neighbourVoxels = neighbourBoundaryVoxels,
                indices = indices,
            };

            stitchJob.Schedule(StitchUtils.CalculateBoundaryLength(65), 2048).Complete();
            test = indices.ToArray();
            //Debug.Log(indexCounter.Count);
            debugIntVal = neighbourBoundaryIndices.state.Value;

            if (totalVertices > 0) {
                // create new arrays that will store "packed" data (discard the vertices that weren't used in the stitching)
                NativeArray<float3> packedVertices = new NativeArray<float3>(totalVertices, Allocator.TempJob);
                NativeArray<int> lookUp = new NativeArray<int>(totalVertices, Allocator.TempJob);
                NativeArray<int> packedIndices = new NativeArray<int>(worstCaseIndices, Allocator.TempJob);

                // run a job that will get rid of unused vertices
                NativeBitArray remappedVerticesBitArray = new NativeBitArray(totalVertices, Allocator.TempJob);
                PostStitchCleanUpJob cleanUp = new PostStitchCleanUpJob {
                    srcIndices = indices,
                    srcVertices = vertices,
                    dstIndices = packedIndices,
                    dstVertices = packedVertices,
                    indexCount = indexCounter.Count,
                    remappedVertices = remappedVerticesBitArray,
                    lookUp = lookUp,
                };
                cleanUp.Schedule().Complete();

                int packedVertexCount = remappedVerticesBitArray.CountBits(0, totalVertices);

                MeshFilter filter = GetComponent<MeshFilter>();
                Mesh mesh = new Mesh();
                mesh.vertices = packedVertices.Reinterpret<Vector3>().GetSubArray(0, packedVertexCount).ToArray();
                mesh.triangles = packedIndices.GetSubArray(0, indexCounter.Count).ToArray();
                filter.mesh = mesh;

                packedIndices.Dispose();
                packedVertices.Dispose();
                remappedVerticesBitArray.Dispose();
                lookUp.Dispose();
            }

            stitched = true;
            indexOffsets.Dispose();
            vertexCounts.Dispose();
            neighbourVertices.Dispose();
            neighbourBoundaryIndices.Dispose();
            neighbourBoundaryVoxels.Dispose();

        }

        public NativeArray<float4> debugDataStuff;
        public uint debugIntVal;

        public void Dispose() {
            boundaryVoxels.Dispose();

            boundaryVertices.Dispose();
            boundaryIndices.Dispose();
            indexCounter.Dispose();
            boundaryCounter.Dispose();
            
            if (vertices.IsCreated) {
                vertices.Dispose();
            }

            if (indices.IsCreated) {
                indices.Dispose();
                debugDataStuff.Dispose();
            }
        }
    }
}