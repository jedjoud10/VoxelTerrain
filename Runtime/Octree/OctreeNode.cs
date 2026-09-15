using System;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Octree {
    [Serializable]
    public struct OctreeNode: IEquatable<OctreeNode> {
        public static OctreeNode Invalid = new OctreeNode {
            parentIndex = -1,
            index = -1,
            depth = -1,
            position = int3.zero,
            size = 0,
            childBaseIndex = -1,
        };

        public int3 position;
        public int depth;
        public int size;
        public int index;
        public int parentIndex;
        public bool atMaxDepth;
        public int childBaseIndex;

        public float3 Center => math.float3(position) + math.float3(size) / 2.0F;
        public Unity.Mathematics.Geometry.MinMaxAABB Bounds => new Unity.Mathematics.Geometry.MinMaxAABB { Min = position, Max = position + size };


        public static OctreeNode RootNode(int maxDepth, int chunkSize) {
            int size = (int)(math.pow(2.0F, maxDepth)) * chunkSize;
            OctreeNode node = new OctreeNode();
            node.position = -math.int3(size / 2);
            node.depth = 0;
            node.size = size;
            node.index = 0;
            node.parentIndex = -1;
            node.childBaseIndex = 1;
            node.atMaxDepth = false;
            return node;
        }

        public static OctreeNode LeafLodZeroNode(int3 chunkPosition, int maxDepth, int chunkSize) {
            return new OctreeNode {
                size = chunkSize,
                childBaseIndex = -1,
                depth = maxDepth,
                index = -1,
                parentIndex = -1,
                atMaxDepth = true,
                position = chunkPosition * chunkSize,
            };
        }

        public bool Equals(OctreeNode other) {
            return math.all(this.position == other.position) &&
                this.depth == other.depth &&
                this.size == other.size &&
                (this.childBaseIndex == -1) == (other.childBaseIndex == -1);
        }

        public override string ToString() {
            return $"index={index}, pos={position}, d={depth}, s={size}";
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