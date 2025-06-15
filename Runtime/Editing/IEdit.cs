using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    public interface IEdit {
        public void Modify(float3 position, ref EditVoxel voxel);
        public MinMaxAABB GetBounds();
    }
}