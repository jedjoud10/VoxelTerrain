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
        interface IHasVoxelData {
            bool HasVoxelData();
        }

        // Stitch boundary (plane) (stored 3 times for each of the x,y,z axii)
        public abstract class Plane : IHasVoxelData {
            public abstract bool HasVoxelData();
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

            public override bool HasVoxelData() {
                return lod1Neighbour.HasVoxelData();
            }
        }

        // this=LOD1, neighbour=LOD0
        // means that we need to downsample the data
        public class LoToHiPlane : Plane {
            // We can have up to 4 neighbours in that direction
            public VoxelChunk[] lod0Neighbours;

            public override bool HasVoxelData() {
                return lod0Neighbours.All(x => x != null && x.HasVoxelData());
            }
        }

        // this=LOD0, neighbour=LOD0
        public class UniformPlane : Plane {
            public VoxelChunk neighbour;

            public override bool HasVoxelData() {
                return neighbour.HasVoxelData();
            }
        }

        // Stitch edge (line) (stored 3 times for each of the x,y,z axii)
        public abstract class Edge : IHasVoxelData {
            public abstract bool HasVoxelData();
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

            public override bool HasVoxelData() {
                return lod1Neighbour.HasVoxelData();
            }
        }

        // this=LOD1, diagonal neighbour=LOD0
        // means that we need to downsample the data
        public class LoToHiEdge : Edge {
            // we can have up to 2 neighbours in that direction
            public VoxelChunk[] lod0Neighbours;

            public override bool HasVoxelData() {
                return lod0Neighbours.All(x => x != null && x.HasVoxelData());
            }
        }

        // this=LOD0, neighbour=LOD0
        public class UniformEdge : Edge {
            public VoxelChunk neighbour;

            public override bool HasVoxelData() {
                return neighbour.HasVoxelData();
            }
        }

        // Stitch corner (point) stored one since we only have one corner
        public abstract class Corner : IHasVoxelData {
            public abstract bool HasVoxelData();
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

            public override bool HasVoxelData() {
                return lod1Neighbour.HasVoxelData();
            }
        }

        // this=LOD1, corner neighbour=LOD0
        // means that we need to downsample the data
        public class LoToHiCorner : Corner  {
            // we only have one LOD0 neighbour
            public VoxelChunk lod0Neighbour;

            public override bool HasVoxelData() {
                return lod0Neighbour.HasVoxelData();
            }
        }

        // this=LOD0, neighbour=LOD0
        public class UniformCorner : Corner {
            public VoxelChunk neighbour;

            public override bool HasVoxelData() {
                return neighbour.HasVoxelData();
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

        // Check if we can adapt neighbouring voxels to our padding voxels array
        // This requires us to have access to all the neighbouring chunks in the positive axii in 3D ABD also that they have valid voxel data
        public bool CanSampleExtraVoxels() {
            bool valid = planes.All(x => x != null) && edges.All(x => x != null) && corner != null;

            if (!valid)
                return false;

            bool hasVoxelData = planes.All(x => x.HasVoxelData()) && edges.All(x => x.HasVoxelData()) && corner.HasVoxelData();
            return hasVoxelData;
        }

        // Check if we can do stitching (if we have our [down/up]-sampled extra voxels)
        public bool CanStitch() {
            return adaptedVoxels;
        }


        // STORING UPSAMPLED / DONWSAMPLED DATA:
        // We basically need to store 65^3 voxels to be able to create that "padding" vertex on the boundary
        // I first store the values for the 3 faces but only the region of 64x64 voxels sequentially
        // Then I store the edges separately, x,y,z
        // Then I store the corner piece by itself, last value
        // Down/up-sampling is done SEPARATELY FROM STITCHING
        public NativeArray<Voxel> extraVoxels;

        // These are the boundary voxels from the source chunk. Compacted the same way as extraVoxels but with size=64
        public NativeArray<Voxel> boundaryVoxels;

        private bool adaptedVoxels;

        // Copied indices from the source chunk mesh
        // Packed so we only store the indices on the boundary (x=62 | y=62 | z=62)
        public NativeArray<int> boundaryIndices;

        // Also copied from the source mesh, but this time to match up with the boundary values since these are packed
        public NativeArray<float3> boundaryVertices;

        public void Init() {
            int smallerBoundary = StitchUtils.CalculateBoundaryLength(63);
            int boundary = StitchUtils.CalculateBoundaryLength(64);
            int paddedBoundary = StitchUtils.CalculateBoundaryLength(65);

            // limit=64
            extraVoxels = new NativeArray<Voxel>(paddedBoundary, Allocator.Persistent);
            
            // limit=63
            boundaryVoxels = new NativeArray<Voxel>(boundary, Allocator.Persistent);
            
            // limit=62
            boundaryIndices = new NativeArray<int>(smallerBoundary, Allocator.Persistent);
            boundaryVertices = new NativeArray<float3>(smallerBoundary, Allocator.Persistent);
            
            adaptedVoxels = false;

            // Set the boundary helpers to null since we haven't set them up yet
            planes = new Plane[3] { null, null, null };
            edges = new Edge[3] { null, null, null };
            corner = null;
        }

        public unsafe struct GenericPlane {
            // Uniform neighbour data
            [ReadOnly]
            public Voxel* uniform;

            // LOD1 neighbour data, not sliced, whole
            [ReadOnly]
            public Voxel* lod1;
            public uint2 relativeOffset;

            // LOD0 neighbours (4 of them) data, morton 2D
            [ReadOnly]
            public UnsafePtrList<Voxel> lod0s;
        }

        public unsafe struct GenericEdge {
            // Uniform neighbour data
            [ReadOnly]
            public Voxel* uniform;

            // LOD1 neighbour data, not sliced, whole
            [ReadOnly]
            public Voxel* lod1;
            public uint relativeOffset;

            // LOD0 neighbours (2 of them) data
            [ReadOnly]
            public UnsafePtrList<Voxel> lod0s;
        }

        public unsafe struct GenericCorner {
            // Uniform neighbour data
            [ReadOnly]
            public Voxel* uniform;

            // LOD1 neighbour data, not sliced, whole
            [ReadOnly]
            public Voxel* lod1;

            // LOD0 neighbour data
            [ReadOnly]
            public Voxel* lod0;
        }

        public struct JobData {
            [ReadOnly]
            public UnsafeList<GenericPlane> planes;
            [ReadOnly]
            public UnsafeList<GenericEdge> edges;
            [ReadOnly]
            public GenericCorner corner;

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
            JobData jobData = new JobData();
            jobData.planes = new UnsafeList<GenericPlane>(3, Allocator.TempJob);
            jobData.edges = new UnsafeList<GenericEdge>(3, Allocator.TempJob);
            BitField32 bits = new BitField32(0);

            // map the planes
            for (int i = 0; i < 3; i++) {
                Plane plane = planes[i];

                int type = -1;
                if (plane is UniformPlane uniform) {
                    jobData.planes.Add(new GenericPlane {
                        uniform = (Voxel*)uniform.neighbour.voxels.GetUnsafeReadOnlyPtr(),
                        lod0s = new UnsafePtrList<Voxel>(),
                        lod1 = null,
                        relativeOffset = 0,
                    });
                    type = 0;
                } else if (plane is LoToHiPlane loToHi) {
                    UnsafePtrList<Voxel> lod0s = new UnsafePtrList<Voxel>(4, Allocator.TempJob);

                    for (int n = 0; n < 4; n++) {
                        lod0s.Add(loToHi.lod0Neighbours[n].voxels.GetUnsafeReadOnlyPtr());
                    }

                    jobData.planes.Add(new GenericPlane {
                        uniform = null,
                        lod0s = lod0s,
                        lod1 = null,
                        relativeOffset = 0,
                    });
                    type = 1;
                } else if (plane is HiToLoPlane hiToLo) {
                    jobData.planes.Add(new GenericPlane {
                        uniform = null,
                        lod0s = new UnsafePtrList<Voxel>(),
                        lod1 = (Voxel*)hiToLo.lod1Neighbour.voxels.GetUnsafeReadOnlyPtr(),
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
                    jobData.edges.Add(new GenericEdge {
                        uniform = (Voxel*)uniform.neighbour.voxels.GetUnsafeReadOnlyPtr(),
                        lod0s = new UnsafePtrList<Voxel>(),
                        lod1 = null,
                        relativeOffset = 0,
                    });
                    type = 0;
                } else if (edge is LoToHiEdge loToHi) {
                    UnsafePtrList<Voxel> lod0s = new UnsafePtrList<Voxel>(2, Allocator.TempJob);

                    for (int n = 0; n < 2; n++) {
                        lod0s.Add(loToHi.lod0Neighbours[n].voxels.GetUnsafeReadOnlyPtr());
                    }

                    jobData.edges.Add(new GenericEdge {
                        uniform = null,
                        lod0s = lod0s,
                        lod1 = null,
                        relativeOffset = 0,
                    });
                    type = 1;
                } else if (edge is HiToLoEdge hiToLo) {
                    jobData.edges.Add(new GenericEdge {
                        uniform = null,
                        lod0s = new UnsafePtrList<Voxel>(),
                        lod1 = (Voxel*)hiToLo.lod1Neighbour.voxels.GetUnsafeReadOnlyPtr(),
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
                    jobData.corner = new GenericCorner {
                        uniform = (Voxel*)uniform.neighbour.voxels.GetUnsafeReadOnlyPtr(),
                        lod0 = null,
                        lod1 = null,
                    };
                    type = 0;
                } else if (corner is LoToHiCorner loToHi) {
                    jobData.corner = new GenericCorner {
                        uniform = null,
                        lod0 = (Voxel*)loToHi.lod0Neighbour.voxels.GetUnsafeReadOnlyPtr(),
                        lod1 = null,
                    };
                    type = 1;
                } else if (corner is HiToLoCorner hiToLo) {
                    jobData.corner = new GenericCorner {
                        uniform = null,
                        lod0 = null,
                        lod1 = (Voxel*)hiToLo.lod1Neighbour.voxels.GetUnsafeReadOnlyPtr(),
                    };
                    type = 2;
                }

                bits.SetBits(12, (type & 1) == 1);
                bits.SetBits(13, (type & 2) == 2);
            }

            jobData.state = bits;
            SampleVoxelsLodJob job = new SampleVoxelsLodJob {
                paddingVoxels = extraVoxels,
                jobData = jobData,
            };
            job.Schedule(StitchUtils.CalculateBoundaryLength(65), 1024).Complete();
            jobData.Dispose();
        }

        public void Dispose() {
            extraVoxels.Dispose();
            boundaryIndices.Dispose();
            boundaryVertices.Dispose();
            boundaryVoxels.Dispose();
        }
    }
}