using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [BurstCompile(CompileSynchronously = true)]
    public struct SegmentSpawnJob : IJob {
        public NativeHashSet<TerrainSegment> oldSegments;
        public NativeHashSet<TerrainSegment> newSegments;
        
        public float3 center;
        public int3 extent;
        public float lodMultiplier;

        [WriteOnly]
        public NativeList<TerrainSegment> addedSegments;

        [WriteOnly]
        public NativeList<TerrainSegment> removedSegments;

        public int maxSegmentsInWorld;
        public float worldSegmentSize;

        public void Execute() {
            newSegments.Clear();

            int3 c = (int3)extent;
            int3 min = new int3(-maxSegmentsInWorld, -maxSegmentsInWorld, -maxSegmentsInWorld);
            int3 max = new int3(maxSegmentsInWorld, maxSegmentsInWorld, maxSegmentsInWorld);

            int3 offset = (int3)math.round(center / worldSegmentSize);

            // You *could* parallelize this but the loop size is so small that it's just not worth the trouble ngl
            for (int x = -c.x; x < c.x; x++) {
                for (int y = -c.y; y < c.y; y++) {
                    for (int z = -c.z; z < c.z; z++) {
                        int3 segment = new int3(x, y, z) + offset;

                        float3 segmentCenter = ((float3)segment + 0.5f) * worldSegmentSize;
                        float distance = math.distance(center, segmentCenter) / worldSegmentSize;

                        int lod = (int)math.round(distance / math.max(lodMultiplier, 0.01));

                        if (math.all(segment >= min) && math.all(segment < max)) {
                            newSegments.Add(new TerrainSegment {
                                position = segment,
                                lod = math.clamp(lod, 0, 1) == 1 ? TerrainSegment.LevelOfDetail.High : TerrainSegment.LevelOfDetail.Low,
                            });
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