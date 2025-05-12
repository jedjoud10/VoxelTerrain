using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SkirtVertexJob : IJobParallelFor {                
        // whole source chunk voxels
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        // indices for the skirt vertices 
        // first 66*66 vertices are from the border of the chunk (already generated)
        // next 66*66 vertices are the ones we generate in this job here
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> skirtVertexIndices;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        // first 66*66 vertices are from the border of the chunk (already generated)
        // next 66*66 vertices are the ones we generate in this job here
        public NativeArray<float3> skirtVertices;

        public Unsafe.NativeCounter.Concurrent skirtVertexCounter;

        // Positions of the first vertex in edges
        public static readonly uint2[] EDGE_POSITIONS_0_CUSTOM = new uint2[] {
            new uint2(0, 0),
            new uint2(0, 1),
            new uint2(1, 1),
            new uint2(1, 0),
        };

        // Positions of the second vertex in edges
        public static readonly uint2[] EDGE_POSITIONS_1_CUSTOM = new uint2[] {
            new uint2(0, 1),
            new uint2(1, 1),
            new uint2(1, 0),
            new uint2(0, 0),
        };

        const int FACE = VoxelUtils.SIZE * VoxelUtils.SIZE;

        public float threshold;

        public void Execute(int index) {
            int face = index / FACE;
            int direction = face % 3;
            bool negative = face < 3;

            uint missing = negative ? 0 : ((uint)VoxelUtils.SIZE - 2);

            int localIndex = index % FACE;
            int indexIndex = localIndex + FACE + 2 * face * FACE;

            uint2 flatten = VoxelUtils.IndexToPos2D(localIndex, VoxelUtils.SIZE);
            uint3 position = SkirtUtils.UnflattenFromFaceRelative(flatten, direction, missing);
            skirtVertexIndices[indexIndex] = int.MaxValue;

            if (math.any(flatten > VoxelUtils.SIZE - 1))
                return;

            if (math.any(flatten == 0 | flatten == (VoxelUtils.SIZE - 1))) {
                if ((math.all(flatten == 0 | flatten == (VoxelUtils.SIZE - 1)))) {
                    /*
                    // what the fuck?
                    bool2 positiveMask = flatten == (VoxelUtils.SIZE - 1);
                    uint3 faceOffset = (uint3)SkirtUtils.UnflattenFromFaceRelative(math.select((uint)0, 1, positiveMask), direction);

                    // special "corner" case, don't even run surface nets, just put it at the corner
                    // I'll be on the safe side and just place the corner even if we don't need one there
                    // we can always run a vertex clean-up job at the very end to get rid of unused vertices
                    int vertexIndex = skirtVertexCounter.Increment();
                    skirtVertexIndices[indexIndex] = vertexIndex;
                    skirtVertices[vertexIndex] = (float3)position - (float3)faceOffset;
                    */
                } else {
                    // special "edge" case, run surface nets in 1D only
                    SurfaceNets1D(indexIndex, direction, negative, position, flatten);
                }
            } else {
                // normal 2D surface nets
                SurfaceNets2D(direction, negative, indexIndex, position);
            }
        }

        private void SurfaceNets2D(int direction, bool negative, int indexIndex, uint3 position) {
            float3 vertex = float3.zero;

            // we need to offset all the skirt vertices by 1 since we need to reserve a space for the edge scenarios
            uint3 faceOffset = (uint3)SkirtUtils.UnflattenFromFaceRelative(new uint2(1, 1), direction);

            int count = 0;
            half average = (half)0f;
            for (int edge = 0; edge < 4; edge++) {
                uint2 startOffset2D = EDGE_POSITIONS_0_CUSTOM[edge];
                uint2 endOffset2D = EDGE_POSITIONS_1_CUSTOM[edge];
                uint3 startOffset = SkirtUtils.UnflattenFromFaceRelative(startOffset2D, direction, (uint)(negative ? 0 : 1));
                uint3 endOffset = SkirtUtils.UnflattenFromFaceRelative(endOffset2D, direction, (uint)(negative ? 0 : 1));


                int startIndex = VoxelUtils.PosToIndex(startOffset + position - faceOffset, VoxelUtils.SIZE);
                int endIndex = VoxelUtils.PosToIndex(endOffset + position - faceOffset, VoxelUtils.SIZE);

                Voxel startVoxel = voxels[startIndex];
                Voxel endVoxel = voxels[endIndex];

                average += startVoxel.density;
                average += endVoxel.density;

                if (startVoxel.density > 0 ^ endVoxel.density > 0) {
                    count++;
                    float value = math.unlerp(startVoxel.density, endVoxel.density, 0);
                    vertex += math.lerp(startOffset, endOffset, value) - math.float3(0.5);
                }
            }

            // forcefully create the vertex 
            average = (half)(average / (half)(8f));

            bool force = average > -threshold && average < 0f;

            if (count == 0 && !force) {
                return;
            }

            int vertexIndex = skirtVertexCounter.Increment();

            // we can use the skirt vertex counter directly since we offset it BEFORE we run this job
            skirtVertexIndices[indexIndex] = vertexIndex;

            float3 offset = 0f;
            if (force && count == 0) {
                offset = 0f;

                if (negative) {
                    offset[direction] = -0.5f;
                } else {
                    offset[direction] = 0.5f;
                }
            } else {
                offset = (vertex / (float)count);
            }

            // apply the offset to the vertex position
            offset -= faceOffset;

            // Whatever you FUCKING DO do NOT change the 0.5f offset
            // It is required to place the vertex INSIDE the cube of 8 voxel data points.
            // Just work with it lil bro
            skirtVertices[vertexIndex] = offset + position + 0.5f;
        }

        static readonly uint3[] forwardDirections = new uint3[3]
        {
            new uint3(1, 0, 0),
            new uint3(0, 1, 0),
            new uint3(0, 0, 1),
        };

        private void SurfaceNets1D(int indexIndex, int faceDirection, bool negative, uint3 position, uint2 flatten) {
            bool2 positiveMask = flatten == (VoxelUtils.SIZE - 1);
            bool2 negativeMask = flatten == 0;
            bool2 local = negativeMask | positiveMask;

            // we need to offset all the skirt vertices by 1 since we need to reserve a space for the edge scenarios
            uint3 faceOffset = (uint3)SkirtUtils.UnflattenFromFaceRelative(math.select((uint)0, 1, positiveMask), faceDirection);

            int edgeDirection = SkirtUtils.GetEdgeDirFaceRelative(local, faceDirection);
            float3 vertex = float3.zero;
            bool spawn = false;
            half average = (half)0f;

            uint3 endOffset = forwardDirections[edgeDirection];
            int startIndex = VoxelUtils.PosToIndex(position - endOffset - faceOffset, VoxelUtils.SIZE);
            int endIndex = VoxelUtils.PosToIndex(position - faceOffset, VoxelUtils.SIZE);

            Voxel startVoxel = voxels[startIndex];
            Voxel endVoxel = voxels[endIndex];

            average += startVoxel.density;
            average += endVoxel.density;

            if (startVoxel.density > 0 ^ endVoxel.density > 0) {
                spawn = true;
                float value = math.unlerp(startVoxel.density, endVoxel.density, 0);
                vertex += math.lerp(-(float3)(endOffset), 0, value);
            }

            // forcefully create the vertex based on density threshold
            average = (half)(average / (half)(2f));
            bool force = average > -threshold && average < 0f;
            if (!spawn && !force) {
                return;
            }

            int vertexIndex = skirtVertexCounter.Increment();

            skirtVertexIndices[indexIndex] = vertexIndex;

            float3 offset = 0f;
            if (force && !spawn) {
                //offset[edgeDirection] = -0.5f;
            } else {
                offset = vertex;
            }

            /*
            if (negative) {
                offset[edgeDirection] -= 0.5f;
            } else {
                offset[edgeDirection] += 0.5f;
            }
            */

            offset -= faceOffset;

            skirtVertices[vertexIndex] = offset + position;
        }
    }
}