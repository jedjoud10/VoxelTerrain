using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Segments {
    [BurstCompile(CompileSynchronously = true)]
    public struct SegmentSpawnJob : IJob {
        public NativeHashSet<TerrainSegment> oldSegments;
        public NativeHashSet<TerrainSegment> newSegments;
        
        public NativeArray<TerrainLoader> loaders;
        public NativeArray<LocalTransform> loaderTransforms;

        [WriteOnly]
        public NativeList<TerrainSegment> addedSegments;

        [WriteOnly]
        public NativeList<TerrainSegment> removedSegments;

        public int maxSegmentsInWorld;
        public float worldSegmentSize;

        public void Execute() {
            newSegments.Clear();

            for (int l = 0; l < loaders.Length; l++) {
                TerrainLoader loader = loaders[l];
                LocalTransform transform = loaderTransforms[l];
                float3 center = transform.Position;
                int3 extent = loader.segmentExtent;
                int3 extentHigh = loader.segmentExtentHigh;


                int3 c = (int3)extent;
                int3 min = new int3(-maxSegmentsInWorld);
                int3 max = new int3(maxSegmentsInWorld);

                int3 offset = (int3)math.round(center / worldSegmentSize);

                // pls ooptimuze...
                for (int x = -c.x; x < c.x; x++) {
                    for (int y = -c.y; y < c.y; y++) {
                        for (int z = -c.z; z < c.z; z++) {
                            int3 localSegment = new int3(x, y, z);
                            int3 worldSegment = localSegment + offset;

                            float3 segmentCenter = ((float3)worldSegment + 0.5f) * worldSegmentSize;
                            float distance = math.distance(center, segmentCenter) / worldSegmentSize;

                            if (math.all(worldSegment >= min) && math.all(worldSegment < max)) {
                                var lod = TerrainSegment.LevelOfDetail.Low;

                                if (math.all(localSegment >= -extentHigh) && math.all(localSegment < extentHigh)) {
                                    lod = TerrainSegment.LevelOfDetail.High;
                                }

                                newSegments.Add(new TerrainSegment {
                                    position = worldSegment,
                                    lod = lod,
                                });
                            }
                        }
                    }
                }
            }


            addedSegments.Clear();
            removedSegments.Clear();

            foreach (var item in newSegments) {
                if (!oldSegments.Contains(item)) {
                    addedSegments.Add(item);
                }
            }

            foreach (var item in oldSegments) {
                if (!newSegments.Contains(item)) {
                    removedSegments.Add(item);
                }
            }

            oldSegments.Clear();
            foreach (var item in newSegments) {
                oldSegments.Add(item);
            }
        }
    }
}