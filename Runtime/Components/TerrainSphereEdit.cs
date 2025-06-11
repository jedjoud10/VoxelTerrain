using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

[assembly: RegisterGenericJobType(typeof(jedjoud.VoxelTerrain.Edits.EditStoreJob<jedjoud.VoxelTerrain.Edits.TerrainSphereEdit>))]
namespace jedjoud.VoxelTerrain.Edits {
    public struct TerrainSphereEdit : IComponentData, IEdit {
        public float3 center;
        public float radius;

        public MinMaxAABB GetBounds() {
            return MinMaxAABB.CreateFromCenterAndHalfExtents(center, radius);
        }

        public void Modify(float3 position, ref float voxel) {
            float sphere = math.length(position - center) - radius;
            voxel = math.max(voxel, -sphere);
        }
    }
}