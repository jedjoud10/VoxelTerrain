using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct SkirtVertexJob : IJobParallelFor {
        [ReadOnly]
        public VoxelData voxels;
        [ReadOnly]
        public NativeArray<float3> voxelNormals;
        [ReadOnly]
        public NativeArray<bool> withinThreshold;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> skirtVertexIndicesGenerated;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public Vertices skirtVertices;

        public NativeCounter.Concurrent skirtVertexCounter;

        [ReadOnly]
        public NativeCounter vertexCounter;

        public static readonly uint2[] EDGE_POSITIONS_0_CUSTOM = new uint2[] {
            new uint2(0, 0),
            new uint2(0, 1),
            new uint2(1, 1),
            new uint2(1, 0),
        };

        public static readonly uint2[] EDGE_POSITIONS_1_CUSTOM = new uint2[] {
            new uint2(0, 1),
            new uint2(1, 1),
            new uint2(1, 0),
            new uint2(0, 0),
        };

        struct VertexToSpawn {
            public Vertices.Single inner;
            public bool shouldSpawn;

            public bool useWorldPosition;
            public float3 worldPosition;
            public bool forced;
        }

        public void Execute(int index) {
            int face = index / VoxelUtils.SKIRT_FACE;
            int direction = face % 3;
            bool negative = face < 3;
            uint missing = negative ? 0 : ((uint)VoxelUtils.SIZE - 3);
            int localIndex = index % VoxelUtils.SKIRT_FACE;
            int indexIndex = localIndex + face * VoxelUtils.SKIRT_FACE;

            // hold up I'm resetted
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

            int count = BitUtils.CountTrue(minMaxMask);
            // if corner case, then only 2 of the above are true
            // if edge case, then only 1 of the above is true

            if (count == 2) {
                // corner case!!!!
                vertex = CreateCorner(direction, negative, flatten);
            } else if (count == 1) {
                // edge case!!!
                vertex = CreateSurfaceNets1D(flatten, minMaxMask, negative, face);
            } else {
                // normal face case!!!
                uint3 position = SkirtUtils.UnflattenFromFaceRelative(flatten - 1, direction, missing);
                vertex = CreateSurfaceNets2D(face, negative, position);
            }

            // Actually spawn the vertex if needed
            if (vertex.shouldSpawn) {
                int vertexIndex = skirtVertexCounter.Increment();

                if (vertex.useWorldPosition) {
                    vertex.inner.position = (vertex.worldPosition + vertex.inner.position);
                } else {
                    vertex.inner.position = ((float3)unoffsetted + vertex.inner.position);
                }

                skirtVertices[vertexIndex] = vertex.inner;

                // We want the triangles that utilize these vertex indices to refer to these vertices *after* we have merged them to the original mesh (after the SN vertices)
                skirtVertexIndicesGenerated[indexIndex] = vertexCounter.Count + vertexIndex;
            }
        }

        private VertexToSpawn CreateCorner(int direction, bool negative, uint2 flatten) {
            // TODO: fix me. removed it cause of bounds stuff
            return new VertexToSpawn { shouldSpawn = false };
            /*
            VertexToSpawn vertex;
            uint missing2 = negative ? 0 : ((uint)VoxelUtils.SIZE - 2);
            uint2 flatten2 = math.clamp(flatten, 0, VoxelUtils.SIZE - 2);
            float3 worldPos = SkirtUtils.UnflattenFromFaceRelative(flatten2, direction, missing2);

            // corner case!!!
            vertex = new VertexToSpawn {
                offset = 0f,
                shouldSpawn = true,
                worldPosition = worldPos,
                useWorldPosition = true,
            };
            return vertex;
            */
        }

        private VertexToSpawn CreateSurfaceNets2D(int face, bool negative, uint3 position) {
            int faceDir = face % 3;

            Vertices.Single vertex = new Vertices.Single();
            Vertices.Single forcedVertex = new Vertices.Single();
            bool force = false;

            uint2 flat = SkirtUtils.FlattenToFaceRelative(position, faceDir);

            int count = 0;
            for (int edge = 0; edge < 4; edge++) {
                uint2 startOffset2D = EDGE_POSITIONS_0_CUSTOM[edge];
                uint2 endOffset2D = EDGE_POSITIONS_1_CUSTOM[edge];

                uint3 startOffset = SkirtUtils.UnflattenFromFaceRelative(startOffset2D, faceDir, (uint)(negative ? 0 : 1));
                uint3 endOffset = SkirtUtils.UnflattenFromFaceRelative(endOffset2D, faceDir, (uint)(negative ? 0 : 1));

                int startIndex = VoxelUtils.PosToIndex(startOffset + position, VoxelUtils.SIZE);
                int endIndex = VoxelUtils.PosToIndex(endOffset + position, VoxelUtils.SIZE);

                float startDensity = voxels.densities[startIndex];
                float endDensity = voxels.densities[endIndex];


                uint2 startPosition2D = startOffset2D + flat;
                uint2 endPosition2D = endOffset2D + flat;
                int withinThresholdFaceIndex = VoxelUtils.FACE * face;
                force |= withinThreshold[VoxelUtils.PosToIndex2D(startPosition2D, VoxelUtils.SIZE) + withinThresholdFaceIndex];
                force |= withinThreshold[VoxelUtils.PosToIndex2D(endPosition2D, VoxelUtils.SIZE) + withinThresholdFaceIndex];

                if (startDensity >= 0 ^ endDensity >= 0) {
                    count++;
                    vertex.Add(startOffset, endOffset, startIndex, endIndex, ref voxels, ref voxelNormals);
                }

                forcedVertex.AddLerped(startOffset, endOffset, startIndex, endIndex, 0.5f, ref voxels, ref voxelNormals);
            }



            if (count == 0) {
                if (force) {
                    forcedVertex.Finalize(4);
                    float3 middle2D = SkirtUtils.UnflattenFromFaceRelative(-0.5f, faceDir, negative ? 0 : 1);
                    forcedVertex.position = middle2D;
                    return new VertexToSpawn {
                        inner = forcedVertex,
                        shouldSpawn = true,
                        forced = true,
                    };
                } else {
                    return default;
                }
            } else {
                vertex.Finalize(count);
                vertex.position -= SkirtUtils.UnflattenFromFaceRelative(new uint2(1), faceDir);

                return new VertexToSpawn {
                    inner = vertex,
                    shouldSpawn = true,
                };
            }
        }

        private VertexToSpawn CreateSurfaceNets1D(uint2 flatten, bool4 minMaxMask, bool negative, int face) {
            int faceDir = face % 3;

            bool2 mask = new bool2(minMaxMask.x || minMaxMask.z, minMaxMask.y || minMaxMask.w);
            int edgeDir = SkirtUtils.GetEdgeDirFaceRelative(mask, faceDir);

            uint missing = negative ? 0 : ((uint)VoxelUtils.SIZE - 2);
            flatten = math.clamp(flatten, 0, VoxelUtils.SIZE - 2);
            float3 worldPos = SkirtUtils.UnflattenFromFaceRelative(flatten, faceDir, missing);

            Vertices.Single vertex = new Vertices.Single();
            Vertices.Single forcedVertex = new Vertices.Single();
            bool force = false;
            bool spawn = false;

            uint3 endOffset = DirectionOffsetUtils.FORWARD_DIRECTION[edgeDir];
            uint3 unoffsetted = SkirtUtils.UnflattenFromFaceRelative(flatten, faceDir, missing);
            int startIndex = VoxelUtils.PosToIndex(unoffsetted - endOffset, VoxelUtils.SIZE);
            int endIndex = VoxelUtils.PosToIndex(unoffsetted, VoxelUtils.SIZE);

            float startDensity = voxels.densities[startIndex];
            float endDensity = voxels.densities[endIndex];

            uint2 startPosition2D = flatten - SkirtUtils.FlattenToFaceRelative(endOffset, faceDir);
            uint2 endPosition2D = flatten;
            int withinThresholdFaceIndex = VoxelUtils.FACE * face;
            force |= withinThreshold[VoxelUtils.PosToIndex2D(startPosition2D, VoxelUtils.SIZE) + withinThresholdFaceIndex];
            force |= withinThreshold[VoxelUtils.PosToIndex2D(endPosition2D, VoxelUtils.SIZE) + withinThresholdFaceIndex];

            if (startDensity >= 0 ^ endDensity >= 0) {
                spawn = true;
                vertex.Add(-(float3)(endOffset), 0, startIndex, endIndex, ref voxels, ref voxelNormals);
            }

            forcedVertex.AddLerped(-(float3)(endOffset), 0, startIndex, endIndex, 0.5f, ref voxels, ref voxelNormals);

            if (spawn) {
                vertex.Finalize(1);
                return new VertexToSpawn {
                    inner = vertex,
                    worldPosition = worldPos,
                    shouldSpawn = spawn,
                    useWorldPosition = true,
                    forced = false,
                };
            } else {
                if (force) {
                    forcedVertex.Finalize(1);
                    forcedVertex.position = -(float3)(endOffset) * 0.5f;
                    return new VertexToSpawn {
                        worldPosition = worldPos,
                        inner = forcedVertex,
                        shouldSpawn = true,
                        useWorldPosition = true,
                    };
                } else {
                    return default;
                }
            }
        }
    }
}