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
                return lod0Neighbours.All(x => x.HasVoxelData());
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
                return lod0Neighbours.All(x => x.HasVoxelData());
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

        // X,Y,Z
        public Plane[] planes;
        
        // X,Y,Z
        public Edge[] edges;
        
        // Corner
        public Corner corner;

        // Check if we can adapt neighbouring voxels to our padding voxels array
        // This requires us to have access to all the neighbouring chunks in the positive axii in 3D ABD also that they have valid voxel data
        public bool CanAdaptVoxels() {
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
        // You could make some sort of 2D face voxels, but that will discard edge and corner piece in 3D
        // I have opted to make a compacted voxel array that only stores the voxel values at (x==65 || y==65 || z==65)
        // Then we use a lookup table to convert from 3D flattened index into index for that data
        // Down/up-sampling is done SEPARATELY FROM STITCHING
        public NativeArray<Voxel> extraVoxels;
        private bool adaptedVoxels;

        public void Init() {
            // Principle of inclusion/exclusion but with 3 sets and minus the main set (64x64x64)
            int count = (65*65) * 3 - 3 * 65 + 1;
            extraVoxels = new NativeArray<Voxel>(count, Allocator.Persistent);
            adaptedVoxels = false;

            // Set the boundary helpers to null since we haven't set them up yet
            planes = new Plane[3] { null, null, null };
            edges = new Edge[3] { null, null, null };
            corner = null;
        }

        public void Dispose() {
            extraVoxels.Dispose();
        }
    }
}