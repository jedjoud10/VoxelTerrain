using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Segments {
    public struct TerrainSegmentOwnedProp : IBufferElementData {
        public Entity entity;
    }
}