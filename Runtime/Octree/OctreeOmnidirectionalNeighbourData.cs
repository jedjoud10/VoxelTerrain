using System;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    public struct OctreeOmnidirectionalNeighbourData {
        public enum Mode {
            // single neighbour
            SameLod,

            // single neighbour
            HigherLod,

            // multi-neighbour
            // we always assume a 2:1 ratio between neighbours
            // if the direction is a plane, then we will have at most 4 neighbours
            // if the direction is an edge, then we will have at most 2 neighbours
            // if the direction 
            LowerLod,
        }

        public int baseIndex;
        public Mode mode;

        public bool IsValid() {
            return baseIndex != -1;
        }

        public static readonly OctreeOmnidirectionalNeighbourData Invalid = new OctreeOmnidirectionalNeighbourData {
            baseIndex = -1
        };
    }
}