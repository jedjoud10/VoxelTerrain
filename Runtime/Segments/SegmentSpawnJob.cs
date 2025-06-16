using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Segments {
    [BurstCompile(CompileSynchronously = true)]
    public struct SegmentSpawnJob : IJob {
        public NativeHashSet<TerrainSegment.Data> oldSegments;
        public NativeHashSet<TerrainSegment.Data> newSegments;
        
        public NativeArray<TerrainLoader.Data> loaders;

        [WriteOnly]
        public NativeList<TerrainSegment.Data> addedSegments;

        [WriteOnly]
        public NativeList<TerrainSegment.Data> removedSegments;

        public int maxSegmentsInWorld;
        public float worldSegmentSize;

        public void Execute() {
            newSegments.Clear();

            // TODO: implement clustering algorithm to make this faster...
            for (int l = 0; l < loaders.Length; l++) {
                TerrainLoader.Data loader = loaders[l];
                float3 center = loader.position;
                int3 extent = loader.segmentExtent;
                int3 extentHigh = loader.segmentExtentHigh;


                int3 c = (int3)extent;
                int3 min = new int3(-maxSegmentsInWorld);
                int3 max = new int3(maxSegmentsInWorld);

                int3 offset = (int3)math.round(center / worldSegmentSize);

                // TODO: pls ooptimuze...
                for (int x = -c.x; x < c.x; x++) {
                    for (int y = -c.y; y < c.y; y++) {
                        for (int z = -c.z; z < c.z; z++) {
                            int3 localSegment = new int3(x, y, z);
                            int3 worldSegment = localSegment + offset;

                            float3 segmentCenter = ((float3)worldSegment + 0.5f) * worldSegmentSize;
                            float distance = math.distance(center, segmentCenter) / worldSegmentSize;

                            if (math.all(worldSegment >= min) && math.all(worldSegment < max)) {
                                var lod = TerrainSegment.Data.LevelOfDetail.Low;

                                if (math.all(localSegment >= -extentHigh) && math.all(localSegment < extentHigh)) {
                                    lod = TerrainSegment.Data.LevelOfDetail.High;
                                }

                                newSegments.Add(new TerrainSegment.Data {
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