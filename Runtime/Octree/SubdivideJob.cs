using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    [BurstCompile(CompileSynchronously = true)]
    public struct SubdivideJob : IJob {
        public NativeList<OctreeNode> nodes;
        public NativeList<OctreeOmnidirectionalNeighbourData> omniDirectionalNeighbourData;
        public NativeQueue<OctreeNode> pending;

        [ReadOnly]
        public OctreeLoader.Target target;

        [ReadOnly] public int maxDepth;

        public void Execute() {
            while (pending.TryDequeue(out OctreeNode node)) {
                MinMaxAABB targetBounds = MinMaxAABB.CreateFromCenterAndHalfExtents(target.center, target.radius);

                // AABB bounds method
                //bool subdivide = node.Bounds.Overlaps(targetBounds);

                // relative distance method
                bool subdivide = math.distance(node.Center, target.center) < target.radius * node.size;

                if (subdivide && node.depth < maxDepth) {
                    Subdivide(node, true);
                }
            }

            // keep subdiving leaf nodes that require it for the neighbour 2:1 ratio
            int copy = nodes.Length;

            // safe guard...
            for (int d = 0; d < maxDepth; d++) {
                copy = nodes.Length;
                bool any = false;
                for (int i = 0; i < copy; i++) {
                    OctreeNode node = nodes[i];
                    // is this fucking studid?
                    // yes. yes it fucking is. it's atrociously slow
                    // do I care? no. not at all
                    // it's simple and even though it takes 50 FUCKING ms at depth=8 idgaf
                    if (node.childBaseIndex == -1 && node.depth < (maxDepth-1) && SubdivideTwoToOne(node)) {
                        Subdivide(node, false);
                        any = true;
                    }
                }

                if (!any) {
                    break;
                }
            }

            // loop over the nodes again and add the omniDirectionalNeighbourData for the leaf ones
            copy = nodes.Length;
            for (int i = 0; i < copy; i++) {
                OctreeNode node = nodes[i];
                if (node.childBaseIndex == -1) {
                    int index = omniDirectionalNeighbourData.Length;
                    omniDirectionalNeighbourData.AddReplicate(OctreeOmnidirectionalNeighbourData.Invalid, 27);
                    node.neighbourDataBaseIndex = index;
                    nodes[i] = node;
                }
            }
        }

        private void Subdivide(OctreeNode node, bool enqueue) {
            node.childBaseIndex = nodes.Length;

            for (int i = 0; i < 8; i++) {
                float3 offset = math.float3(VoxelUtils.OCTREE_CHILD_OFFSETS[i]);
                OctreeNode child = new OctreeNode {
                    position = offset * (node.size / 2.0F) + node.position,
                    depth = node.depth + 1,
                    size = node.size / 2,
                    parentIndex = node.index,
                    index = node.childBaseIndex + i,
                    childBaseIndex = -1,
                };

                if (enqueue) {
                    pending.Enqueue(child);
                }
                nodes.Add(child);
            }

            nodes[node.index] = node;
        }


        private bool CheckIfAnyNonTwoToOneNeighbours(MinMaxAABB bounds, int ogDepth) {
            pending.Clear();
            pending.Enqueue(nodes[0]);

            while (pending.TryDequeue(out OctreeNode node)) {
                for (int c = 0; c < 8; c++) {
                    OctreeNode child = nodes[node.childBaseIndex + c];
                    if (child.Bounds.Overlaps(bounds)) {
                        if (child.childBaseIndex == -1) {
                            int lodGap = math.abs(ogDepth - child.depth);

                            if (lodGap > 1) {
                                return true;
                            }
                        } else {
                            pending.Enqueue(child);
                        }
                    }
                }
            }

            return false;
        }

        private bool SubdivideTwoToOne(OctreeNode node) {
            MinMaxAABB bounds = node.Bounds;
            float3 min = bounds.Min;
            float3 max = bounds.Max;

            // planes only. 
            for (int i = 0; i < 6; i++) {
                // convert to 0-26 index for the omni directional data
                int omniDirOffsetIndex = VoxelUtils.PosToIndex((uint3)(NeighbourJob.DIRECTIONS[i] + 1), 3);
                MinMaxAABB plane = NeighbourJob.CreatePlane(min, max, i);

                if (CheckIfAnyNonTwoToOneNeighbours(plane, node.depth)) {
                    return true;
                }
            }

            // edges only. 
            for (int i = 0; i < 3; i++) {
                int3 dir = NeighbourJob.DIRECTIONS[i + 3];

                // for each axis we have 4 edges
                for (int l = 0; l < 4; l++) {
                    uint2 mortoned = VoxelUtils.IndexToPos2D(l, 2) * 2;
                    uint3 offset = 1;

                    if (i == 0) {
                        offset.yz = mortoned;
                    } else if (i == 1) {
                        offset.xz = mortoned;
                    } else if (i == 2) {
                        offset.xy = mortoned;
                    }

                    MinMaxAABB edge = NeighbourJob.CreateEdge(min, max, i, l);

                    if (CheckIfAnyNonTwoToOneNeighbours(edge, node.depth)) {
                        return true;
                    }
                }
            }

            // corners only
            for (int i = 0; i < 8; i++) {
                uint3 mortoned = VoxelUtils.IndexToPos(i, 2) * 2;
                int omniDirOffsetIndex = VoxelUtils.PosToIndex(mortoned, 3);
                MinMaxAABB corner = NeighbourJob.CreateCorner(min, max, i);

                if (CheckIfAnyNonTwoToOneNeighbours(corner, node.depth)) {
                    return true;
                }
            }

            // no need to subdivide!
            return false;
        }
    }
}