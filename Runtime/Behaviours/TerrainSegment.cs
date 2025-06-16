using System;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Segments {
    public class TerrainSegment {
        public Data data;

        public float3 WorldPosition => SegmentUtils.PHYSICAL_SEGMENT_SIZE * data.position;
        public float3 DispatchScale => (SegmentUtils.PHYSICAL_SEGMENT_SIZE / SegmentUtils.SEGMENT_SIZE);

        public struct Data : IEquatable<TerrainSegment.Data> {
            public int3 position;
            public LevelOfDetail lod;


            public bool Equals(TerrainSegment.Data other) {
                return math.all(position == other.position) && lod == other.lod;
            }

            public enum LevelOfDetail : int {
                // Spawn physical entities at this level
                High,

                // Use billboards at this level
                Low,
            }

            // https://forum.unity.com/threads/burst-error-bc1091-external-and-internal-calls-are-not-allowed-inside-static-constructors.1347293/
            public override int GetHashCode() {
                unchecked {
                    int hash = 17;
                    hash = hash * 23 + position.GetHashCode();
                    hash = hash * 23 + ((int)lod).GetHashCode();
                    return hash;
                }
            }
        }
    }
}