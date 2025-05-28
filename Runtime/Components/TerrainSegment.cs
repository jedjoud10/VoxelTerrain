using System;
using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Segments {
    public struct TerrainSegment : IComponentData, IEquatable<TerrainSegment> {
        public int3 position;
        public LevelOfDetail lod;

        public bool Equals(TerrainSegment other) {
            return math.all(position == other.position) && lod == other.lod;
        }

        public enum LevelOfDetail: int {
            // Spawn physical entities at this level
            High,
            
            // Use billboards at this level
            Low
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