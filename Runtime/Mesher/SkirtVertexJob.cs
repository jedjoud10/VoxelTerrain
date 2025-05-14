using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SkirtVertexJob : IJobParallelFor {                
        [ReadOnly]
        public NativeArray<Voxel> voxels;
        [ReadOnly]
        public NativeArray<bool> withinThreshold;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> skirtVertexIndicesGenerated;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> skirtVertices;

        public Unsafe.NativeCounter.Concurrent skirtVertexCounter;

        public float voxelScale;

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
            // within the cell!!!
            public float3 offset;
            public bool shouldSpawn;

            public bool useWorldPosition;
            public float3 worldPosition;
            public bool forced;
        }

        public void Execute(int index) {
            int face = index / VoxelUtils.SKIRT_FACE;
            int direction = face % 3;
            bool negative = face < 3;
            uint missing = negative ? 0 : ((uint)VoxelUtils.SIZE - 2);
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
                vertex = SurfaceNets1D(flatten, minMaxMask, negative, face);
            } else {
                // normal face case!!!
                uint3 position = SkirtUtils.UnflattenFromFaceRelative(flatten - 1, direction, missing);
                vertex = SurfaceNets2D(face, negative, position);
            }

            // Actually spawn the vertex if needed
            if (vertex.shouldSpawn) {
                int vertexIndex = skirtVertexCounter.Increment();
                skirtVertexIndicesGenerated[indexIndex] = vertexIndex;

                if (vertex.useWorldPosition) {
                    skirtVertices[vertexIndex] = (vertex.worldPosition + vertex.offset) * voxelScale;
                } else {
                    skirtVertices[vertexIndex] = ((float3)unoffsetted + vertex.offset) * voxelScale;
                }
            }
        }

        private VertexToSpawn SurfaceNets2D(int face, bool negative, uint3 position) {
            int faceDir = face % 3;
            
            float3 vertex = float3.zero;
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

                Voxel startVoxel = voxels[startIndex];
                Voxel endVoxel = voxels[endIndex];

                uint2 startPosition2D = startOffset2D + flat;
                uint2 endPosition2D = endOffset2D + flat;
                int withinThresholdFaceIndex = VoxelUtils.FACE * face;
                force |= withinThreshold[VoxelUtils.PosToIndex2D(startPosition2D, VoxelUtils.SIZE) + withinThresholdFaceIndex];
                force |= withinThreshold[VoxelUtils.PosToIndex2D(endPosition2D, VoxelUtils.SIZE) + withinThresholdFaceIndex];

                if (startVoxel.density >= 0 ^ endVoxel.density >= 0) {
                    count++;
                    float value = math.unlerp(startVoxel.density, endVoxel.density, 0);
                    vertex += math.lerp(startOffset, endOffset, value);
                }
            }

            if (count == 0) {
                if (force) {
                    float3 middle2D = SkirtUtils.UnflattenFromFaceRelative(-0.5f, faceDir, negative ? 0 : 1);
                    return new VertexToSpawn {
                        offset = middle2D,
                        shouldSpawn = true,
                        forced = true,
                    };
                } else {
                    return default;
                }
            }

            return new VertexToSpawn {
                offset = (vertex / (float)count) - SkirtUtils.UnflattenFromFaceRelative(new uint2(1), faceDir),
                shouldSpawn = true
            };
        }

        private VertexToSpawn SurfaceNets1D(uint2 flatten, bool4 minMaxMask, bool negative, int face) {
            int faceDir = face % 3;

            bool2 mask = new bool2(minMaxMask.x || minMaxMask.z, minMaxMask.y || minMaxMask.w);
            int edgeDir = SkirtUtils.GetEdgeDirFaceRelative(mask, faceDir);

            uint missing = negative ? 0 : ((uint)VoxelUtils.SIZE - 1);
            flatten = math.clamp(flatten, 0, VoxelUtils.SIZE - 1);
            float3 worldPos = SkirtUtils.UnflattenFromFaceRelative(flatten, faceDir, missing);

            float3 vertex = float3.zero;
            bool force = false;
            bool spawn = false;

            uint3 endOffset = VoxelUtils.FORWARD_DIRECTION[edgeDir];
            uint3 unoffsetted = SkirtUtils.UnflattenFromFaceRelative(flatten, faceDir, missing);
            int startIndex = VoxelUtils.PosToIndex(unoffsetted - endOffset, VoxelUtils.SIZE);
            int endIndex = VoxelUtils.PosToIndex(unoffsetted, VoxelUtils.SIZE);

            Voxel startVoxel = voxels[startIndex];
            Voxel endVoxel = voxels[endIndex];

            uint2 startPosition2D = flatten - SkirtUtils.FlattenToFaceRelative(endOffset, faceDir);
            uint2 endPosition2D = flatten;
            int withinThresholdFaceIndex = VoxelUtils.FACE * face;
            force |= withinThreshold[VoxelUtils.PosToIndex2D(startPosition2D, VoxelUtils.SIZE) + withinThresholdFaceIndex];
            force |= withinThreshold[VoxelUtils.PosToIndex2D(endPosition2D, VoxelUtils.SIZE) + withinThresholdFaceIndex];

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
                if (force) {
                    return new VertexToSpawn {
                        worldPosition = worldPos,
                        offset = -(float3)(endOffset) * 0.5f,
                        shouldSpawn = true,
                        useWorldPosition = true,
                        forced = true,
                    };
                } else {
                    return default;
                }
            }
        }
    }
}