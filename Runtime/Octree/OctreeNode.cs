using System;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    public struct OctreeNode: IEquatable<OctreeNode> {
        public static OctreeNode Invalid = new OctreeNode {
            parentIndex = -1,
            index = -1,
            depth = -1,
            position = float3.zero,
            size = 0,
            childBaseIndex = -1,
        };

        // TODO: maybe compress this type? make it take a smaller memory footprint...
        public float3 position;
        public int depth;
        public float size;
        public int index;
        public int parentIndex;
        public int childBaseIndex;
        public int neighbourDataStartIndex;

        public float3 Center => math.float3(position) + math.float3(size) / 2.0F;
        public MinMaxAABB Bounds => new MinMaxAABB(position, position + size);


        public static OctreeNode RootNode(int maxDepth, float chunkSize) {
            float size = (math.pow(2.0F, (float)(maxDepth))) * chunkSize;
            OctreeNode node = new OctreeNode();
            node.position = -math.int3(size / 2);
            node.depth = 0;
            node.size = size;
            node.index = 0;
            node.parentIndex = -1;
            node.childBaseIndex = 1;
            return node;
        }

        public bool Equals(OctreeNode other) {
            return math.all(this.position == other.position) &&
                this.depth == other.depth &&
                this.size == other.size &&
                (this.childBaseIndex == -1) == (other.childBaseIndex == -1);
        }

        // https://forum.unity.com/threads/burst-error-bc1091-external-and-internal-calls-are-not-allowed-inside-static-constructors.1347293/
        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 23 + position.GetHashCode();
                hash = hash * 23 + depth.GetHashCode();
                hash = hash * 23 + childBaseIndex.GetHashCode();
                hash = hash * 23 + size.GetHashCode();
                return hash;
            }
        }
    }
}