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
            public static Plane CreateWithNeighbour(VoxelChunk neighbour, bool hiToLow) {
                if (hiToLow) {
                    return new HiToLoPlane() { lod1Neighbour = neighbour };
                } else {
                    return new UniformPlane() { neighbour = neighbour };
                }
            }
        }

        // this=LOD0, neighbour=LOD1
        public class HiToLoPlane : Plane {
            // we only have one LOD1 neighbour
            public VoxelChunk lod1Neighbour;

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
            public static Edge CreateWithNeighbour(VoxelChunk neighbour, bool hiToLow) {
                if (hiToLow) {
                    return new HiToLoEdge() { lod1Neighbour = neighbour };
                } else {
                    return new UniformEdge() { neighbour = neighbour };
                }
            }
        }

        // this=LOD0, diagonal neighbour=LOD1
        public class HiToLoEdge : Edge {
            // we only have one LOD1 neighbour
            public VoxelChunk lod1Neighbour;

            public override bool HasVoxelData() {
                return lod1Neighbour.HasVoxelData();
            }
        }

        // this=LOD1, diagonal neighbour=LOD0
        // means that we need to downsample the data
        public class LoToHiEdge : Edge {
            // We can have up to 2 neighbours in that direction
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

        struct GenericPlane {
            // Uniform neighbour data
            NativeArray<Voxel> uniform;

            // LOD1 neighbour data, not sliced, whole
            NativeArray<Voxel> lod1;

            // LOD0 neighbours (4 of them) data, morton 2D
            UnsafePtrList<Voxel> lod0MortonedNeighbours;
        }

        struct GenericEdge {

        }

        struct GenericCorner {

        }

        public void Dispose() {
            extraVoxels.Dispose();
            boundaryIndices.Dispose();
            boundaryVertices.Dispose();
            boundaryVoxels.Dispose();
        }
    }
}