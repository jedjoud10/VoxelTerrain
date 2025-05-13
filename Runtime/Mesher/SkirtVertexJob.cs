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
        public NativeArray<int> skirtVertexIndicesGenerated;

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

        public float threshold;

        struct VertexToSpawn {
            // within the cell!!!
            public float3 offset;
            public bool shouldSpawn;

            public bool useWorldPosition;
            public float3 worldPosition;
        }

        public void Execute(int index) {
            int face = index / VoxelUtils.SKIRT_FACE;
            int direction = face % 3;
            bool negative = face < 3;


            uint missing = negative ? 0 : ((uint)VoxelUtils.SIZE - 2);

            int localIndex = index % VoxelUtils.SKIRT_FACE;
            int indexIndex = localIndex + face * VoxelUtils.SKIRT_FACE;

            skirtVertexIndicesGenerated[indexIndex] = int.MaxValue;

            uint2 flatten = VoxelUtils.IndexToPos2D(localIndex, VoxelUtils.SKIRT_SIZE);
            uint3 unoffsetted = SkirtUtils.UnflattenFromFaceRelative(flatten, direction, missing);

            // vertex that we will spawn for this iteration!!!
            VertexToSpawn vertex = default;

            // mask to check if we're dealing with an edge case or corner case
            bool4 minMaxMask = new bool4(false);
            minMaxMask.x = flatten.x == 0;
            minMaxMask.y = flatten.y == 0;
            minMaxMask.z = flatten.x == (VoxelUtils.SKIRT_SIZE - 1);
            minMaxMask.w = flatten.y == (VoxelUtils.SKIRT_SIZE - 1);

            int count = SkirtUtils.CountTrue(minMaxMask);
            // if corner case, then only 2 of the above are true
            // if edge case, then only 1 of the above is true

            if (count == 2) {
                // corner case!!!
                vertex = default;
                /*
                // TODO: I know this is bad but wtv...
                vertex = new VertexToSpawn {
                    offset = 0.5f,
                    shouldSpawn = true,
                };
                */
            } else if (count == 1) {
                // edge case!!!
                vertex = SurfaceNets1D(flatten, minMaxMask, negative, direction);
            } else {
                // normal face case!!!
                uint3 position = SkirtUtils.UnflattenFromFaceRelative(flatten - 1, direction, missing);
                vertex = SurfaceNets2D(direction, negative, position, unoffsetted);
            }

            // Actually spawn the vertex if needed
            if (vertex.shouldSpawn) {
                int vertexIndex = skirtVertexCounter.Increment();
                skirtVertexIndicesGenerated[indexIndex] = vertexIndex;

                if (vertex.useWorldPosition) {
                    skirtVertices[vertexIndex] = vertex.worldPosition + vertex.offset;
                } else {
                    skirtVertices[vertexIndex] = (float3)unoffsetted + vertex.offset;
                }
            }
        }

        private VertexToSpawn SurfaceNets2D(int direction, bool negative, uint3 position, uint3 unoffsetted) {
            float3 vertex = float3.zero;
            float average = 0f;

            int count = 0;
            for (int edge = 0; edge < 4; edge++) {
                uint2 startOffset2D = EDGE_POSITIONS_0_CUSTOM[edge];
                uint2 endOffset2D = EDGE_POSITIONS_1_CUSTOM[edge];
                uint3 startOffset = SkirtUtils.UnflattenFromFaceRelative(startOffset2D, direction, (uint)(negative ? 0 : 1));
                uint3 endOffset = SkirtUtils.UnflattenFromFaceRelative(endOffset2D, direction, (uint)(negative ? 0 : 1));


                int startIndex = VoxelUtils.PosToIndex(startOffset + position, VoxelUtils.SIZE);
                int endIndex = VoxelUtils.PosToIndex(endOffset + position, VoxelUtils.SIZE);

                Voxel startVoxel = voxels[startIndex];
                Voxel endVoxel = voxels[endIndex];

                average += startVoxel.density;
                average += endVoxel.density;

                if (startVoxel.density >= 0 ^ endVoxel.density >= 0) {
                    count++;
                    float value = math.unlerp(startVoxel.density, endVoxel.density, 0);
                    vertex += math.lerp(startOffset, endOffset, value);
                }
            }

            average /= 8f;

            if (count == 0) {
                if (average > -threshold && average < 0) {
                    float3 middle2D = SkirtUtils.UnflattenFromFaceRelative(-0.5f, direction, negative ? 0 : 1);
                    return new VertexToSpawn {
                        offset = middle2D,
                        shouldSpawn = true
                    };
                } else {
                    return default;
                }
            }

            return new VertexToSpawn {
                offset = (vertex / (float)count) - SkirtUtils.UnflattenFromFaceRelative(new uint2(1), direction),
                shouldSpawn = true
            };
        }

        static readonly uint3[] forwardDirections = new uint3[3]
        {
            new uint3(1, 0, 0),
            new uint3(0, 1, 0),
            new uint3(0, 0, 1),
        };

        private VertexToSpawn SurfaceNets1D(uint2 flatten, bool4 minMaxMask, bool negative, int faceNormalDir) {
            bool2 mask = new bool2(minMaxMask.x || minMaxMask.z, minMaxMask.y || minMaxMask.w);
            int edgeDir = SkirtUtils.GetEdgeDirFaceRelative(mask, faceNormalDir);

            uint missing = negative ? 0 : ((uint)VoxelUtils.SIZE - 1);
            flatten = math.clamp(flatten, 0, VoxelUtils.SIZE - 1);
            float3 worldPos = SkirtUtils.UnflattenFromFaceRelative(flatten, faceNormalDir, missing);
            //worldPos[edgeDir] -= 0.5f;

            float3 vertex = float3.zero;
            float average = 0f;
            bool spawn = false;

            uint3 endOffset = forwardDirections[edgeDir];
            uint3 unoffsetted = SkirtUtils.UnflattenFromFaceRelative(flatten, faceNormalDir, missing);
            int startIndex = VoxelUtils.PosToIndex(unoffsetted - endOffset, VoxelUtils.SIZE);
            int endIndex = VoxelUtils.PosToIndex(unoffsetted, VoxelUtils.SIZE);

            Voxel startVoxel = voxels[startIndex];
            Voxel endVoxel = voxels[endIndex];

            average += startVoxel.density;
            average += endVoxel.density;
            average /= 2f;

            if (startVoxel.density >= 0 ^ endVoxel.density >= 0) {
                spawn = true;
                float value = math.unlerp(startVoxel.density, endVoxel.density, 0);
                vertex += math.lerp(-(float3)(endOffset), 0, value);
            }

            if (spawn) {
                return new VertexToSpawn {
                    worldPosition = worldPos,
                    offset = vertex,
                    shouldSpawn = spawn,
                    useWorldPosition = true,
                };
            } else {
                if (average > -threshold && average < 0) {
                    return new VertexToSpawn {
                        worldPosition = worldPos,
                        offset = -(float3)(endOffset) * 0.5f,
                        shouldSpawn = true,
                        useWorldPosition = true,
                    };
                } else {
                    return default;
                }
            }
        }



        /*
        private void SurfaceNets1D(int indexIndex, int faceDirection, bool negative, uint3 position, uint2 flatten) {
            // special "corner" case, don't even run surface nets, just put it at the corner
            // I'll be on the safe side and just place the corner even if we don't need one there
            // we can always run a vertex clean-up job at the very end to get rid of unused vertices
            int vertexIndex = skirtVertexCounter.Increment();
            skirtVertexIndices[indexIndex] = vertexIndex;
            skirtVertices[vertexIndex] = (float3)position;
            /*
            bool2 positiveMask = flatten == (VoxelUtils.SIZE - 1);
            bool2 negativeMask = flatten == 0;
            bool2 local = negativeMask | positiveMask;

            //uint3 faceOffset = (uint3)SkirtUtils.UnflattenFromFaceRelative(new uint2(1, 1), faceDirection);
            // we need to offset all the skirt vertices by 1 since we need to reserve a space for the edge scenarios
            //uint3 faceOffset = (uint3)SkirtUtils.UnflattenFromFaceRelative(math.select((uint)1, 0, positiveMask), faceDirection);

            int edgeDirection = SkirtUtils.GetEdgeDirFaceRelative(local, faceDirection);
            float3 vertex = float3.zero;
            bool spawn = false;
            half average = (half)0f;

            uint3 endOffset = forwardDirections[edgeDirection];
            int startIndex = VoxelUtils.PosToIndex(position - endOffset, VoxelUtils.SIZE);
            int endIndex = VoxelUtils.PosToIndex(position, VoxelUtils.SIZE);

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
                        
            //offset -= faceOffset;

            skirtVertices[vertexIndex] = offset + position;
        }
        */
    }
}